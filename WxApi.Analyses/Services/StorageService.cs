using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using WxApi.Analyses.Models;

namespace WxApi.Analyses.Services;

public interface IStorageService
{
    Task<DecisionRecord> SaveDecisionAsync(ContactItem contact, string action, string pitch);
    Task<List<DecisionRecord>> GetRecordsAsync(string listType);
    Task<string> GetStatusForWxidAsync(string wxid);
    Task<Dictionary<string, string>> GetAllStatusesAsync();
    Task<StatsOverview> GetStatsAsync(int totalContacts, Dictionary<string, int> tierDistribution);
    string GetFilePath(string listType);
    string GetTrainingSamplesFilePath();
    Task<List<AiTrainingSample>> GetTrainingSamplesAsync();
    Task<AiTrainingSample> SaveTrainingSampleAsync(SaveAiTrainingSampleRequest req);
}

public class StorageService : IStorageService
{
    private readonly string _dataDir;
    private readonly JsonSerializerOptions _jsonOptions;
    private static readonly object _fileLock = new();

    public StorageService(IConfiguration config)
    {
        var configuredDir = config["AppConfig:DataDirectory"] ?? "data";
        _dataDir = Path.IsPathRooted(configuredDir) 
            ? configuredDir 
            : Path.Combine(AppContext.BaseDirectory, configuredDir);

        if (!Directory.Exists(_dataDir))
        {
            Directory.CreateDirectory(_dataDir);
        }

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }

    public string GetFilePath(string listType)
    {
        var fileName = listType.ToLowerInvariant() switch
        {
            "linked" => "linked_contacts.json",
            "starred" => "starred_contacts.json",
            "ignored" => "ignored_contacts.json",
            _ => $"{listType.ToLowerInvariant()}_contacts.json"
        };
        return Path.Combine(_dataDir, fileName);
    }

    public Task<List<DecisionRecord>> GetRecordsAsync(string listType)
    {
        var filePath = GetFilePath(listType);
        if (!File.Exists(filePath)) return Task.FromResult(new List<DecisionRecord>());

        try
        {
            string json;
            lock (_fileLock)
            {
                json = File.ReadAllText(filePath);
            }
            var list = JsonSerializer.Deserialize<List<DecisionRecord>>(json) ?? new List<DecisionRecord>();
            return Task.FromResult(list);
        }
        catch
        {
            return Task.FromResult(new List<DecisionRecord>());
        }
    }

    private Task SaveRecordsAsync(string listType, List<DecisionRecord> records)
    {
        var filePath = GetFilePath(listType);
        var json = JsonSerializer.Serialize(records, _jsonOptions);
        lock (_fileLock)
        {
            File.WriteAllText(filePath, json);

            // 同时镜像保存一份到项目源代码目录下的 data 目录方便直接在编辑器中查阅
            try
            {
                var srcDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "data"));
                if (!Directory.Exists(srcDir)) Directory.CreateDirectory(srcDir);
                var srcFile = Path.Combine(srcDir, Path.GetFileName(filePath));
                File.WriteAllText(srcFile, json);
            }
            catch { }
        }
        return Task.CompletedTask;
    }

    public async Task<DecisionRecord> SaveDecisionAsync(ContactItem contact, string action, string pitch)
    {
        // 1. 先从另外两个列表中移除（如果之前存在）
        var allTypes = new[] { "linked", "starred", "ignored" };
        var targetType = action.ToLowerInvariant() switch
        {
            "linked" => "linked",
            "starred" => "starred",
            "ignored" => "ignored",
            _ => "linked"
        };

        foreach (var t in allTypes)
        {
            var records = await GetRecordsAsync(t);
            var removed = records.RemoveAll(r => string.Equals(r.Wxid, contact.Wxid, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                await SaveRecordsAsync(t, records);
            }
        }

        // 2. 加入目标列表
        var targetRecords = await GetRecordsAsync(targetType);
        var newRecord = new DecisionRecord
        {
            Wxid = contact.Wxid,
            Name = contact.Name,
            Remark = contact.Remark,
            Tier = contact.Tier,
            Action = action.ToUpperInvariant(),
            Pitch = pitch,
            CreatedAt = DateTime.Now
        };
        targetRecords.Insert(0, newRecord);
        await SaveRecordsAsync(targetType, targetRecords);

        return newRecord;
    }

    public async Task<string> GetStatusForWxidAsync(string wxid)
    {
        var linked = await GetRecordsAsync("linked");
        if (linked.Any(r => string.Equals(r.Wxid, wxid, StringComparison.OrdinalIgnoreCase))) return "LINKED";

        var starred = await GetRecordsAsync("starred");
        if (starred.Any(r => string.Equals(r.Wxid, wxid, StringComparison.OrdinalIgnoreCase))) return "STARRED";

        var ignored = await GetRecordsAsync("ignored");
        if (ignored.Any(r => string.Equals(r.Wxid, wxid, StringComparison.OrdinalIgnoreCase))) return "IGNORED";

        return "PENDING";
    }

    public async Task<Dictionary<string, string>> GetAllStatusesAsync()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var linked = await GetRecordsAsync("linked");
        foreach (var r in linked) result[r.Wxid] = "LINKED";

        var starred = await GetRecordsAsync("starred");
        foreach (var r in starred) result[r.Wxid] = "STARRED";

        var ignored = await GetRecordsAsync("ignored");
        foreach (var r in ignored) result[r.Wxid] = "IGNORED";

        return result;
    }

    public async Task<StatsOverview> GetStatsAsync(int totalContacts, Dictionary<string, int> tierDistribution)
    {
        var linked = (await GetRecordsAsync("linked")).Count;
        var starred = (await GetRecordsAsync("starred")).Count;
        var ignored = (await GetRecordsAsync("ignored")).Count;
        var handled = linked + starred + ignored;

        return new StatsOverview
        {
            TotalContacts = totalContacts,
            LinkedCount = linked,
            StarredCount = starred,
            IgnoredCount = ignored,
            PendingCount = Math.Max(0, totalContacts - handled),
            TierDistribution = tierDistribution
        };
    }

    public string GetTrainingSamplesFilePath()
    {
        return Path.Combine(_dataDir, "ai_training_samples.json");
    }

    public Task<List<AiTrainingSample>> GetTrainingSamplesAsync()
    {
        var filePath = GetTrainingSamplesFilePath();
        if (!File.Exists(filePath)) return Task.FromResult(new List<AiTrainingSample>());

        try
        {
            string json;
            lock (_fileLock)
            {
                json = File.ReadAllText(filePath);
            }
            var list = JsonSerializer.Deserialize<List<AiTrainingSample>>(json) ?? new List<AiTrainingSample>();
            return Task.FromResult(list);
        }
        catch
        {
            return Task.FromResult(new List<AiTrainingSample>());
        }
    }

    public async Task<AiTrainingSample> SaveTrainingSampleAsync(SaveAiTrainingSampleRequest req)
    {
        var list = await GetTrainingSamplesAsync();

        // 移除同 Wxid 的历史样本，确保最新编辑的强化样本置顶
        list.RemoveAll(s => string.Equals(s.Wxid, req.Wxid, StringComparison.OrdinalIgnoreCase));

        var newSample = new AiTrainingSample
        {
            Wxid = req.Wxid,
            Name = req.Name,
            Remark = req.Remark,
            Tier = req.Tier,
            OriginalPitch = req.OriginalPitch,
            EditedPitch = req.EditedPitch,
            PainPoint = req.PainPoint,
            Profile = req.Profile,
            CreatedAt = DateTime.Now
        };

        list.Insert(0, newSample);

        var filePath = GetTrainingSamplesFilePath();
        var json = JsonSerializer.Serialize(list, _jsonOptions);
        lock (_fileLock)
        {
            File.WriteAllText(filePath, json);

            // 同时镜像保存到项目源码 data 目录
            try
            {
                var srcDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "data"));
                if (!Directory.Exists(srcDir)) Directory.CreateDirectory(srcDir);
                var srcFile = Path.Combine(srcDir, Path.GetFileName(filePath));
                File.WriteAllText(srcFile, json);
            }
            catch { }
        }

        return newSample;
    }

}
