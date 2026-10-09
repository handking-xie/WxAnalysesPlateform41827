using System;
using System.IO;
using System.Text.Json;

namespace WxApi.SetLabel.Config;

public class AppConfig
{
    public string ApiBaseUrl { get; set; } = "http://127.0.0.1:19088";
    public string ExcelFileName { get; set; } = "surry_全部好友副业微创业意向全景透视表.xlsx";
    public int RequestDelayMs { get; set; } = 5000;
    public int MaxRetryCount { get; set; } = 3;
    public bool SkipOverviewSheet { get; set; } = true;
    public string OverviewSheetName { get; set; } = "全量2529位好友分析";
    public bool SkipIfAlreadyLabeled { get; set; } = true;
    public bool AppendExistingLabels { get; set; } = true;

    public static AppConfig LoadOrCreate(string configPath = "appsettings.json")
    {
        try
        {
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (config != null) return config;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[警告] 读取配置文件 {configPath} 失败: {ex.Message}，将使用默认配置。");
        }

        var defaultConfig = new AppConfig();
        try
        {
            var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(configPath, json);
        }
        catch
        {
            // ignore
        }
        return defaultConfig;
    }
}
