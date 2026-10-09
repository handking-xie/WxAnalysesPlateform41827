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
    void ReloadCache(string? fileNameOrPath = null);
    List<ExcelFileInfo> GetAvailableExcelFiles();
    string GetCurrentExcelFilePath();
    string GetCurrentAccountKey();
    void SwitchExcelFile(string fileNameOrPath);
    string GetWxDatasDirectory();
}

public class ContactService : IContactService
{
    private readonly Microsoft.Extensions.Configuration.IConfiguration _config;
    private readonly IStorageService _storageService;
    private readonly ILogger<ContactService> _logger;
    private List<ContactItem>? _cachedContacts;
    private string? _currentExcelPath;
    private readonly object _lock = new();

    public ContactService(Microsoft.Extensions.Configuration.IConfiguration config, IStorageService storageService, ILogger<ContactService> logger)
    {
        _config = config;
        _storageService = storageService;
        _logger = logger;
    }

    public string GetWxDatasDirectory()
    {
        var candidates = new List<string>();

        var configured = _config["AppConfig:WxDatasPath"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            candidates.Add(configured);
        }

        candidates.Add(@"D:\WxAnalysesPlateform41827\WxDatas");
        candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "WxDatas"));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "WxDatas"));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "WxDatas"));
        candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "..", "WxDatas"));

        foreach (var dir in candidates)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    return Path.GetFullPath(dir);
                }
            }
            catch { }
        }

        var defaultDir = @"D:\WxAnalysesPlateform41827\WxDatas";
        try
        {
            if (!Directory.Exists(defaultDir)) Directory.CreateDirectory(defaultDir);
        }
        catch { }
        return defaultDir;
    }

    public List<ExcelFileInfo> GetAvailableExcelFiles()
    {
        var dir = GetWxDatasDirectory();
        var result = new List<ExcelFileInfo>();

        if (Directory.Exists(dir))
        {
            var files = Directory.GetFiles(dir, "*.*")
                .Where(f => (f.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
                         && !Path.GetFileName(f).StartsWith("~$"))
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .ToList();

            var currentPath = _currentExcelPath ?? ResolveExcelPathSilently();

            foreach (var file in files)
            {
                var fi = new FileInfo(file);
                var isCurr = !string.IsNullOrEmpty(currentPath) &&
                    string.Equals(Path.GetFullPath(file), Path.GetFullPath(currentPath), StringComparison.OrdinalIgnoreCase);

                result.Add(new ExcelFileInfo
                {
                    FileName = fi.Name,
                    FilePath = fi.FullName,
                    FileSize = fi.Length,
                    FileSizeFormatted = FormatFileSize(fi.Length),
                    LastModified = fi.LastWriteTime,
                    IsCurrent = isCurr
                });
            }
        }

        if (result.Count > 0 && !result.Any(r => r.IsCurrent))
        {
            result[0].IsCurrent = true;
        }

        return result;
    }

    public string GetCurrentExcelFilePath()
    {
        return ResolveExcelPath();
    }

    public string GetCurrentAccountKey()
    {
        return ExtractAccountKey(GetCurrentExcelFilePath());
    }

    public static string ExtractAccountKey(string fileNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(fileNameOrPath)) return string.Empty;
        var name = Path.GetFileNameWithoutExtension(fileNameOrPath);
        var parts = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            if (int.TryParse(p, out _)) continue;
            return p.Trim();
        }
        return name.Trim();
    }

    public void SwitchExcelFile(string fileNameOrPath)
    {
        lock (_lock)
        {
            string targetPath = fileNameOrPath;
            var dir = GetWxDatasDirectory();

            if (!File.Exists(targetPath))
            {
                var candidate = Path.Combine(dir, fileNameOrPath);
                if (File.Exists(candidate))
                {
                    targetPath = candidate;
                }
            }

            if (!File.Exists(targetPath) && Directory.Exists(dir))
            {
                var files = Directory.GetFiles(dir, "*.*")
                    .Where(f => (f.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
                             && !Path.GetFileName(f).StartsWith("~$"))
                    .ToList();

                var cleanName = Path.GetFileName(fileNameOrPath).Trim();
                var prefix = cleanName.Split('_')[0].Trim();

                var matched = files.FirstOrDefault(f => string.Equals(Path.GetFileName(f), cleanName, StringComparison.OrdinalIgnoreCase))
                           ?? files.FirstOrDefault(f => !string.IsNullOrEmpty(prefix) && Path.GetFileName(f).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                           ?? files.FirstOrDefault(f => Path.GetFileName(f).Contains(cleanName, StringComparison.OrdinalIgnoreCase));

                if (matched != null)
                {
                    targetPath = matched;
                }
            }

            if (!File.Exists(targetPath))
            {
                throw new FileNotFoundException($"找不到指定的 Excel 文件：{fileNameOrPath}");
            }

            _currentExcelPath = Path.GetFullPath(targetPath);
            _cachedContacts = null;
        }
        EnsureCache();
    }

    private string? ResolveExcelPathSilently()
    {
        try
        {
            return ResolveExcelPath();
        }
        catch
        {
            return null;
        }
    }

    private string ResolveExcelPath()
    {
        if (!string.IsNullOrWhiteSpace(_currentExcelPath) && File.Exists(_currentExcelPath))
        {
            return _currentExcelPath;
        }

        // 1. 优先从 WxDatas 检索所有 Excel 文件并选取第一个
        var dir = GetWxDatasDirectory();
        if (Directory.Exists(dir))
        {
            var wxDatasFiles = Directory.GetFiles(dir, "*.*")
                .Where(f => (f.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
                         && !Path.GetFileName(f).StartsWith("~$"))
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .ToList();

            if (wxDatasFiles.Count > 0)
            {
                _currentExcelPath = Path.GetFullPath(wxDatasFiles[0]);
                return _currentExcelPath;
            }
        }

        // 2. 候选路径回退
        var configured = _config["AppConfig:ExcelPath"];
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(configured))
        {
            candidates.Add(configured);
            candidates.Add(Path.Combine(AppContext.BaseDirectory, configured));
            candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), configured));
        }

        candidates.Add(Path.Combine(@"D:\WxAnalysesPlateform41827\WxDatas", "aixian19920303_全部好友副业微创业意向全景透视表.xlsx"));
        candidates.Add(Path.Combine(@"D:\WxAnalysesPlateform41827\WxDatas", "samuelsurry_全部好友副业微创业意向全景透视表.xlsx"));
        candidates.Add("samuelsurry_全部好友副业微创业意向全景透视表.xlsx");
        candidates.Add("01_samuelsurry_全部好友副业微创业意向全景透视表.xlsx");
        candidates.Add(Path.Combine("..", "samuelsurry_全部好友副业微创业意向全景透视表.xlsx"));
        candidates.Add(Path.Combine("d:\\WxHookSource41827", "samuelsurry_全部好友副业微创业意向全景透视表.xlsx"));

        foreach (var c in candidates)
        {
            if (File.Exists(c))
            {
                _currentExcelPath = Path.GetFullPath(c);
                return _currentExcelPath;
            }
        }

        throw new FileNotFoundException($"在 {dir} 及候选路径中均未找到有效的 Excel 透视表文件");
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes >= 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F2} MB";
        if (bytes >= 1024)
            return $"{bytes / 1024.0:F1} KB";
        return $"{bytes} B";
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
            if (sheetName.StartsWith("S", StringComparison.OrdinalIgnoreCase) || sheetName.Contains("S级", StringComparison.OrdinalIgnoreCase)) tier = "S";
            else if (sheetName.StartsWith("A", StringComparison.OrdinalIgnoreCase) || sheetName.Contains("A级", StringComparison.OrdinalIgnoreCase)) tier = "A";
            else if (sheetName.StartsWith("B", StringComparison.OrdinalIgnoreCase) || sheetName.Contains("B级", StringComparison.OrdinalIgnoreCase)) tier = "B";
            else if (sheetName.StartsWith("C", StringComparison.OrdinalIgnoreCase) || sheetName.Contains("C级", StringComparison.OrdinalIgnoreCase)) tier = "C";
            else if (sheetName.StartsWith("D", StringComparison.OrdinalIgnoreCase) || sheetName.Contains("D级", StringComparison.OrdinalIgnoreCase)) tier = "D";

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

    public void ReloadCache(string? fileNameOrPath = null)
    {
        lock (_lock)
        {
            if (!string.IsNullOrWhiteSpace(fileNameOrPath))
            {
                SwitchExcelFile(fileNameOrPath);
                return;
            }
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
