using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MiniExcelLibs;
using WxApi.SetLabel.Config;
using WxApi.SetLabel.Models;

namespace WxApi.SetLabel.Services;

public class ExcelReaderService
{
    private readonly AppConfig _config;

    public ExcelReaderService(AppConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// 自动定位 Excel 文件路径（优先从当前运行目录检索）
    /// </summary>
    public string FindExcelFile(string? specifiedPath = null)
    {
        if (!string.IsNullOrWhiteSpace(specifiedPath) && File.Exists(specifiedPath))
        {
            return Path.GetFullPath(specifiedPath);
        }

        var currentDir = Directory.GetCurrentDirectory();
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        var candidates = new List<string>
        {
            _config.ExcelFileName,
            "surry_全部好友副业微创业意向全景透视表.xlsx",
            "01_samuelsurry_全部好友副业微创业意向全景透视表.xlsx"
        }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // 1. 优先检查当前运行目录 (精确匹配)
        foreach (var name in candidates)
        {
            var p = Path.Combine(currentDir, name);
            if (File.Exists(p)) return Path.GetFullPath(p);
        }

        // 2. 当前运行目录模糊匹配
        var currentMatched = Directory.GetFiles(currentDir, "*微创业意向全景透视表*.xlsx").FirstOrDefault()
                             ?? Directory.GetFiles(currentDir, "*surry*.xlsx").FirstOrDefault();
        if (currentMatched != null && File.Exists(currentMatched)) return Path.GetFullPath(currentMatched);

        // 3. 检查程序执行目录 (BaseDirectory)
        foreach (var name in candidates)
        {
            var p = Path.Combine(baseDir, name);
            if (File.Exists(p)) return Path.GetFullPath(p);
        }

        var baseMatched = Directory.GetFiles(baseDir, "*微创业意向全景透视表*.xlsx").FirstOrDefault()
                          ?? Directory.GetFiles(baseDir, "*surry*.xlsx").FirstOrDefault();
        if (baseMatched != null && File.Exists(baseMatched)) return Path.GetFullPath(baseMatched);

        // 4. 回退上级目录查找
        var parentDir = Path.GetFullPath(Path.Combine(currentDir, ".."));
        foreach (var name in candidates)
        {
            var p = Path.Combine(parentDir, name);
            if (File.Exists(p)) return Path.GetFullPath(p);
        }

        var parentMatched = Directory.GetFiles(parentDir, "*微创业意向全景透视表*.xlsx").FirstOrDefault()
                            ?? Directory.GetFiles(parentDir, "*surry*.xlsx").FirstOrDefault();
        if (parentMatched != null && File.Exists(parentMatched)) return Path.GetFullPath(parentMatched);

        throw new FileNotFoundException($"未能找到 Excel 透视表文件: {_config.ExcelFileName}。请确认文件已放置在当前运行目录: {currentDir}");
    }

    /// <summary>
    /// 读取 Excel 文件并解析出每个 Sheet 的好友列表
    /// </summary>
    public List<SheetLabelPlan> LoadPlans(string excelPath, bool includeOverviewSheet = false)
    {
        var plans = new List<SheetLabelPlan>();

        using var fs = new FileStream(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var sheetNames = MiniExcel.GetSheetNames(fs);

        foreach (var sheetName in sheetNames)
        {
            bool isOverview = sheetName.Equals(_config.OverviewSheetName, StringComparison.OrdinalIgnoreCase)
                              || sheetName.Contains("全量");

            if (isOverview && !includeOverviewSheet)
            {
                continue;
            }

            fs.Position = 0;
            var rows = MiniExcel.Query(fs, useHeaderRow: true, sheetName: sheetName).ToList();

            var contacts = new List<ContactInfo>();
            var seenWxids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

                contacts.Add(new ContactInfo
                {
                    Wxid = wxid,
                    Name = GetValue(row, "姓名/称呼", "B", "姓名", "称呼"),
                    Nickname = GetValue(row, "微信昵称", "C", "昵称"),
                    Remark = GetValue(row, "完整备注", "D", "备注"),
                    IntentionGrade = GetValue(row, "意向评级", "M", "评级")
                });
            }

            plans.Add(new SheetLabelPlan
            {
                SheetName = sheetName,
                TargetLabelName = sheetName,
                Contacts = contacts
            });
        }

        return plans;
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
