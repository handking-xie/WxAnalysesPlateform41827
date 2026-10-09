using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Spectre.Console;
using WxApi.SetLabel.Config;
using WxApi.SetLabel.Models;

namespace WxApi.SetLabel.Services;

public class LabelSyncEngine
{
    private readonly WxApiClient _apiClient;
    private readonly AppConfig _config;

    public LabelSyncEngine(WxApiClient apiClient, AppConfig config)
    {
        _apiClient = apiClient;
        _config = config;
    }

    /// <summary>
    /// 对比与创建标签，确保每个 Sheet 对应的微信标签都有有效的 LabelId
    /// </summary>
    public async Task<bool> PrepareLabelsAsync(List<SheetLabelPlan> plans, bool isDryRun = false)
    {
        AnsiConsole.MarkupLine("[bold cyan]正在从微信拉取当前所有已存在的标签列表...[/]");
        List<LabelPair> existingLabels;
        try
        {
            existingLabels = await _apiClient.GetLabelListsAsync();
            AnsiConsole.MarkupLine($"[green]✔ 成功拉取到 {existingLabels.Count} 个现有微信标签。[/]");
        }
        catch (Exception ex)
        {
            if (isDryRun)
            {
                AnsiConsole.MarkupLine($"[yellow]⚠ 无法连接微信接口 ({Markup.Escape(ex.Message)})，DryRun 模式将使用虚拟标签模拟。[/]");
                existingLabels = new List<LabelPair>();
            }
            else
            {
                AnsiConsole.MarkupLine($"[bold red]✖ 拉取微信现有标签失败: {Markup.Escape(ex.Message)}[/]");
                return false;
            }
        }

        var labelTable = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold yellow]标签对应与创建规划[/]")
            .AddColumn(new TableColumn("[bold]Sheet / 目标标签名[/]"))
            .AddColumn(new TableColumn("[bold]好友数量[/]").Centered())
            .AddColumn(new TableColumn("[bold]微信状态[/]").Centered())
            .AddColumn(new TableColumn("[bold]标签 ID[/]").Centered());

        foreach (var plan in plans)
        {
            var matched = existingLabels.FirstOrDefault(l => 
                string.Equals(l.LabelName, plan.TargetLabelName, StringComparison.OrdinalIgnoreCase));

            if (matched != null)
            {
                plan.LabelId = matched.LabelId;
                plan.LabelExisted = true;
                labelTable.AddRow(
                    $"[green]{Markup.Escape(plan.TargetLabelName)}[/]",
                    $"{plan.Contacts.Count}",
                    "[green]已存在[/]",
                    $"[cyan]{plan.LabelId}[/]"
                );
            }
            else
            {
                if (isDryRun)
                {
                    plan.LabelId = 9999;
                    plan.LabelExisted = false;
                    labelTable.AddRow(
                        $"[yellow]{Markup.Escape(plan.TargetLabelName)}[/]",
                        $"{plan.Contacts.Count}",
                        "[yellow]模拟创建[/]",
                        "[grey]预设 9999[/]"
                    );
                }
                else
                {
                    AnsiConsole.MarkupLine($"[yellow]标签 【{Markup.Escape(plan.TargetLabelName)}】 在微信中不存在，正在调用 /api/add_label 创建...[/]");
                    var (success, labelId, rawResp) = await _apiClient.AddLabelAsync(plan.TargetLabelName);
                    if (success && labelId.HasValue)
                    {
                        plan.LabelId = labelId.Value;
                        plan.LabelExisted = false;
                        labelTable.AddRow(
                            $"[green]{Markup.Escape(plan.TargetLabelName)}[/]",
                            $"{plan.Contacts.Count}",
                            "[bold green]新建成功[/]",
                            $"[bold cyan]{plan.LabelId}[/]"
                        );
                        AnsiConsole.MarkupLine($"[green]✔ 标签 【{Markup.Escape(plan.TargetLabelName)}】 创建成功，ID: {labelId.Value}[/]");

                        if (_config.RequestDelayMs > 0)
                        {
                            AnsiConsole.MarkupLine($"[grey]已向微信提交创建标签，等待 {_config.RequestDelayMs / 1000.0:F1} 秒后继续下一步...[/]");
                            await Task.Delay(_config.RequestDelayMs);
                        }
                    }
                    else
                    {
                        labelTable.AddRow(
                            $"[red]{Markup.Escape(plan.TargetLabelName)}[/]",
                            $"{plan.Contacts.Count}",
                            "[bold red]创建失败[/]",
                            "[red]--[/]"
                        );
                        AnsiConsole.MarkupLine($"[bold red]✖ 标签 【{Markup.Escape(plan.TargetLabelName)}】 创建失败: {Markup.Escape(rawResp)}[/]");
                        return false;
                    }
                }
            }
        }

        AnsiConsole.Write(labelTable);
        return true;
    }

    /// <summary>
    /// 预检查联系人的现有标签（拉取标签下已有人员，避免重复设置）
    /// </summary>
    public async Task PreScanPlanLabelsAsync(SheetLabelPlan plan, bool isDryRun = false)
    {
        if (!_config.SkipIfAlreadyLabeled || !plan.LabelId.HasValue) return;

        if (isDryRun && plan.LabelId.Value == 9999)
        {
            AnsiConsole.MarkupLine($"[grey]演练模式（虚拟标签），跳过拉取现有联系人标签状态。[/]");
            return;
        }

        string targetIdStr = plan.LabelId.Value.ToString();
        AnsiConsole.MarkupLine($"\n[cyan]🔍 正在拉取标签 【{Markup.Escape(plan.TargetLabelName)}】 (ID: {plan.LabelId.Value}) 下已有人员状态...[/]");

        int checkedCount = 0;
        int alreadyTaggedCount = 0;
        int consecutiveFailures = 0;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"正在快速检索好友标签缓存 (0/{plan.Contacts.Count})...", async ctx =>
            {
                foreach (var contact in plan.Contacts)
                {
                    checkedCount++;
                    if (checkedCount % 5 == 0 || checkedCount == plan.Contacts.Count)
                    {
                        ctx.Status($"正在快速检索好友标签缓存 ({checkedCount}/{plan.Contacts.Count})...");
                    }

                    try
                    {
                        var (success, labelIdList, _) = await _apiClient.GetContactFastAsync(contact.Wxid);
                        if (success)
                        {
                            consecutiveFailures = 0;
                            if (!string.IsNullOrWhiteSpace(labelIdList))
                            {
                                var ids = labelIdList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                                contact.ExistingLabelIds = ids;
                                if (ids.Contains(targetIdStr))
                                {
                                    contact.AlreadyHasTargetLabel = true;
                                    alreadyTaggedCount++;
                                }
                            }
                        }
                        else
                        {
                            consecutiveFailures++;
                            if (isDryRun && consecutiveFailures >= 3)
                            {
                                break;
                            }
                        }
                    }
                    catch
                    {
                        consecutiveFailures++;
                        if (isDryRun && consecutiveFailures >= 3)
                        {
                            break;
                        }
                    }
                }
            });

        int pendingCount = plan.Contacts.Count - alreadyTaggedCount;
        AnsiConsole.MarkupLine($"[green]✔ 预检完成:[/] 检测到 [bold green]{alreadyTaggedCount}[/] 位好友已在该标签内（将自动跳过），剩余 [bold yellow]{pendingCount}[/] 位好友待新增打标。");
    }

    /// <summary>
    /// 执行标签同步与设置
    /// </summary>
    public async Task<List<ExecutionRecord>> ExecuteSyncAsync(List<SheetLabelPlan> plans, bool isDryRun = false)
    {
        var records = new List<ExecutionRecord>();
        int totalContacts = plans.Sum(p => p.Contacts.Count);
        int successCount = 0;
        int skippedCount = 0;
        int failCount = 0;

        // 步骤 1: 预检已有人员
        foreach (var plan in plans)
        {
            await PreScanPlanLabelsAsync(plan, isDryRun);
        }

        AnsiConsole.MarkupLine($"\n[bold yellow]开始执行标签同步 (对未标记好友每人间隔 {_config.RequestDelayMs / 1000.0:F1} 秒)...[/]");

        await AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn()
            )
            .StartAsync(async ctx =>
            {
                var progressTask = ctx.AddTask("[green]设置标签进度[/]", maxValue: totalContacts);

                foreach (var plan in plans)
                {
                    if (!plan.LabelId.HasValue)
                    {
                        AnsiConsole.MarkupLine($"[bold red]跳过 Sheet 【{Markup.Escape(plan.SheetName)}】: 标签 ID 无效[/]");
                        continue;
                    }

                    progressTask.Description = $"[cyan]正在处理: {Markup.Escape(plan.SheetName)} ({plan.Contacts.Count}人)[/]";

                    foreach (var contact in plan.Contacts)
                    {
                        var record = new ExecutionRecord
                        {
                            SheetName = plan.SheetName,
                            LabelName = plan.TargetLabelName,
                            LabelId = plan.LabelId.Value,
                            Wxid = contact.Wxid,
                            Name = !string.IsNullOrEmpty(contact.Name) ? contact.Name : contact.Nickname
                        };

                        // 如果好友已经在该标签内，直接跳过无需重复调用接口
                        if (_config.SkipIfAlreadyLabeled && contact.AlreadyHasTargetLabel)
                        {
                            record.Success = true;
                            record.Skipped = true;
                            record.Message = $"已在标签内 (ID: {plan.LabelId})，跳过设置";
                            skippedCount++;
                            records.Add(record);
                            progressTask.Increment(1);
                            progressTask.Description = $"[grey]好友 {Markup.Escape(contact.Wxid)} 已存在该标签，跳过[/]";
                            continue;
                        }

                        // 拼接标签 ID（保留已有标签 + 追加目标新标签）
                        string finalLabelId;
                        if (_config.AppendExistingLabels && contact.ExistingLabelIds.Count > 0)
                        {
                            var set = new HashSet<string>(contact.ExistingLabelIds) { plan.LabelId.Value.ToString() };
                            finalLabelId = string.Join(",", set);
                        }
                        else
                        {
                            finalLabelId = plan.LabelId.Value.ToString();
                        }

                        if (isDryRun)
                        {
                            record.Success = true;
                            record.Skipped = false;
                            record.Message = "[DryRun] 模拟执行，未实际调用微信";
                            successCount++;
                            await Task.Delay(2);
                        }
                        else
                        {
                            try
                            {
                                var (success, rawResp) = await _apiClient.ModifyContactLabelAsync(
                                    contact.Wxid, 
                                    finalLabelId
                                );

                                if (success)
                                {
                                    record.Success = true;
                                    record.Skipped = false;
                                    record.Message = "成功";
                                    successCount++;
                                }
                                else
                                {
                                    record.Success = false;
                                    record.Skipped = false;
                                    record.Message = $"失败: {rawResp}";
                                    failCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                record.Success = false;
                                record.Skipped = false;
                                record.Message = $"异常: {ex.Message}";
                                failCount++;
                            }

                            // 每次对微信进行操作后 sleep 指定时间（默认 5 秒），再进行下一次 post
                            if (_config.RequestDelayMs > 0)
                            {
                                var statusText = record.Success ? "[green]成功[/]" : "[red]失败[/]";
                                progressTask.Description = $"[cyan]好友 {Markup.Escape(contact.Wxid)} {statusText} | 安全等待 {_config.RequestDelayMs / 1000.0:F1} 秒后处理下一位...[/]";
                                await Task.Delay(_config.RequestDelayMs);
                            }
                        }

                        records.Add(record);
                        progressTask.Increment(1);
                    }
                }
            });

        // 打印结果统计
        AnsiConsole.WriteLine();
        var summaryTable = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold green]执行结果汇总[/]")
            .AddColumn(new TableColumn("项目"))
            .AddColumn(new TableColumn("数值").Centered());

        summaryTable.AddRow("总计划好友数", $"{totalContacts}");
        summaryTable.AddRow("[yellow]已存在自动跳过数[/]", $"[bold yellow]{skippedCount}[/]");
        summaryTable.AddRow("[green]本次成功新增设置数[/]", $"[bold green]{successCount}[/]");
        summaryTable.AddRow("[red]失败数[/]", failCount > 0 ? $"[bold red]{failCount}[/]" : "0");
        summaryTable.AddRow("模式", isDryRun ? "[yellow]模拟运行 (DryRun)[/]" : "[green]真实执行[/]");

        AnsiConsole.Write(summaryTable);

        // 保存报告到当前目录
        SaveReport(records);

        return records;
    }

    private static void SaveReport(List<ExecutionRecord> records)
    {
        try
        {
            var fileName = $"set_label_report_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var json = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(fileName, json);
            AnsiConsole.MarkupLine($"[green]✔ 执行日志及报告已保存至: [bold underline]{Markup.Escape(Path.GetFullPath(fileName))}[/][/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]保存报告文件失败: {Markup.Escape(ex.Message)}[/]");
        }
    }
}
