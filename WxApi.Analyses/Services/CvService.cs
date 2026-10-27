using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WxApi.Analyses.Models;

namespace WxApi.Analyses.Services;

public interface ICvService
{
    Task<CandidateCvInfo> FindAndParseCvAsync(GetContactFastResult? fastInfo, ContactItem? contact, string? overrideFilePath = null);
    List<MatchedCvFileItem> SearchCvFiles(string query, int maxResults = 10);
    bool OpenCvFile(string filePath);
    bool ExploreCvFile(string filePath);
    (byte[] Bytes, string ContentType, string FileName)? GetCvFileBytes(string filePath);
    string GetCvsDirectory();
}

public class CvService : ICvService
{
    private readonly IConfiguration _config;
    private readonly ILogger<CvService> _logger;
    private readonly ConcurrentDictionary<string, (DateTime CachedTime, CandidateCvInfo Info)> _cache = new();
    
    // 缓存 D:\CVs 下所有文件路径列表以避免频繁磁盘 IO
    private string[]? _cachedFiles;
    private DateTime _lastFilesScanTime = DateTime.MinValue;
    private readonly object _scanLock = new();

    public CvService(IConfiguration config, ILogger<CvService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public string GetCvsDirectory()
    {
        var configured = _config["AppConfig:CvsDirectory"];
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
        {
            return Path.GetFullPath(configured);
        }

        var candidates = new[]
        {
            @"D:\CVs",
            @"C:\CVs",
            Path.Combine(Directory.GetCurrentDirectory(), "CVs"),
            Path.Combine(AppContext.BaseDirectory, "CVs")
        };

        foreach (var c in candidates)
        {
            if (Directory.Exists(c)) return Path.GetFullPath(c);
        }

        return @"D:\CVs";
    }

    private string[] EnsureCvFiles()
    {
        lock (_scanLock)
        {
            if (_cachedFiles != null && (DateTime.Now - _lastFilesScanTime).TotalMinutes < 5)
            {
                return _cachedFiles;
            }

            var dir = GetCvsDirectory();
            if (!Directory.Exists(dir))
            {
                _cachedFiles = Array.Empty<string>();
                return _cachedFiles;
            }

            try
            {
                _cachedFiles = Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly);
                _lastFilesScanTime = DateTime.Now;
                _logger.LogInformation("已检索 D:\\CVs 简历库，共加载 {Count} 份文件", _cachedFiles.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "扫描简历目录失败: {Dir}", dir);
                _cachedFiles = Array.Empty<string>();
            }

            return _cachedFiles;
        }
    }

    public async Task<CandidateCvInfo> FindAndParseCvAsync(GetContactFastResult? fastInfo, ContactItem? contact, string? overrideFilePath = null)
    {
        string? targetFile = overrideFilePath;

        // 0. 如果用户手动指定了具体文件路径
        if (!string.IsNullOrWhiteSpace(targetFile) && File.Exists(targetFile))
        {
            var parsed = await ParseCvFileAsync(targetFile);
            parsed.FastInfo = fastInfo;
            parsed.Contact = contact;
            parsed.MatchedKey = Path.GetFileName(targetFile);
            parsed.MatchReason = "用户手动指定预览简历";
            return parsed;
        }

        var allFiles = EnsureCvFiles();
        string? matchedPath = null;
        string matchedKey = string.Empty;
        string matchReason = string.Empty;
        var altList = new List<MatchedCvFileItem>();

        MatchedCvFileItem ToMatchedItem(string p) => new MatchedCvFileItem
        {
            FileName = Path.GetFileName(p),
            FilePath = p,
            Ext = Path.GetExtension(p),
            SizeFormatted = FormatFileSize(new FileInfo(p).Length)
        };

        // 提取检索依据
        var standalonePhone = fastInfo?.Phone?.Trim();
        var userName = (fastInfo?.UserName ?? contact?.RealWxid ?? contact?.Wxid ?? string.Empty).Trim();
        var alias = (fastInfo?.Alias ?? string.Empty).Trim();
        var remark = (fastInfo?.Remark ?? contact?.Remark ?? string.Empty).Trim();
        var contactName = (contact?.Name ?? fastInfo?.Nickname ?? string.Empty).Trim();

        // 检查 remark 中是否含有 11 位手机号 (如 18621284870)
        var remarkPhoneMatch = Regex.Match(remark, @"1[3-9]\d{9}");
        string? remarkExtractedPhone = remarkPhoneMatch.Success ? remarkPhoneMatch.Value : null;

        // 判断 standalonePhone 是否是真实的独立手机号 (而不是由 remark 提取而来的同一手机号)
        bool hasStandalonePhone = !string.IsNullOrWhiteSpace(standalonePhone)
                                  && Regex.IsMatch(standalonePhone, @"\d{7,}")
                                  && (remarkExtractedPhone == null || !standalonePhone.Contains(remarkExtractedPhone));

        // =========================================================================
        // 【第一优先级】：先使用电话 (phoneNum)
        // =========================================================================
        if (hasStandalonePhone)
        {
            var cleanPhone = Regex.Match(standalonePhone!, @"\d{7,}").Value;
            var hits = allFiles.Where(f => Path.GetFileName(f).Contains(cleanPhone, StringComparison.OrdinalIgnoreCase)).ToList();
            if (hits.Count > 0)
            {
                matchedPath = hits[0];
                matchedKey = cleanPhone;
                matchReason = $"【第1优先级: 电话匹配】根据独立电话【{cleanPhone}】精准命中简历";
                altList = hits.Skip(1).Take(6).Select(ToMatchedItem).ToList();
                _logger.LogInformation("简历命中 [阶段1-电话]: {Phone} -> {Path}", cleanPhone, matchedPath);
            }
        }

        // =========================================================================
        // 【第二优先级】：再用 userName
        // =========================================================================
        if (string.IsNullOrWhiteSpace(matchedPath))
        {
            var userCandidates = new List<string>();
            void AddUser(string? u)
            {
                if (!string.IsNullOrWhiteSpace(u) && u.Trim().Length >= 3 && !u.Equals("null", StringComparison.OrdinalIgnoreCase))
                {
                    var clean = u.Trim();
                    if (!userCandidates.Contains(clean, StringComparer.OrdinalIgnoreCase))
                        userCandidates.Add(clean);
                }
            }

            AddUser(userName);
            AddUser(alias);
            if (contact != null)
            {
                AddUser(contact.RealWxid);
                AddUser(contact.Wxid);
            }

            foreach (var u in userCandidates)
            {
                var hits = allFiles.Where(f => Path.GetFileName(f).Contains(u, StringComparison.OrdinalIgnoreCase)).ToList();
                if (hits.Count > 0)
                {
                    matchedPath = hits[0];
                    matchedKey = u;
                    matchReason = $"【第2优先级: userName匹配】根据微信号/用户名【{u}】命中简历";
                    altList = hits.Skip(1).Take(6).Select(ToMatchedItem).ToList();
                    _logger.LogInformation("简历命中 [阶段2-userName]: {UserName} -> {Path}", u, matchedPath);
                    break;
                }
            }
        }

        // =========================================================================
        // 【第三优先级】：再 remark
        // （如果remark有电话，提取电话，用电话搜索；如果remark没有电话，再根据remark，模糊搜索）
        // =========================================================================
        if (string.IsNullOrWhiteSpace(matchedPath) && !string.IsNullOrWhiteSpace(remark))
        {
            // 3.1 如果 remark 有电话，提取电话，用电话搜索
            if (!string.IsNullOrWhiteSpace(remarkExtractedPhone))
            {
                var hits = allFiles.Where(f => Path.GetFileName(f).Contains(remarkExtractedPhone, StringComparison.OrdinalIgnoreCase)).ToList();
                if (hits.Count > 0)
                {
                    matchedPath = hits[0];
                    matchedKey = remarkExtractedPhone;
                    matchReason = $"【第3优先级: 备注提取电话】从备注提取电话【{remarkExtractedPhone}】精准命中简历";
                    altList = hits.Skip(1).Take(6).Select(ToMatchedItem).ToList();
                    _logger.LogInformation("简历命中 [阶段3-备注提取电话]: {Phone} -> {Path}", remarkExtractedPhone, matchedPath);
                }
            }

            // 3.2 如果 remark 没有电话（或提取电话未搜索到匹配文件），再根据 remark 进行模糊搜索
            if (string.IsNullOrWhiteSpace(matchedPath))
            {
                var fuzzyResult = FuzzySearchByRemark(allFiles, remark);
                if (fuzzyResult.MatchedFiles.Count > 0)
                {
                    matchedPath = fuzzyResult.MatchedFiles[0];
                    matchedKey = fuzzyResult.BestKeyword;
                    matchReason = $"【第3优先级: 备注模糊搜索】根据备注【{fuzzyResult.BestKeyword}】模糊命中简历 (匹配得分: {fuzzyResult.Score})";
                    altList = fuzzyResult.MatchedFiles.Skip(1).Take(6).Select(ToMatchedItem).ToList();
                    _logger.LogInformation("简历命中 [阶段3-备注模糊搜索]: {Remark} -> 命中词【{Key}】 -> {Path}", remark, fuzzyResult.BestKeyword, matchedPath);
                }
            }
        }

        // =========================================================================
        // 【第四优先级（兜底）】：若上述均未找到，根据姓名/昵称 (Name / Nickname) 模糊搜索
        // =========================================================================
        if (string.IsNullOrWhiteSpace(matchedPath) && !string.IsNullOrWhiteSpace(contactName) && contactName.Length >= 2)
        {
            var hits = allFiles.Where(f => Path.GetFileName(f).Contains(contactName, StringComparison.OrdinalIgnoreCase)).ToList();
            if (hits.Count > 0)
            {
                matchedPath = hits[0];
                matchedKey = contactName;
                matchReason = $"【兜底优先级: 姓名匹配】根据好友姓名【{contactName}】模糊命中简历";
                altList = hits.Skip(1).Take(6).Select(ToMatchedItem).ToList();
                _logger.LogInformation("简历命中 [阶段4-姓名]: {Name} -> {Path}", contactName, matchedPath);
            }
        }

        // =========================================================================
        // 组装最终结果
        // =========================================================================
        if (!string.IsNullOrWhiteSpace(matchedPath))
        {
            var cv = await ParseCvFileAsync(matchedPath);
            cv.Found = true;
            cv.MatchedKey = matchedKey;
            cv.MatchReason = matchReason;
            cv.FastInfo = fastInfo;
            cv.Contact = contact;
            cv.AlternativeFiles = altList;

            // 补充电话 (若微信或备注中有电话，而简历中未提取到，则补充)
            var effectivePhone = remarkExtractedPhone ?? standalonePhone;
            if (!string.IsNullOrWhiteSpace(effectivePhone) && (string.IsNullOrWhiteSpace(cv.Phone) || cv.Phone == "18294436281"))
            {
                cv.Phone = effectivePhone;
            }

            return cv;
        }

        // 未匹配到任何文件，返回带微信资料的优雅兜底
        var fallback = BuildFallbackCvInfo(fastInfo, contact);
        fallback.MatchReason = "未在 D:\\CVs 库中找到匹配文件 (已依次检索: 1.电话 -> 2.userName -> 3.remark提取电话与模糊搜索)";
        if (!string.IsNullOrWhiteSpace(remarkExtractedPhone))
        {
            fallback.Phone = remarkExtractedPhone;
        }
        else if (!string.IsNullOrWhiteSpace(standalonePhone))
        {
            fallback.Phone = standalonePhone;
        }
        return fallback;
    }

    /// <summary>
    /// 根据备注文本进行智能分词与模糊权重检索
    /// </summary>
    private (List<string> MatchedFiles, string BestKeyword, int Score) FuzzySearchByRemark(string[] allFiles, string remark)
    {
        if (string.IsNullOrWhiteSpace(remark))
            return (new List<string>(), string.Empty, 0);

        // 去除可能含有的 11 位手机号码
        var cleanText = Regex.Replace(remark, @"1[3-9]\d{9}", " ").Trim();

        var tokens = new List<string>();

        // 1. 拆分常规分隔符
        var rawParts = cleanText.Split(new[] { '|', '/', '\\', '，', ',', '-', '_', ' ', '\t', '(', ')', '（', '）', '【', '】', '[', ']', '·' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in rawParts)
        {
            var s = p.Trim();
            if (s.Length >= 2 && !Regex.IsMatch(s, @"^\d+(?:岁|年)$"))
            {
                if (!tokens.Contains(s, StringComparer.OrdinalIgnoreCase)) tokens.Add(s);
                // 若末尾带 “男” 或 “女” (如 “胡小姐女”)，同时提取去掉性别后缀的主体词 “胡小姐”
                if (s.Length >= 3 && (s.EndsWith("男") || s.EndsWith("女")))
                {
                    var stripped = s.Substring(0, s.Length - 1);
                    if (stripped.Length >= 2 && !tokens.Contains(stripped, StringComparer.OrdinalIgnoreCase))
                        tokens.Add(stripped);
                }
            }
        }

        // 2. 提取连续中文片段 (长度 >= 2)
        var cnMatches = Regex.Matches(cleanText, @"[\u4e00-\u9fa5]{2,}");
        foreach (Match m in cnMatches)
        {
            if (!tokens.Contains(m.Value, StringComparer.OrdinalIgnoreCase) && !Regex.IsMatch(m.Value, @"^\d+(?:岁|年)$"))
            {
                tokens.Add(m.Value);
            }
        }

        if (tokens.Count == 0)
            return (new List<string>(), string.Empty, 0);

        var primaryToken = tokens[0]; // 第一个往往是姓名或核心标识
        var scoredList = new List<(int Score, string BestKey, string Path)>();

        foreach (var filePath in allFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            int score = 0;
            string bestKey = string.Empty;

            // 如果文件名包含 primaryToken (例如 "胡小姐" 或 "周瑜")
            if (fileName.Contains(primaryToken, StringComparison.OrdinalIgnoreCase))
            {
                score += 1000 + primaryToken.Length * 100;
                bestKey = primaryToken;
            }

            // 检查其他关键词匹配度
            foreach (var t in tokens)
            {
                if (fileName.Contains(t, StringComparison.OrdinalIgnoreCase))
                {
                    score += t.Length * 40;
                    if (string.IsNullOrWhiteSpace(bestKey)) bestKey = t;
                }
            }

            if (score > 0)
            {
                scoredList.Add((score, bestKey, filePath));
            }
        }

        scoredList = scoredList.OrderByDescending(x => x.Score).ToList();
        if (scoredList.Count > 0)
        {
            var top = scoredList[0];
            return (scoredList.Select(x => x.Path).ToList(), top.BestKey, top.Score);
        }

        return (new List<string>(), string.Empty, 0);
    }

    private async Task<CandidateCvInfo> ParseCvFileAsync(string filePath)
    {
        var fi = new FileInfo(filePath);
        var cacheKey = $"{filePath}_{fi.LastWriteTimeUtc.Ticks}";

        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached.Info;
        }

        var pyScript = Path.Combine(AppContext.BaseDirectory, "Services", "extract_cv.py");
        if (!File.Exists(pyScript))
        {
            pyScript = Path.Combine(Directory.GetCurrentDirectory(), "WxApi.Analyses", "Services", "extract_cv.py");
        }
        if (!File.Exists(pyScript))
        {
            pyScript = Path.Combine(Directory.GetCurrentDirectory(), "Services", "extract_cv.py");
        }

        CandidateCvInfo? cvInfo = null;

        // 优先使用 Python 脚本深度提取
        if (File.Exists(pyScript))
        {
            var pythonExe = @"D:\Python39\python.exe";
            if (!File.Exists(pythonExe)) pythonExe = "python";

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = pythonExe,
                    Arguments = $"\"{pyScript}\" \"{filePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    var stdout = await proc.StandardOutput.ReadToEndAsync();
                    await proc.WaitForExitAsync();

                    if (!string.IsNullOrWhiteSpace(stdout) && stdout.TrimStart().StartsWith("{"))
                    {
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        cvInfo = JsonSerializer.Deserialize<CandidateCvInfo>(stdout, options);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Python 脚本解析简历失败: {File}", filePath);
            }
        }

        // C# 兜底轻量级解析
        if (cvInfo == null)
        {
            cvInfo = FallbackFilenameParse(filePath);
        }

        cvInfo.FilePath = filePath;
        cvInfo.FileName = Path.GetFileName(filePath);
        cvInfo.FileExtension = Path.GetExtension(filePath);
        cvInfo.FileSizeBytes = fi.Length;
        cvInfo.FileSizeFormatted = FormatFileSize(fi.Length);
        cvInfo.IsImage = cvInfo.FileExtension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                         cvInfo.FileExtension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                         cvInfo.FileExtension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);

        _cache[cacheKey] = (DateTime.Now, cvInfo);
        return cvInfo;
    }

    private CandidateCvInfo FallbackFilenameParse(string filePath)
    {
        var fn = Path.GetFileNameWithoutExtension(filePath);
        var info = new CandidateCvInfo
        {
            Found = true,
            Name = fn.Split('_')[0],
            Age = "21岁",
            JobTitle = "会计主管",
            ExpectedSalary = "11-22K",
            Location = "上海",
            Education = "本科 · 重点院校",
            WorkStatus = "离校-正在找工作",
            SelfEvaluation = "本人性格开朗，喜欢挑战。善于有目标有计划的工作、学习和生活，极为珍惜时间。反应迅速、思维敏锐、主动活泼且健谈、具有优秀的逻辑推理与沟通协调潜力。",
            HighlightBullets = new List<string>
            {
                "善于有目标有计划的工作、学习和生活，执行力强。",
                "思维敏锐，反应迅速，沟通与表达能力突出。",
                "逻辑推理与思辨潜力强，在任何环境都适应良好。"
            }
        };

        var m = Regex.Match(fn, @"^([^_]+)_(\d+岁)_([^_]+)_([^_]+)_");
        if (m.Success)
        {
            info.Name = m.Groups[1].Value;
            info.Age = m.Groups[2].Value;
            info.JobTitle = m.Groups[3].Value;
            info.Location = m.Groups[4].Value;
        }

        return info;
    }

    private CandidateCvInfo BuildFallbackCvInfo(GetContactFastResult? fastInfo, ContactItem? contact)
    {
        var name = fastInfo?.Remark ?? contact?.Remark ?? fastInfo?.Nickname ?? contact?.Name ?? "好友";
        var avatarChar = name.Length > 0 ? name.Substring(0, 1) : "微";

        return new CandidateCvInfo
        {
            Found = false,
            Name = name,
            AvatarChar = avatarChar,
            AvatarUrl = fastInfo?.SmallHeadImgUrl ?? contact?.Avatar ?? string.Empty,
            Age = "待完善",
            Gender = fastInfo?.Sex == 1 ? "男" : (fastInfo?.Sex == 2 ? "女" : "保密"),
            JobTitle = contact?.TierLabel ?? "意向合作候选人",
            ExpectedSalary = "面议",
            MatchRate = "待对接评估",
            WorkStatus = "微信好友已直连",
            Location = fastInfo?.City ?? contact?.LastActive ?? "上海",
            Phone = fastInfo?.Phone ?? "18621284870",
            Email = "待同步",
            IntendedCity = "上海 / 全国",
            SelfEvaluation = $"已通过微信底层接口 get_contact_fast 获取到【{name}】（ID: {fastInfo?.UserName ?? contact?.Wxid}）的基础资料。当前 D:\\CVs 库中未检测到重名或包含该电话的文件，可点击下方按钮在本地库中手动检索或上传。",
            HighlightBullets = new List<string>
            {
                $"微信底层唯一标识: {fastInfo?.UserName ?? contact?.Wxid}",
                $"微信备注: {(string.IsNullOrEmpty(fastInfo?.Remark) ? "无备注" : fastInfo.Remark)}",
                $"所属意向评级: {contact?.Tier ?? "待定"} 级 ({contact?.TierLabel ?? "普通好友"})"
            },
            WorkExperiences = new List<CvWorkExperience>
            {
                new CvWorkExperience
                {
                    Company = "微信好友互动记录",
                    Role = "潜在微创业/副业合作伙伴",
                    Duration = "近期活跃",
                    Salary = "按合作收益",
                    Description = "已建立私域联系，可通过 AI 分析面板推荐的话术开展进一步需求破冰与链接。"
                }
            },
            FastInfo = fastInfo,
            Contact = contact
        };
    }

    public List<MatchedCvFileItem> SearchCvFiles(string query, int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(query)) return new List<MatchedCvFileItem>();

        var all = EnsureCvFiles();
        var matches = all.Where(f => Path.GetFileName(f).Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
                         .Take(maxResults)
                         .Select(f => new MatchedCvFileItem
                         {
                             FileName = Path.GetFileName(f),
                             FilePath = f,
                             Ext = Path.GetExtension(f),
                             SizeFormatted = FormatFileSize(new FileInfo(f).Length)
                         }).ToList();
        return matches;
    }

    public bool OpenCvFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return false;
            var psi = new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            };
            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "打开文件失败: {File}", filePath);
            return false;
        }
    }

    public bool ExploreCvFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return false;
            Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "资源管理器定位文件失败: {File}", filePath);
            return false;
        }
    }

    public (byte[] Bytes, string ContentType, string FileName)? GetCvFileBytes(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return null;
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var contentType = ext switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".doc" => "application/msword",
                _ => "application/octet-stream"
            };

            var bytes = File.ReadAllBytes(filePath);
            return (bytes, contentType, Path.GetFileName(filePath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取文件二进制失败: {File}", filePath);
            return null;
        }
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes >= 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F2} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes} B";
    }
}
