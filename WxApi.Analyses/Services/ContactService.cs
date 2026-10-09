using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MiniExcelLibs;
using WxApi.Analyses.Models;

namespace WxApi.Analyses.Services;

public interface IContactService
{
    Task<List<ContactItem>> GetContactsAsync(string? tier = null, string? search = null, string? status = null);
    Task<ContactItem?> GetContactByWxidAsync(string wxid);
    Task<Dictionary<string, int>> GetTierCountsAsync();
    Task<int> GetTotalCountAsync();
    void ReloadCache();
}

public class ContactService : IContactService
{
    private readonly Microsoft.Extensions.Configuration.IConfiguration _config;
    private readonly IStorageService _storageService;
    private readonly ILogger<ContactService> _logger;
    private List<ContactItem>? _cachedContacts;
    private readonly object _lock = new();

    public ContactService(Microsoft.Extensions.Configuration.IConfiguration config, IStorageService storageService, ILogger<ContactService> logger)
    {
        _config = config;
        _storageService = storageService;
        _logger = logger;
    }

    private string ResolveExcelPath()
    {
        var configured = _config["AppConfig:ExcelPath"];
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(configured))
        {
            candidates.Add(configured);
            candidates.Add(Path.Combine(AppContext.BaseDirectory, configured));
            candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), configured));
        }

        candidates.Add("samuelsurry_全部好友副业微创业意向全景透视表.xlsx");
        candidates.Add("01_samuelsurry_全部好友副业微创业意向全景透视表.xlsx");
        candidates.Add(Path.Combine("..", "samuelsurry_全部好友副业微创业意向全景透视表.xlsx"));
        candidates.Add(Path.Combine("d:\\WxHookSource41827", "samuelsurry_全部好友副业微创业意向全景透视表.xlsx"));

        foreach (var c in candidates)
        {
            if (File.Exists(c))
            {
                return Path.GetFullPath(c);
            }
        }

        throw new FileNotFoundException("未能找到 Excel 文件：samuelsurry_全部好友副业微创业意向全景透视表.xlsx");
    }

    private List<ContactItem> LoadContactsFromExcel()
    {
        var excelPath = ResolveExcelPath();
        _logger.LogInformation("正在从 Excel 读取好友数据：{Path}", excelPath);

        var list = new List<ContactItem>();
        var seenWxids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var fs = new FileStream(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var sheetNames = MiniExcel.GetSheetNames(fs);

        var excludedKeywords = new[] { "E级", "删除", "拉黑", "僵尸", "全量", "概览" };

        foreach (var sheetName in sheetNames)
        {
            // 严格排除 E 级和非意向分级
            if (excludedKeywords.Any(k => sheetName.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogInformation("跳过已排除 Sheet: {SheetName}", sheetName);
                continue;
            }

            // 识别评级字母：S, A, B, C, D
            string tier = "C";
            if (sheetName.StartsWith("S", StringComparison.OrdinalIgnoreCase)) tier = "S";
            else if (sheetName.StartsWith("A", StringComparison.OrdinalIgnoreCase)) tier = "A";
            else if (sheetName.StartsWith("B", StringComparison.OrdinalIgnoreCase)) tier = "B";
            else if (sheetName.StartsWith("C", StringComparison.OrdinalIgnoreCase)) tier = "C";
            else if (sheetName.StartsWith("D", StringComparison.OrdinalIgnoreCase)) tier = "D";

            fs.Position = 0;
            var rows = MiniExcel.Query(fs, useHeaderRow: true, sheetName: sheetName).ToList();

            foreach (IDictionary<string, object> row in rows)
            {
                var wxid = GetValue(row, "wxid", "A", "微信ID", "微信号");
                if (string.IsNullOrWhiteSpace(wxid) || wxid.Equals("wxid", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (seenWxids.Contains(wxid))
                {
                    continue;
                }
                seenWxids.Add(wxid);

                var name = GetValue(row, "姓名/称呼", "B", "姓名", "称呼");
                var nick = GetValue(row, "微信昵称", "C", "昵称");
                var remark = GetValue(row, "完整备注", "D", "备注");

                if (string.IsNullOrWhiteSpace(name))
                {
                    name = !string.IsNullOrWhiteSpace(nick) ? nick : (!string.IsNullOrWhiteSpace(remark) ? remark : wxid);
                }

                // 生成稳定唯一的头像种子
                var seed = Math.Abs(wxid.GetHashCode()) % 70 + 1;
                var avatarUrl = $"https://i.pravatar.cc/150?img={seed}";
                var coverUrl = $"https://picsum.photos/seed/{seed + 100}/800/260";

                list.Add(new ContactItem
                {
                    Wxid = wxid,
                    Name = name,
                    Nickname = nick,
                    Remark = remark,
                    Tier = tier,
                    TierLabel = sheetName,
                    Status = "PENDING",
                    Avatar = avatarUrl,
                    Cover = coverUrl,
                    LastActive = "近期互动"
                });
            }
        }

        _logger.LogInformation("Excel 解析完毕，有效意向好友总数（已剔除E级僵尸粉）：{Count}", list.Count);
        return list;
    }

    private List<ContactItem> EnsureCache()
    {
        if (_cachedContacts == null)
        {
            lock (_lock)
            {
                _cachedContacts ??= LoadContactsFromExcel();
            }
        }
        return _cachedContacts;
    }

    public void ReloadCache()
    {
        lock (_lock)
        {
            _cachedContacts = null;
        }
        EnsureCache();
    }

    public async Task<List<ContactItem>> GetContactsAsync(string? tier = null, string? search = null, string? status = null)
    {
        var all = EnsureCache();
        var statuses = await _storageService.GetAllStatusesAsync();

        var query = all.Select(c =>
        {
            var s = statuses.TryGetValue(c.Wxid, out var st) ? st : "PENDING";
            return new ContactItem
            {
                Wxid = c.Wxid,
                Name = c.Name,
                Nickname = c.Nickname,
                Remark = c.Remark,
                Tier = c.Tier,
                TierLabel = c.TierLabel,
                Status = s,
                Avatar = c.Avatar,
                Cover = c.Cover,
                LastActive = c.LastActive
            };
        });

        if (!string.IsNullOrWhiteSpace(tier) && !tier.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(c => string.Equals(c.Tier, tier, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(c => string.Equals(c.Status, status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim();
            query = query.Where(c => 
                c.Name.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                c.Remark.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                c.Nickname.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                c.Wxid.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }

        return query.ToList();
    }

    public async Task<ContactItem?> GetContactByWxidAsync(string wxid)
    {
        var all = EnsureCache();
        var found = all.FirstOrDefault(c => string.Equals(c.Wxid, wxid, StringComparison.OrdinalIgnoreCase));
        if (found == null) return null;

        var status = await _storageService.GetStatusForWxidAsync(wxid);
        return new ContactItem
        {
            Wxid = found.Wxid,
            Name = found.Name,
            Nickname = found.Nickname,
            Remark = found.Remark,
            Tier = found.Tier,
            TierLabel = found.TierLabel,
            Status = status,
            Avatar = found.Avatar,
            Cover = found.Cover,
            LastActive = found.LastActive
        };
    }

    public Task<Dictionary<string, int>> GetTierCountsAsync()
    {
        var all = EnsureCache();
        var counts = all.GroupBy(c => c.Tier)
                        .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(counts);
    }

    public Task<int> GetTotalCountAsync()
    {
        return Task.FromResult(EnsureCache().Count);
    }

    private static string GetValue(IDictionary<string, object> row, params string[] possibleKeys)
    {
        foreach (var key in possibleKeys)
        {
            var match = row.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));
            if (match.Key != null && match.Value != null)
            {
                return match.Value.ToString()?.Trim() ?? string.Empty;
            }
        }
        return string.Empty;
    }
}
