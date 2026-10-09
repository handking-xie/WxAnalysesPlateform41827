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

// 9. 重新刷新 Excel 缓存
app.MapPost("/api/contacts/reload", (IContactService contactService) =>
{
    contactService.ReloadCache();
    return Results.Ok(new { success = true, message = "已重新加载 Excel 缓存" });
});


// 10. 微信图片免鉴权代理服务 (携带微信客户端 UA 与防盗链穿透)
app.MapMethods("/api/wechat/proxy-image", new[] { "GET", "HEAD" }, async (string url, IHttpClientFactory httpClientFactory) =>
{
    if (string.IsNullOrWhiteSpace(url))
    {
        return Results.BadRequest("URL 不能为空");
    }

    try
    {
        var decodedUrl = System.Net.WebUtility.UrlDecode(url);
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.6261.95 Safari/537.36 MicroMessenger/4.0.0 NetType/WIFI WindowsWechat");
        client.DefaultRequestHeaders.Add("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");

        var resp = await client.GetAsync(decodedUrl);
        if (resp.IsSuccessStatusCode)
        {
            var contentType = resp.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            var bytes = await resp.Content.ReadAsByteArrayAsync();
            return Results.File(bytes, contentType);
        }
    }
    catch { }

    var fallbackSvg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"200\" height=\"200\" viewBox=\"0 0 200 200\"><rect width=\"200\" height=\"200\" fill=\"#f8fafc\"/><text x=\"50%\" y=\"50%\" dominant-baseline=\"middle\" text-anchor=\"middle\" fill=\"#cbd5e1\" font-family=\"sans-serif\" font-size=\"12\">图片加载中/暂无原图</text></svg>";
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
