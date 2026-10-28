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

// 注册 HTTP 客户端与业务服务 (禁用系统代理，确保局域网与本地 Hook 19088 直连毫秒级响应)
builder.Services.AddHttpClient("", client => { })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        UseProxy = false
    });
builder.Services.AddSingleton<IStorageService, StorageService>();
builder.Services.AddSingleton<IContactService, ContactService>();
builder.Services.AddSingleton<IWeChatService, WeChatService>();
builder.Services.AddSingleton<IAiAnalysisService, AiAnalysisService>();
builder.Services.AddSingleton<ICvService, CvService>();

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

// 挂载 TempCache 本地缓存目录为静态文件目录
var tempCacheDir = Path.Combine(builder.Environment.ContentRootPath, "TempCache");
if (!Directory.Exists(tempCacheDir))
{
    Directory.CreateDirectory(tempCacheDir);
}
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(tempCacheDir),
    RequestPath = "/TempCache"
});

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

    // 1. 优先解析真实内部微信ID (处理自定义微信号别名)
    var realWxid = await weChatService.ResolveRealWxidAsync(contact.Wxid);
    if (!string.IsNullOrWhiteSpace(realWxid))
    {
        contact.RealWxid = realWxid;
    }

    // 2. 优先毫秒级读取本地数据库聊天记录，随后异步拉取朋友圈 (避免 Hook 阻塞)
    var chatHistory = await weChatService.GetChatHistoryAsync(contact);
    var (moments, coverUrl, snsNotice) = await weChatService.GetMomentsAsync(contact);

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

// 8.1 获取微信朋友圈首页时间线 (sns_get_first_page)
app.MapGet("/api/moments/first-page", async (IWeChatService weChatService, IContactService contactService) =>
{
    var (moments, firstPageMd5, session) = await weChatService.GetFirstPageMomentsAsync();

    // 匹配发布者联系人头像和评级信息
    foreach (var m in moments)
    {
        if (!string.IsNullOrWhiteSpace(m.UserName))
        {
            var contact = await contactService.GetContactByWxidAsync(m.UserName);
            if (contact != null)
            {
                if (string.IsNullOrWhiteSpace(m.Avatar)) m.Avatar = contact.Avatar;
                if (string.IsNullOrWhiteSpace(m.Nickname)) m.Nickname = contact.Name;
                m.Tier = contact.Tier;
                m.Remark = contact.Remark;
            }
        }
    }

    return Results.Ok(new
    {
        success = true,
        moments = moments,
        firstPageMd5 = firstPageMd5,
        session = session
    });
});

// 8.1.1 获取微信朋友圈下一页时间线 (sns_get_next_page)
app.MapPost("/api/moments/next-page", async (SnsNextPageRequest req, IWeChatService weChatService, IContactService contactService) =>
{
    if (string.IsNullOrWhiteSpace(req.LastItemId))
    {
        return Results.BadRequest(new { success = false, message = "必须提供 lastItemId" });
    }

    var (moments, nextSession, md5) = await weChatService.GetNextPageMomentsAsync(req.LastItemId, req.FirstPageMd5, req.Session);

    foreach (var m in moments)
    {
        if (!string.IsNullOrWhiteSpace(m.UserName))
        {
            var contact = await contactService.GetContactByWxidAsync(m.UserName);
            if (contact != null)
            {
                if (string.IsNullOrWhiteSpace(m.Avatar)) m.Avatar = contact.Avatar;
                if (string.IsNullOrWhiteSpace(m.Nickname)) m.Nickname = contact.Name;
                m.Tier = contact.Tier;
                m.Remark = contact.Remark;
            }
        }
    }

    return Results.Ok(new
    {
        success = true,
        moments = moments,
        session = nextSession,
        firstPageMd5 = md5,
        hasMore = moments.Count > 0
    });
});

// 8.2 触发朋友圈首页同步刷新
app.MapPost("/api/wechat/sync-first-page-moments", async (IWeChatService weChatService) =>
{
    var (moments, firstPageMd5, session) = await weChatService.GetFirstPageMomentsAsync();
    return Results.Ok(new
    {
        success = true,
        message = $"朋友圈首页刷新完成 (获取 {moments.Count} 条动态)",
        count = moments.Count,
        firstPageMd5 = firstPageMd5,
        session = session
    });
});

// 8.3 朋友圈点赞 (sns_like)
app.MapPost("/api/wechat/moments/like", async (SnsLikeRequest req, IWeChatService weChatService) =>
{
    if (string.IsNullOrWhiteSpace(req.SnsId))
    {
        return Results.BadRequest(new { success = false, message = "必须提供 sns_id" });
    }
    var (success, msg) = await weChatService.SnsLikeAsync(req.SnsId, req.ToWxid);
    return Results.Ok(new { success, message = msg });
});

// 8.4 取消朋友圈点赞 (sns_unlike)
app.MapPost("/api/wechat/moments/unlike", async (SnsLikeRequest req, IWeChatService weChatService) =>
{
    if (string.IsNullOrWhiteSpace(req.SnsId))
    {
        return Results.BadRequest(new { success = false, message = "必须提供 sns_id" });
    }
    var (success, msg) = await weChatService.SnsUnlikeAsync(req.SnsId, req.ToWxid);
    return Results.Ok(new { success, message = msg });
});

// 8.5 发表朋友圈一级评论 (sns_do_comment)
app.MapPost("/api/wechat/moments/comment", async (SnsCommentRequest req, IWeChatService weChatService) =>
{
    if (string.IsNullOrWhiteSpace(req.SnsId) || string.IsNullOrWhiteSpace(req.Comment))
    {
        return Results.BadRequest(new { success = false, message = "sns_id 和 comment 不能为空" });
    }
    var (success, msg, commentId) = await weChatService.SnsDoCommentAsync(req.SnsId, req.ToWxid, req.Comment);
    return Results.Ok(new { success, message = msg, commentId });
});

// 8.6 朋友圈回复评论 (sns_comment_reply)
app.MapPost("/api/wechat/moments/reply", async (SnsReplyRequest req, IWeChatService weChatService) =>
{
    if (string.IsNullOrWhiteSpace(req.SnsId) || string.IsNullOrWhiteSpace(req.Content))
    {
        return Results.BadRequest(new { success = false, message = "sns_id 和 content 不能为空" });
    }
    var (success, msg) = await weChatService.SnsCommentReplyAsync(req.SnsId, req.CommentId, req.Content);
    return Results.Ok(new { success, message = msg });
});

// 8.7 删除朋友圈评论 (sns_del_comment)
app.MapPost("/api/wechat/moments/delete-comment", async (SnsDeleteCommentRequest req, IWeChatService weChatService) =>
{
    if (string.IsNullOrWhiteSpace(req.SnsId) || string.IsNullOrWhiteSpace(req.CommentId))
    {
        return Results.BadRequest(new { success = false, message = "sns_id 和 commentId 不能为空" });
    }
    var (success, msg) = await weChatService.SnsDelCommentAsync(req.SnsId, req.CommentId);
    return Results.Ok(new { success, message = msg });
});

// 8.8 获取当前登录微信 Profile
app.MapGet("/api/wechat/my-profile", async (IWeChatService weChatService) =>
{
    var (wxid, nickname) = await weChatService.GetMyProfileAsync();
    return Results.Ok(new { success = true, wxid, nickname });
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


// 10. 微信图片免鉴权代理服务 (携带原生 MicroMessenger Client UA、规格降级与微信缓存目录智能检索)
app.MapMethods("/api/wechat/proxy-image", new[] { "GET", "HEAD" }, async (string url, IHttpClientFactory httpClientFactory, IConfiguration config) =>
{
    if (string.IsNullOrWhiteSpace(url))
    {
        return Results.BadRequest("URL 不能为空");
    }

    try
    {
        var decodedUrl = System.Net.WebUtility.UrlDecode(url);

        // 1. 尝试从微信客户端缓存检索 (含局域网共享 \\192.168.1.11\xwechat_files 与本地 xwechat_files 目录)
        var remoteCache = config["AppConfig:RemoteWeChatCachePath"] ?? @"\\192.168.1.11\xwechat_files";
        var userProfileDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localXWechat = Path.Combine(userProfileDir, "xwechat_files");
        var localDocWechat = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WeChat Files");
        var searchRoots = new[] { remoteCache, localXWechat, localDocWechat };

        var cachedBytes = await SearchWeChatCacheAsync(decodedUrl, searchRoots);
        if (cachedBytes != null && cachedBytes.Length > 100)
        {
            var isPng = cachedBytes.Length > 8 && cachedBytes[0] == 0x89 && cachedBytes[1] == 0x50;
            return Results.File(cachedBytes, isPng ? "image/png" : "image/jpeg");
        }

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
                request.Headers.UserAgent.Clear();
                request.Headers.TryAddWithoutValidation("User-Agent", "MicroMessenger Client");
                request.Headers.TryAddWithoutValidation("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
                request.Headers.TryAddWithoutValidation("Referer", "https://servicewechat.com/");

                var resp = await client.SendAsync(request);
                if (resp.IsSuccessStatusCode)
                {
                    var isBlocked = false;
                    if (resp.Headers.TryGetValues("X-Info", out var infoValues) && System.Linq.Enumerable.Any(infoValues, v => v.Contains("illref", StringComparison.OrdinalIgnoreCase))) isBlocked = true;
                    if (resp.Headers.TryGetValues("X-Errno", out var errValues) && System.Linq.Enumerable.Any(errValues, v => v == "-108" || v == "-106")) isBlocked = true;

                    var bytes = await resp.Content.ReadAsByteArrayAsync();
                    if (!isBlocked && bytes.Length > 100 && bytes.Length != 5093)
                    {
                        var contentType = resp.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                        return Results.File(bytes, contentType);
                    }
                }
            }
            catch { }
        }
    }
    catch { }

    // 3. 优雅降级：返回高颜值防盗链安全占位图
    var fallbackSvg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"280\" height=\"200\" viewBox=\"0 0 280 200\"><defs><linearGradient id=\"bg\" x1=\"0%\" y1=\"0%\" x2=\"100%\" y2=\"100%\"><stop offset=\"0%\" stop-color=\"#f1f5f9\"/><stop offset=\"100%\" stop-color=\"#e2e8f0\"/></linearGradient></defs><rect width=\"280\" height=\"200\" rx=\"10\" fill=\"url(#bg)\"/><circle cx=\"140\" cy=\"85\" r=\"28\" fill=\"#cbd5e1\"/><path d=\"M128 85a12 12 0 1 0 24 0 12 12 0 1 0 -24 0\" fill=\"#94a3b8\"/><path d=\"M132 72h16l4 5h8a6 6 0 0 1 6 6v18a6 6 0 0 1 -6 6h-40a6 6 0 0 1 -6 -6v-18a6 6 0 0 1 6 -6h8z\" fill=\"none\" stroke=\"#64748b\" stroke-width=\"2.5\" stroke-linejoin=\"round\"/><text x=\"50%\" y=\"135\" dominant-baseline=\"middle\" text-anchor=\"middle\" fill=\"#64748b\" font-family=\"-apple-system,BlinkMacSystemFont,PingFang SC,sans-serif\" font-size=\"12\" font-weight=\"600\">微信相册防盗链保护</text><text x=\"50%\" y=\"156\" dominant-baseline=\"middle\" text-anchor=\"middle\" fill=\"#94a3b8\" font-family=\"-apple-system,BlinkMacSystemFont,PingFang SC,sans-serif\" font-size=\"11\">在微信电脑端打开即可同步缓存</text></svg>";
    return Results.Content(fallbackSvg, "image/svg+xml");
});


// 10.1 朋友圈图片下载并缓存到 WxApi.Analyses/TempCache (支持微信共享缓存检索与 Hook 联动)
app.MapMethods("/api/moments/download-image", new[] { "GET", "POST" }, async (HttpContext httpContext, IHttpClientFactory httpClientFactory, IConfiguration config, IWeChatService weChatService) =>
{
    string? url = null;
    string? snsId = null;
    if (httpContext.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            using var reader = new StreamReader(httpContext.Request.Body);
            var bodyStr = await reader.ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(bodyStr))
            {
                using var doc = JsonDocument.Parse(bodyStr);
                if (doc.RootElement.TryGetProperty("url", out var uElem)) url = uElem.GetString();
                if (doc.RootElement.TryGetProperty("snsId", out var sElem)) snsId = sElem.GetString();
            }
        }
        catch { }
    }

    if (string.IsNullOrWhiteSpace(url)) url = httpContext.Request.Query["url"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(snsId)) snsId = httpContext.Request.Query["snsId"].FirstOrDefault();

    if (string.IsNullOrWhiteSpace(url))
    {
        return Results.BadRequest(new { success = false, message = "URL 不能为空" });
    }

    try
    {
        var decodedUrl = System.Net.WebUtility.UrlDecode(url);

        // 使用 URL 计算统一 MD5 哈希作为文件名
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hashBytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(decodedUrl));
        var hashHex = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        var fileName = $"{hashHex}.jpg";
        var localFilePath = Path.Combine(tempCacheDir, fileName);

        // 1. 如果已经在 TempCache 中缓存，直接秒级返回
        if (File.Exists(localFilePath) && new FileInfo(localFilePath).Length > 100)
        {
            return Results.Ok(new
            {
                success = true,
                cached = true,
                fileName = fileName,
                cachePath = $"WxApi.Analyses/TempCache/{fileName}",
                url = $"/TempCache/{fileName}"
            });
        }

        // 2. 联动微信 Hook：若提供了 snsId，触发 Hook 的 sns_get_detail 拉取该朋友圈详情与媒体
        if (!string.IsNullOrWhiteSpace(snsId) && long.TryParse(snsId, out var snsIdNum))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var client = httpClientFactory.CreateClient();
                    var hookUrl = weChatService.GetHookApiBaseUrl();
                    await client.PostAsJsonAsync($"{hookUrl}/sns_get_detail", new { sns_id = snsIdNum });
                }
                catch { }
            });
        }

        // 3. 尝试从微信客户端共享缓存与本地缓存中检索 (含 \\192.168.1.11\xwechat_files 与本地 xwechat_files)
        var remoteCache = config["AppConfig:RemoteWeChatCachePath"] ?? @"\\192.168.1.11\xwechat_files";
        var userProfileDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localXWechat = Path.Combine(userProfileDir, "xwechat_files");
        var localDocWechat = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WeChat Files");
        var searchRoots = new[] { remoteCache, localXWechat, localDocWechat };

        var cachedBytes = await SearchWeChatCacheAsync(decodedUrl, searchRoots);
        if (cachedBytes != null && cachedBytes.Length > 100)
        {
            await File.WriteAllBytesAsync(localFilePath, cachedBytes);
            return Results.Ok(new
            {
                success = true,
                cached = true,
                fromCache = true,
                fileName = fileName,
                cachePath = $"WxApi.Analyses/TempCache/{fileName}",
                url = $"/TempCache/{fileName}"
            });
        }

        // 4. 尝试网络代理下载 (按 /2000 -> /0 -> /150 顺序尝试)
        var candidateUrls = new List<string> { decodedUrl };
        if (decodedUrl.Contains(".qpic.cn/mmsns/") && decodedUrl.EndsWith("/2000"))
        {
            candidateUrls.Add(decodedUrl.Substring(0, decodedUrl.Length - 5) + "/0");
            candidateUrls.Add(decodedUrl.Substring(0, decodedUrl.Length - 5) + "/150");
        }
        else if (decodedUrl.Contains(".qpic.cn/mmsns/") && decodedUrl.EndsWith("/0"))
        {
            candidateUrls.Add(decodedUrl.Substring(0, decodedUrl.Length - 2) + "/150");
        }

        var httpClient = httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(8);
        byte[]? downloadedBytes = null;

        foreach (var reqUrl in candidateUrls)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, reqUrl);
                request.Headers.UserAgent.Clear();
                request.Headers.TryAddWithoutValidation("User-Agent", "MicroMessenger Client");
                request.Headers.TryAddWithoutValidation("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
                request.Headers.TryAddWithoutValidation("Referer", "https://servicewechat.com/");

                var resp = await httpClient.SendAsync(request);
                if (resp.IsSuccessStatusCode)
                {
                    var isTencentBlocked = false;
                    if (resp.Headers.TryGetValues("X-Info", out var infoValues) && System.Linq.Enumerable.Any(infoValues, v => v.Contains("illref", StringComparison.OrdinalIgnoreCase))) isTencentBlocked = true;
                    if (resp.Headers.TryGetValues("X-Errno", out var errValues) && System.Linq.Enumerable.Any(errValues, v => v == "-108" || v == "-106")) isTencentBlocked = true;

                    var bytes = await resp.Content.ReadAsByteArrayAsync();
                    if (!isTencentBlocked && bytes.Length > 100 && bytes.Length != 5093)
                    {
                        downloadedBytes = bytes;
                        break;
                    }
                }
            }
            catch { }
        }

        if (downloadedBytes != null && downloadedBytes.Length > 0)
        {
            await File.WriteAllBytesAsync(localFilePath, downloadedBytes);
            return Results.Ok(new
            {
                success = true,
                cached = false,
                fileName = fileName,
                cachePath = $"WxApi.Analyses/TempCache/{fileName}",
                url = $"/TempCache/{fileName}"
            });
        }
        else
        {
            // 防盗链优雅降级占位图
            var svgFallback = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"400\" height=\"300\" viewBox=\"0 0 400 300\"><defs><linearGradient id=\"g\" x1=\"0%\" y1=\"0%\" x2=\"100%\" y2=\"100%\"><stop offset=\"0%\" stop-color=\"#1e293b\"/><stop offset=\"100%\" stop-color=\"#0f172a\"/></linearGradient></defs><rect width=\"400\" height=\"300\" rx=\"10\" fill=\"url(#g)\"/><circle cx=\"200\" cy=\"105\" r=\"32\" fill=\"#334155\"/><text x=\"50%\" y=\"115\" dominant-baseline=\"middle\" text-anchor=\"middle\" font-size=\"28\">🔒</text><text x=\"50%\" y=\"170\" dominant-baseline=\"middle\" text-anchor=\"middle\" fill=\"#f8fafc\" font-size=\"15\" font-weight=\"600\" font-family=\"sans-serif\">微信相册防盗链保护图片</text><text x=\"50%\" y=\"200\" dominant-baseline=\"middle\" text-anchor=\"middle\" fill=\"#94a3b8\" font-size=\"12\" font-family=\"sans-serif\">腾讯 CDN 限制了外部直接提取</text><text x=\"50%\" y=\"225\" dominant-baseline=\"middle\" text-anchor=\"middle\" fill=\"#38bdf8\" font-size=\"11\" font-family=\"sans-serif\">已安全缓存至 WxApi.Analyses/TempCache</text></svg>";
            var svgFileName = $"{hashHex}.svg";
            var svgFilePath = Path.Combine(tempCacheDir, svgFileName);
            await File.WriteAllTextAsync(svgFilePath, svgFallback, System.Text.Encoding.UTF8);

            return Results.Ok(new
            {
                success = true,
                cached = false,
                isFallback = true,
                fileName = svgFileName,
                cachePath = $"WxApi.Analyses/TempCache/{svgFileName}",
                url = $"/TempCache/{svgFileName}"
            });
        }
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, title: "下载朋友圈图片异常");
    }
});


// 10.2 兼容直接以文件路由方式访问 TempCache
app.MapGet("/TempCache/{fileName}", (string fileName) =>
{
    var filePath = Path.Combine(tempCacheDir, fileName);
    if (!File.Exists(filePath))
    {
        return Results.NotFound();
    }
    var ext = Path.GetExtension(fileName).ToLowerInvariant();
    var mime = ext switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        _ => "application/octet-stream"
    };
    return Results.File(filePath, mime);
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

// 13. 获取好友详细资料 (get_contact_fast) 与 D:\CVs 匹配简历 (生成图2 Popup 弹窗数据)
app.MapGet("/api/contacts/{wxid}/cv-profile", async (string wxid, string? filePath, IContactService contactService, IWeChatService weChatService, ICvService cvService) =>
{
    // 1. 调用 Hook /api/get_contact_fast 获取好友底层实时详细资料 (含微信号、手机、备注、头像等)
    var fastInfo = await weChatService.GetContactFastAsync(wxid);
    var contact = await contactService.GetContactByWxidAsync(wxid);

    // 2. 在 D:\CVs 中根据 手机号 (如 18621284870) / userName / remark 等匹配并解析简历
    var cvResult = await cvService.FindAndParseCvAsync(fastInfo, contact, filePath);
    return Results.Ok(cvResult);
});

// 14. 打开本地简历原件 (Windows 系统默认打开程序)
app.MapPost("/api/cv/open", (OpenFileRequest req, ICvService cvService) =>
{
    if (string.IsNullOrWhiteSpace(req.FilePath)) return Results.BadRequest(new { message = "文件路径不能为空" });
    var success = cvService.OpenCvFile(req.FilePath);
    return Results.Ok(new { success, message = success ? "已成功在系统默认程序中打开简历" : "打开文件失败，请检查路径是否存在" });
});

// 15. 在 Windows 资源管理器中高亮定位简历文件
app.MapPost("/api/cv/explore", (OpenFileRequest req, ICvService cvService) =>
{
    if (string.IsNullOrWhiteSpace(req.FilePath)) return Results.BadRequest(new { message = "文件路径不能为空" });
    var success = cvService.ExploreCvFile(req.FilePath);
    return Results.Ok(new { success, message = success ? "已在资源管理器中定位文件" : "定位失败" });
});

// 16. 下载/流式预览简历文件
app.MapGet("/api/cv/download", (string filePath, ICvService cvService) =>
{
    var fileData = cvService.GetCvFileBytes(filePath);
    if (fileData == null) return Results.NotFound(new { message = "文件不存在" });
    return Results.File(fileData.Value.Bytes, fileData.Value.ContentType, fileData.Value.FileName);
});

// 17. 快速搜索 D:\CVs 库
app.MapGet("/api/cv/search", (string query, ICvService cvService) =>
{
    var list = cvService.SearchCvFiles(query);
    return Results.Ok(new { query, total = list.Count, data = list });
});

app.Run("http://localhost:5288");

// ================= 微信相册/多媒体缓存检索与解码解码器 =================
static byte[]? TryDecodeWeChatImage(byte[] rawBytes)
{
    if (rawBytes == null || rawBytes.Length < 16) return null;

    // 1. 标准 JPEG (FF D8 FF)
    if (rawBytes[0] == 0xFF && rawBytes[1] == 0xD8 && rawBytes[2] == 0xFF) return rawBytes;

    // 2. 标准 PNG (89 50 4E 47)
    if (rawBytes[0] == 0x89 && rawBytes[1] == 0x50 && rawBytes[2] == 0x4E && rawBytes[3] == 0x47) return rawBytes;

    // 3. WebP (RIFF ... WEBP)
    if (rawBytes.Length > 12 && rawBytes[0] == 'R' && rawBytes[1] == 'I' && rawBytes[2] == 'F' && rawBytes[3] == 'F' &&
        rawBytes[8] == 'W' && rawBytes[9] == 'E' && rawBytes[10] == 'B' && rawBytes[11] == 'P') return rawBytes;

    // 4. 微信 .dat XOR 单字节异或加密还原
    byte xorJpg = (byte)(rawBytes[0] ^ 0xFF);
    if ((rawBytes[1] ^ xorJpg) == 0xD8 && (rawBytes[2] ^ xorJpg) == 0xFF)
    {
        var decoded = new byte[rawBytes.Length];
        for (int i = 0; i < rawBytes.Length; i++) decoded[i] = (byte)(rawBytes[i] ^ xorJpg);
        return decoded;
    }
    byte xorPng = (byte)(rawBytes[0] ^ 0x89);
    if ((rawBytes[1] ^ xorPng) == 0x50 && (rawBytes[2] ^ xorPng) == 0x4E && (rawBytes[3] ^ xorPng) == 0x47)
    {
        var decoded = new byte[rawBytes.Length];
        for (int i = 0; i < rawBytes.Length; i++) decoded[i] = (byte)(rawBytes[i] ^ xorPng);
        return decoded;
    }

    return null;
}

static async Task<byte[]?> SearchWeChatCacheAsync(string decodedUrl, string[] searchRoots)
{
    using var md5 = System.Security.Cryptography.MD5.Create();
    var hashBytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(decodedUrl));
    var hashHex = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

    var now = DateTime.Now;
    var checkMonths = new[]
    {
        now.ToString("yyyy-MM"),
        now.AddMonths(-1).ToString("yyyy-MM"),
        now.AddMonths(-2).ToString("yyyy-MM")
    };

    foreach (var root in searchRoots)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;

        try
        {
            var userDirs = Directory.GetDirectories(root);
            // 优先排查活动账户目录 (如带有 1f94, 9s8j 等)
            var sortedUserDirs = userDirs
                .OrderByDescending(d => d.Contains("1f94") || d.Contains("9s8j") || d.Contains("aixian") || d.Contains("samuel"))
                .ToList();

            foreach (var userDir in sortedUserDirs)
            {
                var cacheDir = Path.Combine(userDir, "cache");
                if (!Directory.Exists(cacheDir)) continue;

                foreach (var month in checkMonths)
                {
                    var monthDir = Path.Combine(cacheDir, month);
                    if (!Directory.Exists(monthDir)) continue;

                    // 1. 优先扫描 HttpResource 目录 (微信存入的原生未加密高清 JPEG/PNG)
                    var httpResDir = Path.Combine(monthDir, "HttpResource");
                    if (Directory.Exists(httpResDir))
                    {
                        var directHttp = Path.Combine(httpResDir, hashHex);
                        if (File.Exists(directHttp))
                        {
                            var b = await File.ReadAllBytesAsync(directHttp);
                            var dec = TryDecodeWeChatImage(b);
                            if (dec != null) return dec;
                        }

                        // 前 8 位 MD5 模糊匹配
                        var partialMatches = Directory.GetFiles(httpResDir, $"{hashHex.Substring(0, 8)}*");
                        if (partialMatches.Length > 0)
                        {
                            var b = await File.ReadAllBytesAsync(partialMatches[0]);
                            var dec = TryDecodeWeChatImage(b);
                            if (dec != null) return dec;
                        }
                    }

                    // 2. 扫描 Sns/Img 目录 (2层哈希目录结构，如 00/a3c16a68...)
                    var snsImgDir = Path.Combine(monthDir, "Sns", "Img");
                    if (Directory.Exists(snsImgDir))
                    {
                        var subDir = Path.Combine(snsImgDir, hashHex.Substring(0, 2));
                        if (Directory.Exists(subDir))
                        {
                            var directSns = Path.Combine(subDir, hashHex.Substring(2));
                            if (File.Exists(directSns))
                            {
                                var b = await File.ReadAllBytesAsync(directSns);
                                var dec = TryDecodeWeChatImage(b);
                                if (dec != null) return dec;
                            }

                            var partialSns = Directory.GetFiles(subDir, $"{hashHex.Substring(2, 6)}*");
                            if (partialSns.Length > 0)
                            {
                                var b = await File.ReadAllBytesAsync(partialSns[0]);
                                var dec = TryDecodeWeChatImage(b);
                                if (dec != null) return dec;
                            }
                        }
                    }
                }
            }
        }
        catch { }
    }

    return null;
}


