using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WxApi.Analyses.Models;
using WxApi.Analyses.Services;

var builder = WebApplication.CreateBuilder(args);

// 配置控制台编码
Console.OutputEncoding = System.Text.Encoding.UTF8;

// 注册 HTTP 客户端与业务服务
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IStorageService, StorageService>();
builder.Services.AddSingleton<IContactService, ContactService>();
builder.Services.AddSingleton<IWeChatService, WeChatService>();
builder.Services.AddSingleton<IAiAnalysisService, AiAnalysisService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

// ==================== RESTful APIs ====================

// 1. 获取好友列表 (支持 tier: S/A/B/C/D, search, status: PENDING/LINKED/STARRED/IGNORED)
app.MapGet("/api/contacts", async (string? tier, string? search, string? status, IContactService contactService) =>
{
    var list = await contactService.GetContactsAsync(tier, search, status);
    return Results.Ok(new { total = list.Count, data = list });
});

// 2. 获取指定好友详情（聚合联系人 + 聊天历史 + 朋友圈 + AI意向分析）
app.MapGet("/api/contacts/{wxid}/detail", async (string wxid, IContactService contactService, IWeChatService weChatService, IAiAnalysisService aiService) =>
{
    var contact = await contactService.GetContactByWxidAsync(wxid);
    if (contact == null)
    {
        return Results.NotFound(new { message = $"未找到 wxid 为 {wxid} 的好友" });
    }

    // 并行获取聊天记录与朋友圈
    var chatTask = weChatService.GetChatHistoryAsync(contact);
    var momentsTask = weChatService.GetMomentsAsync(contact);
    await Task.WhenAll(chatTask, momentsTask);

    var chatHistory = await chatTask;
    var (moments, coverUrl, snsNotice) = await momentsTask;

    if (!string.IsNullOrWhiteSpace(coverUrl))
    {
        contact.Cover = coverUrl;
    }

    // AI 深度意向分析与破冰话术推荐
    var aiInsight = await aiService.AnalyzeAsync(contact, chatHistory, moments);

    var detail = new ContactDetailResponse
    {
        Contact = contact,
        ChatHistory = chatHistory,
        Moments = moments,
        AiInsight = aiInsight,
        SnsNotice = snsNotice
    };

    return Results.Ok(detail);
});

// 3. 核心决策操作：【链接】/【重点】/【忽略】（直接保存至本地文件）
app.MapPost("/api/contacts/{wxid}/decide", async (string wxid, DecisionRequest req, IContactService contactService, IStorageService storageService) =>
{
    var contact = await contactService.GetContactByWxidAsync(wxid);
    if (contact == null)
    {
        return Results.NotFound(new { message = "好友不存在" });
    }

    if (string.IsNullOrWhiteSpace(req.Action))
    {
        return Results.BadRequest(new { message = "必须指定 Action (LINKED, STARRED 或 IGNORED)" });
    }

    var record = await storageService.SaveDecisionAsync(contact, req.Action, req.CustomPitch ?? string.Empty);
    var totalCount = await contactService.GetTotalCountAsync();
    var tierCounts = await contactService.GetTierCountsAsync();
    var stats = await storageService.GetStatsAsync(totalCount, tierCounts);

    return Results.Ok(new
    {
        success = true,
        record = record,
        stats = stats,
        message = $"已将【{contact.Name}】加入【{req.Action}】列表并已保存本地文件"
    });
});

// 4. 获取本地保存的文件列表内容 (linked / starred / ignored)
app.MapGet("/api/storage/{listType}", async (string listType, IStorageService storageService) =>
{
    var records = await storageService.GetRecordsAsync(listType);
    var filePath = storageService.GetFilePath(listType);
    return Results.Ok(new
    {
        listType = listType,
        filePath = filePath,
        count = records.Count,
        data = records
    });
});

// 5. 导出本地文件
app.MapGet("/api/storage/{listType}/export", async (string listType, IStorageService storageService) =>
{
    var filePath = storageService.GetFilePath(listType);
    if (!File.Exists(filePath))
    {
        return Results.NotFound(new { message = "本地文件尚未生成" });
    }
    var bytes = await File.ReadAllBytesAsync(filePath);
    return Results.File(bytes, "application/json", Path.GetFileName(filePath));
});

// 6. 全局统计看板
app.MapGet("/api/stats", async (IContactService contactService, IStorageService storageService) =>
{
    var total = await contactService.GetTotalCountAsync();
    var tierCounts = await contactService.GetTierCountsAsync();
    var stats = await storageService.GetStatsAsync(total, tierCounts);
    return Results.Ok(stats);
});

// 7. 发送微信消息 (调用 Hook)
app.MapPost("/api/wechat/send", async (SendMessageRequest req, IWeChatService weChatService) =>
{
    if (string.IsNullOrWhiteSpace(req.Wxid) || string.IsNullOrWhiteSpace(req.Message))
    {
        return Results.BadRequest(new { message = "参数不全" });
    }
    var (success, msg) = await weChatService.SendMessageAsync(req.Wxid, req.Message);
    return Results.Ok(new { success, message = msg });
});

// 8. 触发微信相册同步
app.MapPost("/api/wechat/sync-moments", async (string wxid, IWeChatService weChatService) =>
{
    var (success, msg) = await weChatService.TriggerSnsSyncAsync(wxid);
    return Results.Ok(new { success, message = msg });
});

// 9. 获取 WxDatas 中的所有 Excel 文件列表
app.MapGet("/api/excel/files", (IContactService contactService, IWeChatService weChatService) =>
{
    var files = contactService.GetAvailableExcelFiles();
    var currentPath = contactService.GetCurrentExcelFilePath();
    var currentName = Path.GetFileName(currentPath);
    var dir = contactService.GetWxDatasDirectory();
    var currentHook = weChatService.GetHookApiBaseUrl();

    foreach (var f in files)
    {
        f.HookUrl = weChatService.ResolveHookApiUrl(f.FileName);
        f.AccountKey = ContactService.ExtractAccountKey(f.FileName);
    }
    return Results.Ok(new
    {
        success = true,
        directory = dir,
        currentFile = currentName,
        currentPath = currentPath,
        currentHookUrl = currentHook,
        files = files
    });
});

// 10. 选择并加载指定的 Excel 文件
app.MapPost("/api/excel/select", async (SelectExcelRequest req, IContactService contactService, IWeChatService weChatService) =>
{
    var target = !string.IsNullOrWhiteSpace(req.FileName) ? req.FileName : req.FilePath;
    if (string.IsNullOrWhiteSpace(target))
    {
        return Results.BadRequest(new { message = "必须提供文件名或文件路径" });
    }

    try
    {
        contactService.SwitchExcelFile(target);
        var total = await contactService.GetTotalCountAsync();
        var currentFile = Path.GetFileName(contactService.GetCurrentExcelFilePath());
        var hookUrl = weChatService.GetHookApiBaseUrl();
        return Results.Ok(new
        {
            success = true,
            currentFile = currentFile,
            hookUrl = hookUrl,
            totalContacts = total,
            message = $"已成功加载 Excel：{currentFile}，对接微信 Hook 接口：{hookUrl}，共有 {total} 位意向好友"
        });
    }
    catch (FileNotFoundException ex)
    {
        return Results.NotFound(new { message = ex.Message });
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, title: "加载 Excel 失败");
    }
});

// 11. 重新刷新 Excel 缓存
app.MapPost("/api/contacts/reload", async (HttpContext httpContext, IContactService contactService, IWeChatService weChatService) =>
{
    string? fileName = null;
    if (httpContext.Request.HasJsonContentType())
    {
        try
        {
            var body = await httpContext.Request.ReadFromJsonAsync<SelectExcelRequest>();
            fileName = body?.FileName ?? body?.FilePath;
        }
        catch { }
    }
    contactService.ReloadCache(fileName);
    var currentFile = Path.GetFileName(contactService.GetCurrentExcelFilePath());
    var total = await contactService.GetTotalCountAsync();
    var hookUrl = weChatService.GetHookApiBaseUrl();
    return Results.Ok(new { success = true, currentFile = currentFile, hookUrl = hookUrl, totalContacts = total, message = $"已重新加载 Excel：{currentFile}，对接微信 Hook 接口：{hookUrl}，共有 {total} 位意向好友" });
});


// 10. 微信图片免鉴权代理服务 (携带原生 MicroMessenger Client UA、规格降级与本地缓存智能检索)
app.MapMethods("/api/wechat/proxy-image", new[] { "GET", "HEAD" }, async (string url, IHttpClientFactory httpClientFactory) =>
{
    if (string.IsNullOrWhiteSpace(url))
    {
        return Results.BadRequest("URL 不能为空");
    }

    try
    {
        var decodedUrl = System.Net.WebUtility.UrlDecode(url);

        // 1. 尝试从本地微信客户端缓存检索 (微信 4.x xwechat_files 目录)
        try
        {
            var userProfileDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var xWechatDir = Path.Combine(userProfileDir, "xwechat_files");
            if (Directory.Exists(xWechatDir))
            {
                // 计算 URL 的 MD5
                using var md5 = System.Security.Cryptography.MD5.Create();
                var hashBytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(decodedUrl));
                var hashHex = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

                // 在最近 3 个月的 Sns 缓存中查找
                var now = DateTime.Now;
                var checkDirs = new[]
                {
                    now.ToString("yyyy-MM"),
                    now.AddMonths(-1).ToString("yyyy-MM"),
                    now.AddMonths(-2).ToString("yyyy-MM")
                };

                foreach (var userDir in Directory.GetDirectories(xWechatDir))
                {
                    foreach (var month in checkDirs)
                    {
                        var snsCacheDir = Path.Combine(userDir, "cache", month, "Sns", "Img");
                        if (Directory.Exists(snsCacheDir))
                        {
                            var matchingFiles = Directory.GetFiles(snsCacheDir, $"*{hashHex.Substring(2, 8)}*", SearchOption.AllDirectories);
                            if (matchingFiles.Length > 0)
                            {
                                var localBytes = await File.ReadAllBytesAsync(matchingFiles[0]);
                                // 检查是否是标准 JPEG/PNG
                                if (localBytes.Length > 3 && localBytes[0] == 0xFF && localBytes[1] == 0xD8 && localBytes[2] == 0xFF)
                                {
                                    return Results.File(localBytes, "image/jpeg");
                                }
                                if (localBytes.Length > 8 && localBytes[0] == 0x89 && localBytes[1] == 0x50 && localBytes[2] == 0x4E && localBytes[3] == 0x47)
                                {
                                    return Results.File(localBytes, "image/png");
                                }
                            }
                        }
                    }
                }
            }
        }
        catch { }

        // 2. 网络代理请求：按候选 URL 列表轮询 (含 /2000 超清 -> /150 缩略图 -> /0 原图自动降级)
        var candidateUrls = new List<string> { decodedUrl };
        if (decodedUrl.Contains(".qpic.cn/mmsns/") && decodedUrl.EndsWith("/2000"))
        {
            candidateUrls.Add(decodedUrl.Substring(0, decodedUrl.Length - 5) + "/150");
            candidateUrls.Add(decodedUrl.Substring(0, decodedUrl.Length - 5) + "/0");
        }
        else if (decodedUrl.Contains(".qpic.cn/mmsns/") && decodedUrl.EndsWith("/0"))
        {
            candidateUrls.Add(decodedUrl.Substring(0, decodedUrl.Length - 2) + "/150");
        }

        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(8);

        foreach (var reqUrl in candidateUrls)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, reqUrl);
                // 关键防盗链穿透：视频号与朋友圈必须采用原生 MicroMessenger Client UA
                request.Headers.UserAgent.Clear();
                request.Headers.TryAddWithoutValidation("User-Agent", "MicroMessenger Client");
                request.Headers.TryAddWithoutValidation("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
                request.Headers.TryAddWithoutValidation("Referer", "https://servicewechat.com/");

                var resp = await client.SendAsync(request);
                if (resp.IsSuccessStatusCode)
                {
                    var contentType = resp.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                    var bytes = await resp.Content.ReadAsByteArrayAsync();
                    if (bytes.Length > 100) // 确保获取到真实图片
                    {
                        return Results.File(bytes, contentType);
                    }
                }
            }
            catch { }
        }
    }
    catch { }

    // 3. 优雅降级：返回高颜值防盗链安全占位图 (消除网页裂图)
    var fallbackSvg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"280\" height=\"200\" viewBox=\"0 0 280 200\"><defs><linearGradient id=\"bg\" x1=\"0%\" y1=\"0%\" x2=\"100%\" y2=\"100%\"><stop offset=\"0%\" stop-color=\"#f1f5f9\"/><stop offset=\"100%\" stop-color=\"#e2e8f0\"/></linearGradient></defs><rect width=\"280\" height=\"200\" rx=\"10\" fill=\"url(#bg)\"/><circle cx=\"140\" cy=\"85\" r=\"28\" fill=\"#cbd5e1\"/><path d=\"M128 85a12 12 0 1 0 24 0 12 12 0 1 0 -24 0\" fill=\"#94a3b8\"/><path d=\"M132 72h16l4 5h8a6 6 0 0 1 6 6v18a6 6 0 0 1 -6 6h-40a6 6 0 0 1 -6 -6v-18a6 6 0 0 1 6 -6h8z\" fill=\"none\" stroke=\"#64748b\" stroke-width=\"2.5\" stroke-linejoin=\"round\"/><text x=\"50%\" y=\"135\" dominant-baseline=\"middle\" text-anchor=\"middle\" fill=\"#64748b\" font-family=\"-apple-system,BlinkMacSystemFont,PingFang SC,sans-serif\" font-size=\"12\" font-weight=\"600\">微信相册防盗链保护</text><text x=\"50%\" y=\"156\" dominant-baseline=\"middle\" text-anchor=\"middle\" fill=\"#94a3b8\" font-family=\"-apple-system,BlinkMacSystemFont,PingFang SC,sans-serif\" font-size=\"11\">在微信电脑端打开即可同步缓存</text></svg>";
    return Results.Content(fallbackSvg, "image/svg+xml");
});

Console.WriteLine("=================================================");
Console.WriteLine("🚀 WxApi.Analyses 微信好友微创业意向全景分析工作台已就绪！");
Console.WriteLine("本地访问地址: http://localhost:5288");
Console.WriteLine("=================================================");


// 11. 保存 AI 话术编辑为训练数据 (持续优化触达建议模型)
app.MapPost("/api/ai/training-data", async (SaveAiTrainingSampleRequest req, IStorageService storageService) =>
{
    if (string.IsNullOrWhiteSpace(req.Wxid) || string.IsNullOrWhiteSpace(req.EditedPitch))
    {
        return Results.BadRequest(new { message = "Wxid 与编辑后的话术内容不能为空" });
    }

    var saved = await storageService.SaveTrainingSampleAsync(req);
    var allSamples = await storageService.GetTrainingSamplesAsync();

    return Results.Ok(new
    {
        success = true,
        sample = saved,
        totalCount = allSamples.Count,
        message = "已成功保存为 AI 训练样本，模型后续将自动参考优化"
    });
});

// 12. 获取 AI 训练样本列表及统计
app.MapGet("/api/ai/training-data", async (IStorageService storageService) =>
{
    var samples = await storageService.GetTrainingSamplesAsync();
    return Results.Ok(new
    {
        total = samples.Count,
        data = samples
    });
});

app.Run("http://localhost:5288");
