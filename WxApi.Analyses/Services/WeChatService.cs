using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WxApi.Analyses.Models;
using ZstdSharp;

namespace WxApi.Analyses.Services;

public interface IWeChatService
{
    Task<string> ResolveRealWxidAsync(string? wxidOrAlias);
    Task<GetContactFastResult> GetContactFastAsync(string wxid);
    Task<List<ChatMessage>> GetChatHistoryAsync(ContactItem contact);
    Task<(List<MomentItem> Moments, string? Cover, string? SnsNotice)> GetMomentsAsync(ContactItem contact);
    Task<(List<MomentItem> Moments, string? FirstPageMd5, string? Session)> GetFirstPageMomentsAsync(string? firstPageMd5 = "", string? maxId = "0");
    Task<(List<MomentItem> Moments, string? NextSession, string? FirstPageMd5)> GetNextPageMomentsAsync(string lastItemId, string? firstPageMd5 = null, string? session = null);
    Task<(bool Success, string Message)> SendMessageAsync(string wxid, string text);
    Task<(bool Success, string Message)> TriggerSnsSyncAsync(string wxid);
    Task<(bool Success, string Message)> SnsLikeAsync(string snsId, string toWxid);
    Task<(bool Success, string Message)> SnsUnlikeAsync(string snsId, string toWxid);
    Task<(bool Success, string Message, long CommentId)> SnsDoCommentAsync(string snsId, string toWxid, string comment);
    Task<(bool Success, string Message)> SnsCommentReplyAsync(string snsId, long commentId, string content);
    Task<(bool Success, string Message)> SnsDelCommentAsync(string snsId, string commentId);
    Task<(string Wxid, string Nickname)> GetMyProfileAsync();
    string GetHookApiBaseUrl();
    string ResolveHookApiUrl(string? fileNameOrPath);
}

public class WeChatService : IWeChatService
{
    private static string? _lastFirstPageMd5;
    private static string? _lastSessionBuffer;

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly IContactService _contactService;
    private readonly ILogger<WeChatService> _logger;

    public WeChatService(HttpClient httpClient, IConfiguration config, IContactService contactService, ILogger<WeChatService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _contactService = contactService;
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
    }

    public string GetHookApiBaseUrl()
    {
        var currentExcel = _contactService.GetCurrentExcelFilePath();
        return ResolveHookApiUrl(currentExcel);
    }

    public string ResolveHookApiUrl(string? fileNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(fileNameOrPath))
        {
            return GetDefaultHookUrl();
        }

        var fileName = Path.GetFileNameWithoutExtension(fileNameOrPath);

        // 默认映射字典 (根据用户指定配置)
        var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "samuelsurry", "http://127.0.0.1:19088/api" },
            { "xmwking", "http://127.0.0.1:19088/api" },
            { "aixian19920303", "http://192.168.1.9:19088/api" },
            { "aweihub", "http://192.168.100.121:19088/api" },
            { "xuanchen2020", "http://192.168.100.122:19088/api" },
            { "handkingxie", "http://192.168.100.123:19088/api" },
            { "xuanchen-xie", "http://192.168.100.124:19088/api" }
        };

        // 从 appsettings.json 中读取自定义配置覆盖
        var configSection = _config.GetSection("AppConfig:AccountHookUrls");
        foreach (var child in configSection.GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(child.Key) && !string.IsNullOrWhiteSpace(child.Value))
            {
                var val = child.Value.TrimEnd('/');
                if (!val.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
                {
                    val += "/api";
                }
                mapping[child.Key] = val;
            }
        }

        foreach (var kv in mapping)
        {
            if (fileName.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
            {
                return kv.Value.TrimEnd('/');
            }
        }

        return GetDefaultHookUrl();
    }

    private string GetDefaultHookUrl()
    {
        var configured = _config["AppConfig:WeChatHookUrl"]?.TrimEnd('/') ?? "http://127.0.0.1:19088";
        if (!configured.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            configured += "/api";
        }
        return configured;
    }

    private readonly ConcurrentDictionary<string, string> _wxidCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 将可能的微信号别名 (alias) 自动解析为底层真实的微信唯一标识 (username / wxid_xxxx)
    /// </summary>
    public async Task<string> ResolveRealWxidAsync(string? wxidOrAlias)
    {
        if (string.IsNullOrWhiteSpace(wxidOrAlias)) return string.Empty;

        var clean = wxidOrAlias.Trim();
        if (_wxidCache.TryGetValue(clean, out var cached))
        {
            return cached;
        }

        try
        {
            var escaped = clean.Replace("'", "''");
            var sql = $"SELECT username, alias FROM contact WHERE alias = '{escaped}' OR username = '{escaped}' LIMIT 1;";
            var payload = new
            {
                db_name = "contact.db",
                sql_fmt = sql
            };

            var resp = await _httpClient.PostAsJsonAsync($"{GetHookApiBaseUrl()}/sqlite3_exec", payload);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var dataArr) && dataArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in dataArr.EnumerateArray())
                    {
                        var username = item.TryGetProperty("username", out var u) ? u.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(username))
                        {
                            _wxidCache[clean] = username;
                            if (item.TryGetProperty("alias", out var a) && !string.IsNullOrWhiteSpace(a.GetString()))
                            {
                                _wxidCache[a.GetString()!] = username;
                            }
                            _logger.LogInformation("好友标识转换成功: {Original} -> 真实内部微信ID: {RealWxid}", clean, username);
                            return username;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "尝试从 contact.db 解析 {Wxid} 失败", clean);
        }

        // 若查询不到，回退原值
        _wxidCache[clean] = clean;
        return clean;
    }

    /// <summary>
    /// 调用 Hook /api/get_contact_fast 获取好友底层详细资料
    /// </summary>
    public async Task<GetContactFastResult> GetContactFastAsync(string wxid)
    {
        var result = new GetContactFastResult
        {
            Wxid = wxid
        };

        if (string.IsNullOrWhiteSpace(wxid)) return result;
        var clean = wxid.Trim();

        // 1. 尝试直接请求 Hook 快速查找接口 /api/get_contact_fast
        try
        {
            var hookUrl = $"{GetHookApiBaseUrl()}/get_contact_fast";
            var payload = new { wxid = clean };
            var resp = await _httpClient.PostAsJsonAsync(hookUrl, payload);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("contact", out var cElem))
                {
                    result.Success = true;
                    result.Source = "HookApi";

                    if (cElem.TryGetProperty("userName", out var uObj) && uObj.TryGetProperty("String", out var us))
                        result.UserName = us.GetString() ?? clean;
                    else
                        result.UserName = clean;

                    if (cElem.TryGetProperty("nickName", out var nObj) && nObj.TryGetProperty("String", out var ns))
                        result.Nickname = ns.GetString() ?? string.Empty;

                    if (cElem.TryGetProperty("remark", out var rObj) && rObj.TryGetProperty("String", out var rs))
                        result.Remark = rs.GetString() ?? string.Empty;

                    if (cElem.TryGetProperty("alias", out var aStr))
                        result.Alias = aStr.GetString() ?? string.Empty;

                    if (cElem.TryGetProperty("smallHeadImgUrl", out var sImg))
                        result.SmallHeadImgUrl = sImg.GetString() ?? string.Empty;

                    if (cElem.TryGetProperty("bigHeadImgUrl", out var bImg))
                        result.BigHeadImgUrl = bImg.GetString() ?? string.Empty;

                    if (cElem.TryGetProperty("city", out var city))
                        result.City = city.GetString() ?? string.Empty;

                    if (cElem.TryGetProperty("province", out var prov))
                        result.Province = prov.GetString() ?? string.Empty;

                    if (cElem.TryGetProperty("country", out var country))
                        result.Country = country.GetString() ?? string.Empty;

                    if (cElem.TryGetProperty("sex", out var sex))
                        result.Sex = sex.GetInt32();

                    // 尝试从 Hook contact 的独立电话字段获取 (phoneNumList / phone / mobile)
                    if (cElem.TryGetProperty("phoneNumList", out var pList) && pList.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var p in pList.EnumerateArray())
                        {
                            var ps = p.GetString();
                            if (!string.IsNullOrWhiteSpace(ps))
                            {
                                result.Phone = ps.Trim();
                                break;
                            }
                        }
                    }
                    else if (cElem.TryGetProperty("phone", out var pStr) && !string.IsNullOrWhiteSpace(pStr.GetString()))
                    {
                        result.Phone = pStr.GetString()!.Trim();
                    }
                    else if (cElem.TryGetProperty("mobile", out var mStr) && !string.IsNullOrWhiteSpace(mStr.GetString()))
                    {
                        result.Phone = mStr.GetString()!.Trim();
                    }

                    _logger.LogInformation("get_contact_fast 快速拉取资料成功: Wxid={Wxid}, UserName={UserName}, Nickname={Nick}, Remark={Remark}, Phone={Phone}",
                        clean, result.UserName, result.Nickname, result.Remark, result.Phone);
                    return result;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "调用 Hook /api/get_contact_fast 获取好友资料失败，将回退本地数据: {Wxid}", clean);
        }

        // 2. 本地 contact.db 或 ContactService 兜底
        var localContact = await _contactService.GetContactByWxidAsync(clean);
        if (localContact != null)
        {
            result.Success = true;
            result.Source = "LocalCache";
            result.UserName = localContact.RealWxid ?? localContact.Wxid;
            result.Nickname = localContact.Nickname;
            result.Remark = localContact.Remark;
            result.SmallHeadImgUrl = localContact.Avatar;
            result.BigHeadImgUrl = localContact.Avatar;
        }

        return result;
    }

    /// <summary>
    /// 获取指定好友的真实聊天记录 (根据微信 4.x 分表 Msg_<MD5(wxid)> 遍历所有 message_*.db 合并完整会话)
    /// </summary>
    public async Task<List<ChatMessage>> GetChatHistoryAsync(ContactItem contact)
    {
        var messages = new List<ChatMessage>();
        if (string.IsNullOrWhiteSpace(contact.Wxid)) return messages;

        // 优先解析真实内部微信ID (处理透视表中填写的自定义微信号别名)
        var realWxid = await ResolveRealWxidAsync(contact.Wxid);
        if (!string.IsNullOrWhiteSpace(realWxid) && string.IsNullOrWhiteSpace(contact.RealWxid))
        {
            contact.RealWxid = realWxid;
        }

        // 待查询的候选 ID 列表 (真实 wxid 为主，原始微信号为辅)
        var candidateIds = new List<string> { realWxid };
        if (!string.Equals(contact.Wxid, realWxid, StringComparison.OrdinalIgnoreCase))
        {
            candidateIds.Add(contact.Wxid);
        }

        // 计算表名 Msg_<MD5(wxid)>
        var tableNames = candidateIds
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id => $"Msg_{Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(id.Trim()))).ToLowerInvariant()}")
            .ToList();

        _logger.LogInformation("正在查询好友【{Name}】(原始:{Wxid}, 真实:{RealWxid}) 候选消息表: {Tables}",
            contact.Name, contact.Wxid, realWxid, string.Join(", ", tableNames));

        // 获取微信消息数据库列表 (包含 message_0.db, message_1.db 等)
        var dbNames = await GetMessageDbNamesAsync();

        // 收集所有数据库中的消息
        var rawMessageList = new List<(long CreateTime, long LocalId, string DbName, long LocalType, string Content, int Status, int RealSenderId)>();

        foreach (var dbName in dbNames)
        {
            foreach (var tableName in tableNames)
            {
                try
                {
                    var sql = $"SELECT local_id, local_type, create_time, message_content, status, real_sender_id FROM {tableName} ORDER BY create_time ASC LIMIT 300;";
                    var dbPayload = new
                    {
                        db_name = dbName,
                        sql_fmt = sql
                    };

                    var resp = await _httpClient.PostAsJsonAsync($"{GetHookApiBaseUrl()}/sqlite3_exec", dbPayload);
                    if (!resp.IsSuccessStatusCode) continue;

                    var json = await resp.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);

                    if (!doc.RootElement.TryGetProperty("data", out var dataArr) || dataArr.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    int countInDb = 0;
                    foreach (var item in dataArr.EnumerateArray())
                    {
                        var localId = ParseJsonLong(item, "local_id", 0);
                        var localType = ParseJsonLong(item, "local_type", 1);
                        var createTime = ParseJsonLong(item, "create_time", 0);
                        var rawContent = ParseJsonString(item, "message_content", "");
                        var status = ParseJsonInt(item, "status", 0);
                        var realSenderId = ParseJsonInt(item, "real_sender_id", 0);

                        rawMessageList.Add((createTime, localId, dbName, localType, rawContent, status, realSenderId));
                        countInDb++;
                    }

                    if (countInDb > 0)
                    {
                        _logger.LogInformation("从数据库 {Db} 的表 {Table} 成功读取到【{Name}】的聊天记录 {Count} 条", dbName, tableName, contact.Name, countInDb);
                        // 当前库已匹配到有效分表，避免重复查询其他别名表
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "查询 {Db} 中的表 {Table} 异常", dbName, tableName);
                }
            }
        }

        if (rawMessageList.Count == 0)
        {
            return messages;
        }

        // 按时间升序排序所有库中的聊天记录，并按 (DbName, LocalId) 去重
        var sortedList = rawMessageList
            .GroupBy(m => $"{m.DbName}_{m.LocalId}")
            .Select(g => g.First())
            .OrderBy(m => m.CreateTime)
            .ThenBy(m => m.LocalId)
            .ToList();

        foreach (var item in sortedList)
        {
            // 时间转换
            var timeStr = item.CreateTime > 0
                ? DateTimeOffset.FromUnixTimeSeconds(item.CreateTime).ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                : DateTime.Now.ToString("yyyy-MM-dd HH:mm");

            // 提取消息纯文本与富文本标识（含 ZSTD 自动解压缩）
            var parsedText = ParseMessageContent(item.LocalType, item.Content);
            if (string.IsNullOrWhiteSpace(parsedText)) continue;

            // 发送者判断：在微信 4.x 中，status==2 表示自己发送的消息，status==4/3/1 等表示对方发送
            bool isSelf = item.Status == 2;

            messages.Add(new ChatMessage
            {
                Sender = isSelf ? "self" : "peer",
                Time = timeStr,
                Text = parsedText,
                MsgType = item.LocalType switch
                {
                    3 => "image",
                    34 => "voice",
                    43 => "video",
                    49 => "link",
                    _ => "text"
                }
            });
        }

        return messages;
    }

    /// <summary>
    /// 获取指定好友的真实朋友圈 (优先调用 sns_get_user_page2 接口，并结合本地 sns.db 查询)
    /// </summary>
    public async Task<(List<MomentItem> Moments, string? Cover, string? SnsNotice)> GetMomentsAsync(ContactItem contact)
    {
        var moments = new List<MomentItem>();
        string? coverUrl = null;
        string? snsNotice = null;

        if (string.IsNullOrWhiteSpace(contact.Wxid))
        {
            return (moments, coverUrl, snsNotice);
        }

        var realWxid = await ResolveRealWxidAsync(contact.Wxid);

        // 1. 调用 Hook API: sns_get_user_page2 实时获取 (使用真实内部 ID)
        try
        {
            var reqPayload = new
            {
                to_wxid = realWxid.Trim(),
                firstPageMd5 = "",
                maxId = "0"
            };

            var resp = await _httpClient.PostAsJsonAsync($"{GetHookApiBaseUrl()}/sns_get_user_page2", reqPayload);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // 提取背景封面图
                if (root.TryGetProperty("snsUserInfo", out var userInfo))
                {
                    if (userInfo.TryGetProperty("snsBgimgId", out var bg) && !string.IsNullOrWhiteSpace(bg.GetString()))
                    {
                        coverUrl = bg.GetString();
                    }
                }

                // 提取提示信息（如：“朋友仅展示最近三天的朋友圈”）
                if (root.TryGetProperty("retTips", out var tips) && !string.IsNullOrWhiteSpace(tips.GetString()))
                {
                    snsNotice = tips.GetString();
                }

                // 提取朋友圈动态列表 objectList
                if (root.TryGetProperty("objectList", out var objList) && objList.ValueKind == JsonValueKind.Array)
                {
                    foreach (var obj in objList.EnumerateArray())
                    {
                        var createTime = obj.TryGetProperty("createTime", out var ct) ? ct.GetInt64() : 0;
                        var nickname = obj.TryGetProperty("nickname", out var nn) ? nn.GetString() : contact.Name;

                        string xmlBuffer = "";
                        if (obj.TryGetProperty("objectDesc", out var od) && od.TryGetProperty("buffer", out var buf))
                        {
                            var base64 = buf.GetString();
                            if (!string.IsNullOrWhiteSpace(base64))
                            {
                                try
                                {
                                    var xmlBytes = Convert.FromBase64String(base64);
                                    xmlBuffer = Encoding.UTF8.GetString(xmlBytes);
                                }
                                catch { }
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(xmlBuffer))
                        {
                            var moment = ParseTimelineXml(xmlBuffer, createTime, nickname ?? contact.Name, contact.Wxid);
                            if (moment != null)
                            {
                                var myProf = await GetMyProfileAsync();
                                PopulateMomentInteraction(moment, obj, myProf.Wxid, myProf.Nickname);
                                if (string.IsNullOrWhiteSpace(moment.UserName)) moment.UserName = contact.Wxid;
                                moments.Add(moment);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "调用 sns_get_user_page2 失败");
        }

        // 2. 如果接口返回的列表为空，从本地 sqlite 数据库 sns.db 的 SnsTimeLine 表补充查询
        if (moments.Count == 0)
        {
            try
            {
                var sql = $"SELECT tid, user_name, content FROM SnsTimeLine WHERE user_name = '{realWxid}' OR user_name = '{contact.Wxid}' ORDER BY tid DESC LIMIT 20;";
                var dbPayload = new
                {
                    db_name = "sns.db",
                    sql_fmt = sql
                };

                var resp = await _httpClient.PostAsJsonAsync($"{GetHookApiBaseUrl()}/sqlite3_exec", dbPayload);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("data", out var dataArr) && dataArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var row in dataArr.EnumerateArray())
                        {
                            var tid = row.TryGetProperty("tid", out var tElem) ? tElem.GetInt64().ToString() : "";
                            var xml = row.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                            if (!string.IsNullOrWhiteSpace(xml))
                            {
                                var m = ParseTimelineXml(xml, 0, contact.Name, contact.Wxid);
                                if (m != null)
                                {
                                    m.Id = tid;
                                    m.UserName = contact.Wxid;
                                    moments.Add(m);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "从 sns.db 查询朋友圈失败");
            }
        }

        return (moments, coverUrl, snsNotice);
    }

    public async Task<(bool Success, string Message)> SendMessageAsync(string wxid, string text)
    {
        try
        {
            var realWxid = await ResolveRealWxidAsync(wxid);
            var payload = new { wxid = realWxid, msg = text };
            var resp = await _httpClient.PostAsJsonAsync($"{GetHookApiBaseUrl()}/send_text_msg", payload);
            if (resp.IsSuccessStatusCode)
            {
                var content = await resp.Content.ReadAsStringAsync();
                return (true, $"消息已发送 (Hook 响应: {content})");
            }
            return (false, $"Hook 接口返回错误: {resp.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "调用发送微信消息接口失败");
            return (false, $"发送失败: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> TriggerSnsSyncAsync(string wxid)
    {
        try
        {
            var realWxid = await ResolveRealWxidAsync(wxid);
            var payload = new { to_wxid = realWxid, firstPageMd5 = "", maxId = "0" };
            var resp = await _httpClient.PostAsJsonAsync($"{GetHookApiBaseUrl()}/sns_get_user_page2", payload);
            return (true, "已向微信发送拉取朋友圈相册请求，微信后台将异步刷新");
        }
        catch (Exception ex)
        {
            return (false, $"拉取朋友圈失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 朋友圈点赞 (/api/sns_like)
    /// </summary>
    public async Task<(bool Success, string Message)> SnsLikeAsync(string snsId, string toWxid)
    {
        try
        {
            var realWxid = await ResolveRealWxidAsync(toWxid);
            var payload = new
            {
                sns_id = snsId.Trim(),
                to_wxid = realWxid.Trim()
            };
            var resp = await _httpClient.PostAsJsonAsync($"{GetHookApiBaseUrl()}/sns_like", payload);
            if (resp.IsSuccessStatusCode)
            {
                return (true, "点赞成功");
            }
            return (false, $"点赞失败: {resp.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "调用 sns_like 点赞异常");
            return (false, $"点赞异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 取消朋友圈点赞 (/api/sns_unlike)
    /// </summary>
    public async Task<(bool Success, string Message)> SnsUnlikeAsync(string snsId, string toWxid)
    {
        try
        {
            var realWxid = await ResolveRealWxidAsync(toWxid);
            var payload = new
            {
                sns_id = snsId.Trim(),
                to_wxid = realWxid.Trim()
            };
            var resp = await _httpClient.PostAsJsonAsync($"{GetHookApiBaseUrl()}/sns_unlike", payload);
            if (resp.IsSuccessStatusCode)
            {
                return (true, "已取消点赞");
            }
            return (false, $"取消点赞失败: {resp.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "调用 sns_unlike 取消点赞异常");
            return (false, $"取消点赞异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 发表朋友圈一级评论 (/api/sns_do_comment)
    /// </summary>
    public async Task<(bool Success, string Message, long CommentId)> SnsDoCommentAsync(string snsId, string toWxid, string comment)
    {
        try
        {
            var realWxid = await ResolveRealWxidAsync(toWxid);
            var payload = new
            {
                sns_id = snsId.Trim(),
                comment = comment.Trim(),
                to_wxid = realWxid.Trim()
            };
            var resp = await _httpClient.PostAsJsonAsync($"{GetHookApiBaseUrl()}/sns_do_comment", payload);
            if (resp.IsSuccessStatusCode)
            {
                long newCommentId = 0;
                try
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("commentId", out var cid))
                    {
                        newCommentId = cid.GetInt64();
                    }
                    else if (doc.RootElement.TryGetProperty("data", out var d) && d.TryGetProperty("commentId", out var dcid))
                    {
                        newCommentId = dcid.GetInt64();
                    }
                }
                catch { }

                return (true, "评论已发表", newCommentId);
            }
            return (false, $"发表评论失败: {resp.StatusCode}", 0);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "调用 sns_do_comment 发表评论异常");
            return (false, $"发表评论异常: {ex.Message}", 0);
        }
    }

    /// <summary>
    /// 朋友圈回复评论 (/api/sns_comment_reply)
    /// </summary>
    public async Task<(bool Success, string Message)> SnsCommentReplyAsync(string snsId, long commentId, string content)
    {
        try
        {
            var payload = new
            {
                content = content.Trim(),
                sns_id = snsId.Trim(),
                comment_id = commentId
            };
            var resp = await _httpClient.PostAsJsonAsync($"{GetHookApiBaseUrl()}/sns_comment_reply", payload);
            if (resp.IsSuccessStatusCode)
            {
                return (true, "回复已发送");
            }
            return (false, $"回复失败: {resp.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "调用 sns_comment_reply 回复评论异常");
            return (false, $"回复异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 删除朋友圈评论 (/api/sns_del_comment)
    /// </summary>
    public async Task<(bool Success, string Message)> SnsDelCommentAsync(string snsId, string commentId)
    {
        try
        {
            var payload = new
            {
                sns_id = snsId.Trim(),
                commentId = commentId.Trim()
            };
            var resp = await _httpClient.PostAsJsonAsync($"{GetHookApiBaseUrl()}/sns_del_comment", payload);
            if (resp.IsSuccessStatusCode)
            {
                return (true, "评论已删除");
            }
            return (false, $"删除评论失败: {resp.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "调用 sns_del_comment 删除评论异常");
            return (false, $"删除评论异常: {ex.Message}");
        }
    }

    private (string Wxid, string Nickname)? _cachedMyProfile;
    public async Task<(string Wxid, string Nickname)> GetMyProfileAsync()
    {
        if (_cachedMyProfile.HasValue && !string.IsNullOrWhiteSpace(_cachedMyProfile.Value.Wxid))
        {
            return _cachedMyProfile.Value;
        }

        try
        {
            var resp = await _httpClient.PostAsJsonAsync($"{GetHookApiBaseUrl()}/get_profile_new", new { });
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("userInfo", out var ui))
                {
                    string wxid = "";
                    string nick = "";
                    if (ui.TryGetProperty("userName", out var un) && un.TryGetProperty("String", out var uns))
                    {
                        wxid = uns.GetString() ?? "";
                    }
                    if (ui.TryGetProperty("nickName", out var nn) && nn.TryGetProperty("String", out var nns))
                    {
                        nick = nns.GetString() ?? "";
                    }
                    if (!string.IsNullOrWhiteSpace(wxid))
                    {
                        _cachedMyProfile = (wxid, string.IsNullOrWhiteSpace(nick) ? wxid : nick);
                        return _cachedMyProfile.Value;
                    }
                }
            }
        }
        catch { }

        return ("self", "我");
    }

    private static void PopulateMomentInteraction(MomentItem moment, JsonElement obj, string myWxid, string myNickname)
    {
        var id = obj.TryGetProperty("id", out var idElem) ? idElem.GetString() ?? "" : "";
        if (!string.IsNullOrWhiteSpace(id))
        {
            moment.Id = id;
        }

        var likeFlag = obj.TryGetProperty("likeFlag", out var lfElem) ? lfElem.GetInt32() : 0;
        var likeCount = obj.TryGetProperty("likeCount", out var lcElem) ? lcElem.GetInt32() : 0;
        var commentCount = obj.TryGetProperty("commentCount", out var ccElem) ? ccElem.GetInt32() : 0;

        var likes = new List<string>();
        if (obj.TryGetProperty("likeUserList", out var lul) && lul.ValueKind == JsonValueKind.Array)
        {
            foreach (var l in lul.EnumerateArray())
            {
                var lNick = l.TryGetProperty("nickname", out var lne) ? lne.GetString() : null;
                var lUser = l.TryGetProperty("username", out var lue) ? lue.GetString() : null;
                var name = !string.IsNullOrWhiteSpace(lNick) ? lNick : lUser;
                if (!string.IsNullOrWhiteSpace(name) && !likes.Contains(name))
                {
                    likes.Add(name);
                }
            }
        }

        var comments = new List<MomentComment>();
        if (obj.TryGetProperty("commentUserList", out var cul) && cul.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in cul.EnumerateArray())
            {
                var cid = c.TryGetProperty("commentId", out var cidElem) ? cidElem.GetInt64() : 0;
                var cUser = c.TryGetProperty("nickname", out var cne) ? cne.GetString() ?? "" : "";
                var cUsername = c.TryGetProperty("username", out var cue) ? cue.GetString() ?? "" : "";
                var cContent = c.TryGetProperty("content", out var cce) ? cce.GetString() ?? "" : "";
                var rCid = c.TryGetProperty("replyCommentId", out var rce) ? rce.GetInt64() : 0;
                var ctSec = c.TryGetProperty("createTime", out var cte) ? cte.GetInt64() : 0;
                var timeStr = ctSec > 0 ? DateTimeOffset.FromUnixTimeSeconds(ctSec).ToLocalTime().ToString("MM-dd HH:mm") : "";

                comments.Add(new MomentComment
                {
                    CommentId = cid,
                    User = string.IsNullOrWhiteSpace(cUser) ? cUsername : cUser,
                    UserName = cUsername,
                    Text = cContent,
                    ReplyCommentId = rCid,
                    Time = timeStr,
                    IsSelf = (cUsername == myWxid || (!string.IsNullOrWhiteSpace(myNickname) && cUser == myNickname))
                });
            }

            foreach (var c in comments)
            {
                if (c.ReplyCommentId > 0)
                {
                    var parent = comments.FirstOrDefault(x => x.CommentId == c.ReplyCommentId);
                    if (parent != null)
                    {
                        c.ReplyToUser = parent.User;
                        c.ReplyToUserName = parent.UserName;
                    }
                }
            }
        }

        moment.IsLiked = (likeFlag == 1) || (likes.Contains(myNickname));
        moment.LikeCount = likeCount > 0 ? likeCount : likes.Count;
        moment.CommentCount = commentCount > 0 ? commentCount : comments.Count;
        if (likes.Count > 0) moment.Likes = likes;
        if (comments.Count > 0) moment.Comments = comments;
    }

    /// <summary>
    /// 获取微信朋友圈首页公共时间线 (优先调用 Hook /api/sns_get_first_page 接口，并结合本地 sns.db 查询)
    /// </summary>
    public async Task<(List<MomentItem> Moments, string? FirstPageMd5, string? Session)> GetFirstPageMomentsAsync(string? firstPageMd5 = "", string? maxId = "0")
    {
        var moments = new List<MomentItem>();
        string? respFirstPageMd5 = null;
        string? respSession = null;

        // 1. 调用 Hook API: sns_get_first_page 实时获取朋友圈首页
        try
        {
            var reqPayload = new
            {
                firstPageMd5 = firstPageMd5 ?? "",
                maxId = maxId ?? "0"
            };

            var resp = await _httpClient.PostAsJsonAsync($"{GetHookApiBaseUrl()}/sns_get_first_page", reqPayload);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("firstPageMd5", out var md5) && !string.IsNullOrWhiteSpace(md5.GetString()))
                {
                    respFirstPageMd5 = md5.GetString();
                    _lastFirstPageMd5 = respFirstPageMd5;
                }

                if (root.TryGetProperty("session", out var sess) && sess.TryGetProperty("buffer", out var buf) && !string.IsNullOrWhiteSpace(buf.GetString()))
                {
                    respSession = buf.GetString();
                    _lastSessionBuffer = respSession;
                }

                // 提取朋友圈首页动态列表 objectList
                if (root.TryGetProperty("objectList", out var objList) && objList.ValueKind == JsonValueKind.Array)
                {
                    foreach (var obj in objList.EnumerateArray())
                    {
                        var username = obj.TryGetProperty("username", out var un) ? un.GetString() ?? "" : "";
                        var nickname = obj.TryGetProperty("nickname", out var nn) ? nn.GetString() ?? "" : "";
                        var createTime = obj.TryGetProperty("createTime", out var ct) ? ct.GetInt64() : 0;

                        string xmlBuffer = "";
                        if (obj.TryGetProperty("objectDesc", out var od) && od.TryGetProperty("buffer", out var sBuf))
                        {
                            var base64 = sBuf.GetString();
                            if (!string.IsNullOrWhiteSpace(base64))
                            {
                                try
                                {
                                    var xmlBytes = Convert.FromBase64String(base64);
                                    xmlBuffer = Encoding.UTF8.GetString(xmlBytes);
                                }
                                catch { }
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(xmlBuffer))
                        {
                            var moment = ParseTimelineXml(xmlBuffer, createTime, nickname, username);
                            if (moment != null)
                            {
                                moment.UserName = username;
                                moment.Nickname = string.IsNullOrWhiteSpace(nickname) ? moment.Nickname : nickname;
                                var myProf = await GetMyProfileAsync();
                                PopulateMomentInteraction(moment, obj, myProf.Wxid, myProf.Nickname);
                                moments.Add(moment);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "调用 sns_get_first_page 获取朋友圈首页失败");
        }

        // 2. 如果接口返回为空，从本地 sqlite 数据库 sns.db 的 SnsTimeLine 表补充查询全局朋友圈最新时间线
        if (moments.Count == 0)
        {
            try
            {
                var sql = "SELECT tid, user_name, content FROM SnsTimeLine ORDER BY tid DESC LIMIT 30;";
                var dbPayload = new
                {
                    db_name = "sns.db",
                    sql_fmt = sql
                };

                var resp = await _httpClient.PostAsJsonAsync($"{GetHookApiBaseUrl()}/sqlite3_exec", dbPayload);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("data", out var dataArr) && dataArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var row in dataArr.EnumerateArray())
                        {
                            var tid = row.TryGetProperty("tid", out var tElem) ? tElem.GetInt64().ToString() : "";
                            var uName = row.TryGetProperty("user_name", out var un) ? un.GetString() ?? "" : "";
                            var xml = row.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                            if (!string.IsNullOrWhiteSpace(xml))
                            {
                                var m = ParseTimelineXml(xml, 0, uName, uName);
                                if (m != null)
                                {
                                    m.Id = tid;
                                    m.UserName = uName;
                                    moments.Add(m);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "从 sns.db 查询朋友圈首页动态失败");
            }
        }

        return (moments, respFirstPageMd5, respSession);
    }

    /// <summary>
    /// 获取微信朋友圈下一页动态 (sns_get_next_page)
    /// </summary>
    public async Task<(List<MomentItem> Moments, string? NextSession, string? FirstPageMd5)> GetNextPageMomentsAsync(string lastItemId, string? firstPageMd5 = null, string? session = null)
    {
        var moments = new List<MomentItem>();
        string md5ToUse = !string.IsNullOrWhiteSpace(firstPageMd5) ? firstPageMd5 : _lastFirstPageMd5 ?? "";
        string sessionToUse = !string.IsNullOrWhiteSpace(session) ? session : _lastSessionBuffer ?? "";
        string? nextSession = null;

        try
        {
            var reqPayload = new
            {
                firstPageMd5 = md5ToUse,
                lastItemid = lastItemId ?? "",
                session = sessionToUse
            };

            var resp = await _httpClient.PostAsJsonAsync($"{GetHookApiBaseUrl()}/sns_get_next_page", reqPayload);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("firstPageMd5", out var md5Elem) && !string.IsNullOrWhiteSpace(md5Elem.GetString()))
                {
                    md5ToUse = md5Elem.GetString()!;
                    _lastFirstPageMd5 = md5ToUse;
                }

                if (root.TryGetProperty("session", out var sessElem) && sessElem.TryGetProperty("buffer", out var bufElem) && !string.IsNullOrWhiteSpace(bufElem.GetString()))
                {
                    nextSession = bufElem.GetString();
                    _lastSessionBuffer = nextSession;
                }

                if (root.TryGetProperty("objectList", out var objList) && objList.ValueKind == JsonValueKind.Array)
                {
                    foreach (var obj in objList.EnumerateArray())
                    {
                        var username = obj.TryGetProperty("username", out var un) ? un.GetString() ?? "" : "";
                        var nickname = obj.TryGetProperty("nickname", out var nn) ? nn.GetString() ?? "" : "";
                        var createTime = obj.TryGetProperty("createTime", out var ct) ? ct.GetInt64() : 0;

                        string xmlBuffer = "";
                        if (obj.TryGetProperty("objectDesc", out var od) && od.TryGetProperty("buffer", out var sBuf))
                        {
                            var base64 = sBuf.GetString();
                            if (!string.IsNullOrWhiteSpace(base64))
                            {
                                try
                                {
                                    var xmlBytes = Convert.FromBase64String(base64);
                                    xmlBuffer = Encoding.UTF8.GetString(xmlBytes);
                                }
                                catch { }
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(xmlBuffer))
                        {
                            var moment = ParseTimelineXml(xmlBuffer, createTime, nickname, username);
                            if (moment != null)
                            {
                                moment.UserName = username;
                                moment.Nickname = string.IsNullOrWhiteSpace(nickname) ? moment.Nickname : nickname;
                                var myProf = await GetMyProfileAsync();
                                PopulateMomentInteraction(moment, obj, myProf.Wxid, myProf.Nickname);
                                moments.Add(moment);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "调用 sns_get_next_page 获取朋友圈下一页失败");
        }

        // 本地 sqlite 兜底 (从 SnsTimeLine 取更早的动态)
        if (moments.Count == 0 && long.TryParse(lastItemId, out var lastTid))
        {
            try
            {
                var sql = $"SELECT tid, user_name, content FROM SnsTimeLine WHERE tid < {lastTid} ORDER BY tid DESC LIMIT 20;";
                var dbPayload = new
                {
                    db_name = "sns.db",
                    sql_fmt = sql
                };

                var resp = await _httpClient.PostAsJsonAsync($"{GetHookApiBaseUrl()}/sqlite3_exec", dbPayload);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("data", out var dataArr) && dataArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var row in dataArr.EnumerateArray())
                        {
                            var tid = row.TryGetProperty("tid", out var tElem) ? tElem.GetInt64().ToString() : "";
                            var uName = row.TryGetProperty("user_name", out var un) ? un.GetString() ?? "" : "";
                            var xml = row.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                            if (!string.IsNullOrWhiteSpace(xml))
                            {
                                var m = ParseTimelineXml(xml, 0, uName, uName);
                                if (m != null)
                                {
                                    m.Id = tid;
                                    m.UserName = uName;
                                    moments.Add(m);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "从 sns.db 查询朋友圈下一页历史动态失败");
            }
        }

        return (moments, nextSession, md5ToUse);
    }

    // ================= 辅助解析方法 =================

    private async Task<List<string>> GetMessageDbNamesAsync()
    {
        var dbs = new List<string>();
        try
        {
            var resp = await _httpClient.PostAsJsonAsync($"{GetHookApiBaseUrl()}/get_db_handle", new { });
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var list) && list.ValueKind == JsonValueKind.Array)
                {
                    foreach (var it in list.EnumerateArray())
                    {
                        var name = it.TryGetProperty("name", out var n) ? n.GetString() : null;
                        // 精准匹配 message_0.db, message_1.db 等，过滤掉 message_resource.db 和 message_fts.db
                        if (!string.IsNullOrWhiteSpace(name) && Regex.IsMatch(name, @"^message_\d+\.db$", RegexOptions.IgnoreCase))
                        {
                            dbs.Add(name);
                        }
                    }
                }
            }
        }
        catch { }

        if (dbs.Count == 0)
        {
            dbs = new List<string> { "message_0.db", "message_1.db", "message_2.db" };
        }

        return dbs.OrderBy(x => x).ToList();
    }

    private static long ParseJsonLong(JsonElement element, string propName, long fallback = 0)
    {
        if (!element.TryGetProperty(propName, out var p)) return fallback;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var v)) return v;
        if (p.ValueKind == JsonValueKind.String && long.TryParse(p.GetString(), out var sv)) return sv;
        return fallback;
    }

    private static int ParseJsonInt(JsonElement element, string propName, int fallback = 0)
    {
        if (!element.TryGetProperty(propName, out var p)) return fallback;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var v)) return v;
        if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var sv)) return sv;
        return fallback;
    }

    private static string ParseJsonString(JsonElement element, string propName, string fallback = "")
    {
        if (!element.TryGetProperty(propName, out var p)) return fallback;
        return p.GetString() ?? fallback;
    }

    private static string ParseMessageContent(long localType, string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        // 若含有 zstd 压缩流，自动解压缩还原明文
        raw = TryDecompressZstd(raw);

        return localType switch
        {
            1 => CleanTextMessage(raw),
            3 => "[图片]",
            34 => "[语音消息]",
            42 => "[个人名片]",
            43 => "[视频]",
            47 => "[动画表情]",
            49 => ExtractAppMsgTitle(raw),
            10000 => $"[系统通知] {raw}",
            _ => CleanTextMessage(raw)
        };
    }

    /// <summary>
    /// 解密/解压缩微信 4.x Zstandard (zstd) 压缩的文本
    /// </summary>
    private static string TryDecompressZstd(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;

        var trimmed = raw.Trim();
        // 快速检测 Base64 以及 Zstandard Magic 标头 (0x28 0xB5 0x2F 0xFD 对应 Base64 "KLUv")
        if (trimmed.StartsWith("KLUv", StringComparison.Ordinal) ||
            (trimmed.Length >= 20 && trimmed.Length % 4 == 0 && Regex.IsMatch(trimmed, @"^[A-Za-z0-9+/=]+$")))
        {
            try
            {
                var bytes = Convert.FromBase64String(trimmed);
                if (bytes.Length >= 4 && bytes[0] == 0x28 && bytes[1] == 0xB5 && bytes[2] == 0x2F && bytes[3] == 0xFD)
                {
                    using var decompressor = new ZstdSharp.Decompressor();
                    var decompressed = decompressor.Unwrap(bytes).ToArray();
                    return Encoding.UTF8.GetString(decompressed);
                }
            }
            catch
            {
                // 若解码或解压缩异常，回退原始文本
            }
        }
        return raw;
    }

    private static string CleanTextMessage(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        // 如果包含群聊前缀 (如 "wxid_xxx:\n内容")
        var colonIdx = raw.IndexOf(":\n", StringComparison.Ordinal);
        if (colonIdx > 0 && colonIdx < 40)
        {
            return raw.Substring(colonIdx + 2).Trim();
        }
        return raw.Trim();
    }

    private static string ExtractAppMsgTitle(string xml)
    {
        try
        {
            var match = Regex.Match(xml, @"<title>(.*?)</title>", RegexOptions.Singleline);
            if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
            {
                var title = match.Groups[1].Value.Replace("<![CDATA[", "").Replace("]]>", "").Trim();
                return $"[链接卡片] {title}";
            }
        }
        catch { }
        return "[分享链接/小程序]";
    }

    private static MomentItem? ParseTimelineXml(string xml, long defaultTime, string defaultNickname, string? defaultUsername = null)
    {
        try
        {
            // 提取发布时间
            long createTimeSec = defaultTime;
            var timeMatch = Regex.Match(xml, @"<createTime>(\d+)</createTime>");
            if (timeMatch.Success && long.TryParse(timeMatch.Groups[1].Value, out var ts))
            {
                createTimeSec = ts;
            }

            var dateStr = createTimeSec > 0
                ? DateTimeOffset.FromUnixTimeSeconds(createTimeSec).ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                : "近期";

            // 提取文字内容
            var contentMatch = Regex.Match(xml, @"<contentDesc>(.*?)</contentDesc>", RegexOptions.Singleline);
            var content = contentMatch.Success 
                ? contentMatch.Groups[1].Value.Replace("<![CDATA[", "").Replace("]]>", "").Trim() 
                : "";

            if (string.IsNullOrWhiteSpace(content))
            {
                var descMatch = Regex.Match(xml, @"<description>(.*?)</description>", RegexOptions.Singleline);
                if (descMatch.Success)
                {
                    content = descMatch.Groups[1].Value.Replace("<![CDATA[", "").Replace("]]>", "").Trim();
                }
            }

            // 提取图片列表 (按媒体节点 <media> 精准解析，支持普通照片 UHD、实况照片 LivePhoto 静态帧、视频号封面)
            var images = new List<string>();

            // 1. 常规媒体节点 <media>...</media>
            var mediaNodes = Regex.Matches(xml, @"<media>(.*?)</media>", RegexOptions.Singleline);
            foreach (Match m in mediaNodes)
            {
                var mXml = m.Groups[1].Value;
                string? chosenUrl = null;

                // 优先 1：普通照片，提取 UHD 超高清大图 (腾讯 CDN 免密直接访问 200 OK)
                var uhdMatch = Regex.Match(mXml, @"<uhd.*?>(http.*?)</uhd>", RegexOptions.Singleline);
                if (uhdMatch.Success)
                {
                    chosenUrl = uhdMatch.Groups[1].Value.Trim();
                }

                // 优先 2：iPhone 实况照片 (LivePhoto)，提取其 liveMedia 内部的 thumb (实况静态封面图，Content-Type 为 image/jpg)
                if (string.IsNullOrWhiteSpace(chosenUrl) && mXml.Contains("<LivePhoto>"))
                {
                    var liveThumbMatch = Regex.Match(mXml, @"<liveMedia>.*?<thumb.*?>(http.*?)</thumb>.*?</liveMedia>", RegexOptions.Singleline);
                    if (liveThumbMatch.Success)
                    {
                        chosenUrl = liveThumbMatch.Groups[1].Value.Trim();
                    }
                }

                // 优先 3：次选普通图片 url
                if (string.IsNullOrWhiteSpace(chosenUrl))
                {
                    var urlMatch = Regex.Match(mXml, @"<url.*?>(http.*?)</url>", RegexOptions.Singleline);
                    if (urlMatch.Success)
                    {
                        var u = urlMatch.Groups[1].Value.Trim();
                        if (!u.Contains("/video/") && (!u.Contains("snsvideodownload") || u.Contains("dotrans=0")))
                        {
                            chosenUrl = u;
                        }
                    }
                }

                // 优先 4：普通封面 thumb
                if (string.IsNullOrWhiteSpace(chosenUrl))
                {
                    var thumbMatch = Regex.Match(mXml, @"<thumb.*?>(http.*?)</thumb>", RegexOptions.Singleline);
                    if (thumbMatch.Success)
                    {
                        var t = thumbMatch.Groups[1].Value.Trim();
                        if (!t.Contains("/video/") && (!t.Contains("snsvideodownload") || t.Contains("dotrans=0")))
                        {
                            chosenUrl = t;
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(chosenUrl))
                {
                    chosenUrl = System.Net.WebUtility.HtmlDecode(chosenUrl);
                    if (!images.Contains(chosenUrl))
                    {
                        images.Add(chosenUrl);
                    }
                }
            }

            // 2. 视频号等富媒体动态的封面图 (coverUrl / thumbUrl)
            if (images.Count == 0)
            {
                var coverMatches = Regex.Matches(xml, @"<(coverUrl|thumbUrl)>(http.*?)</(coverUrl|thumbUrl)>", RegexOptions.Singleline);
                foreach (Match cm in coverMatches)
                {
                    var cu = System.Net.WebUtility.HtmlDecode(cm.Groups[2].Value.Trim());
                    if (!string.IsNullOrWhiteSpace(cu) && !images.Contains(cu))
                    {
                        images.Add(cu);
                    }
                }
            }

            var idMatch = Regex.Match(xml, @"<id>(\d+)</id>");
            var parsedId = idMatch.Success ? idMatch.Groups[1].Value.Trim() : "";

            var usernameMatch = Regex.Match(xml, @"<username>(.*?)</username>");
            var parsedUsername = usernameMatch.Success ? usernameMatch.Groups[1].Value.Replace("<![CDATA[", "").Replace("]]>", "").Trim() : defaultUsername;

            return new MomentItem
            {
                Id = parsedId,
                Date = dateStr,
                Content = string.IsNullOrWhiteSpace(content) ? (images.Count > 0 ? "[分享图片]" : "") : content,
                Images = images,
                UserName = parsedUsername,
                Nickname = defaultNickname,
                Likes = new List<string>(),
                Comments = new List<MomentComment>()
            };
        }
        catch
        {
            return null;
        }
    }
}
