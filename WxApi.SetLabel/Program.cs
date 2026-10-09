using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Spectre.Console;
using WxApi.SetLabel.Config;
using WxApi.SetLabel.Models;
using WxApi.SetLabel.Services;

namespace WxApi.SetLabel;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        AnsiConsole.Write(
            new FigletText("WxApi.SetLabel")
                .Color(Color.Green));

        AnsiConsole.MarkupLine("[bold grey]微信 4.x Hook 批量联系人标签同步工具 (WeChat 4.1.8.27)[/]\n");

        // 1. 加载配置
        var config = AppConfig.LoadOrCreate();

        // 2. 解析命令行参数
        bool isDryRun = args.Contains("--dry-run");
        bool autoConfirm = args.Contains("-y") || args.Contains("--yes") || args.Contains("--run");
        bool includeOverview = args.Contains("--all") || args.Contains("--include-overview");
        string? customExcelPath = GetArgValue(args, "--excel");
        string? customApiUrl = GetArgValue(args, "--api");
        string? targetSingleSheet = GetArgValue(args, "--sheet");
        string? delayStr = GetArgValue(args, "--delay");

        if (args.Contains("-h") || args.Contains("--help"))
        {
            PrintHelp();
            return;
        }

        if (!string.IsNullOrWhiteSpace(customApiUrl)) config.ApiBaseUrl = customApiUrl;
        if (int.TryParse(delayStr, out int delay)) config.RequestDelayMs = delay;

        // 3. 读取 Excel
        var excelService = new ExcelReaderService(config);
        string excelPath;
        try
        {
            excelPath = excelService.FindExcelFile(customExcelPath);
            AnsiConsole.MarkupLine($"[green]✔ 找到 Excel 文件:[/] [bold underline]{Markup.Escape(excelPath)}[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[bold red]✖ 错误: {Markup.Escape(ex.Message)}[/]");
            return;
        }

        List<SheetLabelPlan> allPlans;
        try
        {
            allPlans = excelService.LoadPlans(excelPath, includeOverview);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[bold red]✖ 解析 Excel 失败: {Markup.Escape(ex.Message)}[/]");
            return;
        }

        if (!string.IsNullOrWhiteSpace(targetSingleSheet))
        {
            allPlans = allPlans.Where(p => string.Equals(p.SheetName, targetSingleSheet, StringComparison.OrdinalIgnoreCase)).ToList();
            if (allPlans.Count == 0)
            {
                AnsiConsole.MarkupLine($"[bold red]✖ 未找到指定的 Sheet: 【{Markup.Escape(targetSingleSheet)}】[/]");
                return;
            }
        }

        // 显示读取结果
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold yellow]计划处理的透视表 Sheet 列表[/]")
            .AddColumn(new TableColumn("序号").Centered())
            .AddColumn(new TableColumn("Sheet / 标签名称"))
            .AddColumn(new TableColumn("好友数量").Centered())
            .AddColumn(new TableColumn("示例 wxid"));

        int idx = 1;
        foreach (var p in allPlans)
        {
            var sample = p.Contacts.Count > 0 ? p.Contacts[0].Wxid : "--";
            table.AddRow($"{idx++}", $"[bold cyan]{Markup.Escape(p.SheetName)}[/]", $"{p.Contacts.Count}", $"[grey]{Markup.Escape(sample)}[/]");
        }
        AnsiConsole.Write(table);

        int totalContacts = allPlans.Sum(p => p.Contacts.Count);
        AnsiConsole.MarkupLine($"\n共计发现 [bold green]{allPlans.Count}[/] 个分类标签，覆盖 [bold green]{totalContacts}[/] 位好友 (操作间隔: [bold yellow]{config.RequestDelayMs / 1000.0:F1} 秒[/])。");

        // 4. 检查微信 HTTP 接口连通性
        using var client = new WxApiClient(config.ApiBaseUrl);
        AnsiConsole.MarkupLine($"\n正在检查 Hook 接口服务: [bold cyan]{Markup.Escape(config.ApiBaseUrl)}[/] ...");

        var (healthy, healthMsg) = await client.CheckHealthAsync();
        if (!healthy)
        {
            AnsiConsole.MarkupLine($"[bold red]✖ 无法连接到微信 Hook 接口服务:[/] {Markup.Escape(healthMsg)}");
            AnsiConsole.MarkupLine("[yellow]提示: 请确保微信已登录并注入 libGLESv1.dll，且已启动本地 HTTP 服务 (19088端口)。[/]");

            if (!isDryRun)
            {
                if (SafeConfirm("当前微信服务未连接，是否以 [yellow]DryRun(模拟测试模式)[/] 预览执行流程？", defaultValue: true))
                {
                    isDryRun = true;
                }
                else
                {
                    AnsiConsole.MarkupLine("[grey]已取消执行。[/]");
                    return;
                }
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"[bold green]✔ 微信服务连接正常！[/] {Markup.Escape(healthMsg)}");
        }

        // 5. 用户确认
        if (!autoConfirm)
        {
            AnsiConsole.WriteLine();
            var prompt = isDryRun 
                ? "[yellow]即将进行【模拟测试 (DryRun)】，不会实际修改微信数据。是否继续？[/]"
                : $"[bold red]即将开始向微信批量创建标签并打标签 (每次操作间隔 {config.RequestDelayMs / 1000.0:F1} 秒)。是否确认执行？[/]";

            if (!SafeConfirm(prompt, defaultValue: !isDryRun))
            {
                AnsiConsole.MarkupLine("[grey]用户取消了操作。[/]");
                return;
            }
        }

        // 6. 执行核心逻辑
        var engine = new LabelSyncEngine(client, config);

        // 阶段一: 准备标签 (检查存在 / 创建标签)
        bool prepOk = await engine.PrepareLabelsAsync(allPlans, isDryRun);
        if (!prepOk)
        {
            AnsiConsole.MarkupLine("[bold red]标签准备阶段出现错误，已终止操作。[/]");
            return;
        }

        // 阶段二: 遍历好友设置标签
        await engine.ExecuteSyncAsync(allPlans, isDryRun);

        AnsiConsole.MarkupLine("\n[bold green]🎉 全部操作处理完成！[/]");
    }

    private static string? GetArgValue(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return null;
    }

    private static bool SafeConfirm(string prompt, bool defaultValue)
    {
        if (Console.IsInputRedirected)
        {
            AnsiConsole.MarkupLine($"{prompt} [grey](环境非交互式，采用默认选项: {(defaultValue ? "y" : "n")})[/]");
            return defaultValue;
        }

        try
        {
            return AnsiConsole.Confirm(prompt, defaultValue);
        }
        catch
        {
            Console.Write($"{prompt} (y/n) [默认 {(defaultValue ? "y" : "n")}]: ");
            var input = Console.ReadLine()?.Trim().ToLower();
            if (string.IsNullOrEmpty(input)) return defaultValue;
            return input == "y" || input == "yes";
        }
    }

    private static void PrintHelp()
    {
        AnsiConsole.MarkupLine(@"[bold]WxApi.SetLabel 命令行选项说明:[/]
  [green]--excel <path>[/]           指定 Excel 文件的完整路径
  [green]--api <url>[/]              指定微信 Hook HTTP 服务根地址 (默认: http://127.0.0.1:19088)
  [green]--delay <ms>[/]             每个好友设置标签的间隔毫秒数 (默认: 5000ms / 5秒)
  [green]--sheet <name>[/]           仅处理指定的某个 Sheet (例如: --sheet 'S级-强烈意向')
  [green]--all[/]                    包含'全量2529位好友分析'主表 (默认自动忽略概览总表)
  [green]--dry-run[/]                模拟运行模式 (不调用 modify_contact_label 接口)
  [green]-y, --run, --yes[/]         直接执行，无需手动回车确认
  [green]-h, --help[/]               查看本帮助信息
");
    }
}
