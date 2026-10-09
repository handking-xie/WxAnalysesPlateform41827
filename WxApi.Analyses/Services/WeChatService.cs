using System;
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
    Task<List<ChatMessage>> GetChatHistoryAsync(ContactItem contact);
    Task<(List<MomentItem> Moments, string? Cover, string? SnsNotice)> GetMomentsAsync(ContactItem contact);
    Task<(bool Success, string Message)> SendMessageAsync(string wxid, string text);
    Task<(bool Success, string Message)> TriggerSnsSyncAsync(string wxid);
}

public class WeChatService : IWeChatService
{
    private readonly HttpClient _httpClient;
    private readonly string _hookBaseUrl;
    private readonly ILogger<WeChatService> _logger;

    public WeChatService(HttpClient httpClient, IConfiguration config, ILogger<WeChatService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _hookBaseUrl = config["AppConfig:WeChatHookUrl"]?.TrimEnd('/') ?? "http://127.0.0.1:19088";
    }

    /// <summary>
    /// 获取指定好友的真实聊天记录 (根据微信 4.x 分表 Msg_<MD5(wxid)> 遍历所有 message_*.db 合并完整会话)
    /// </summary>
    public async Task<List<ChatMessage>> GetChatHistoryAsync(ContactItem contact)
    {
        var messages = new List<ChatMessage>();
        if (string.IsNullOrWhiteSpace(contact.Wxid)) return messages;

        // 计算表名 Msg_<MD5(wxid)>
        var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(contact.Wxid.Trim()));
        var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        var tableName = $"Msg_{hashHex}";

        _logger.LogInformation("正在查询好友【{Name}】({Wxid}) 消息表: {Table}", contact.Name, contact.Wxid, tableName);

        // 获取微信消息数据库列表 (包含 message_0.db, message_1.db 等)
        var dbNames = await GetMessageDbNamesAsync();

        // 收集所有数据库中的消息
        var rawMessageList = new List<(long CreateTime, int LocalId, string DbName, int LocalType, string Content, int Status, int RealSenderId)>();

        foreach (var dbName in dbNames)
        {
            try
            {
                var sql = $"SELECT local_id, local_type, create_time, message_content, status, real_sender_id FROM {tableName} ORDER BY create_time ASC LIMIT 300;";
                var dbPayload = new
                {
                    db_name = dbName,
                    sql_fmt = sql
                };

                var resp = await _httpClient.PostAsJsonAsync($"{_hookBaseUrl}/api/sqlite3_exec", dbPayload);
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
                    var localId = item.TryGetProperty("local_id", out var li) ? li.GetInt32() : 0;
                    var localType = item.TryGetProperty("local_type", out var lt) ? lt.GetInt32() : 1;
                    var createTime = item.TryGetProperty("create_time", out var ct) ? ct.GetInt64() : 0;
                    var rawContent = item.TryGetProperty("message_content", out var mc) ? mc.GetString() ?? "" : "";
                    var status = item.TryGetProperty("status", out var st) ? st.GetInt32() : 0;
                    var realSenderId = item.TryGetProperty("real_sender_id", out var rsi) ? rsi.GetInt32() : 0;

                    rawMessageList.Add((createTime, localId, dbName, localType, rawContent, status, realSenderId));
                    countInDb++;
                }

                if (countInDb > 0)
                {
                    _logger.LogInformation("从数据库 {Db} 成功读取到【{Name}】的聊天记录 {Count} 条", dbName, contact.Name, countInDb);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "查询 {Db} 中的表 {Table} 异常", dbName, tableName);
            }
        }

        if (rawMessageList.Count == 0)
        {
            return messages;
        }

        // 按时间升序排序所有库中的聊天记录
        var sortedList = rawMessageList
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

        // 1. 调用 Hook API: sns_get_user_page2 实时获取
        try
        {
            var reqPayload = new
            {
                to_wxid = contact.Wxid.Trim(),
                firstPageMd5 = "",
                maxId = "0"
            };

            var resp = await _httpClient.PostAsJsonAsync($"{_hookBaseUrl}/api/sns_get_user_page2", reqPayload);
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
                            var moment = ParseTimelineXml(xmlBuffer, createTime, nickname ?? contact.Name);
                            if (moment != null)
                            {
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
                var sql = $"SELECT tid, user_name, content FROM SnsTimeLine WHERE user_name = '{contact.Wxid}' ORDER BY tid DESC LIMIT 20;";
                var dbPayload = new
                {
                    db_name = "sns.db",
                    sql_fmt = sql
                };

                var resp = await _httpClient.PostAsJsonAsync($"{_hookBaseUrl}/api/sqlite3_exec", dbPayload);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("data", out var dataArr) && dataArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var row in dataArr.EnumerateArray())
                        {
                            var xml = row.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                            if (!string.IsNullOrWhiteSpace(xml))
                            {
                                var m = ParseTimelineXml(xml, 0, contact.Name);
                                if (m != null)
                                {
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
            var payload = new { wxid = wxid, msg = text };
            var resp = await _httpClient.PostAsJsonAsync($"{_hookBaseUrl}/api/send_text_msg", payload);
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
            var payload = new { to_wxid = wxid, firstPageMd5 = "", maxId = "0" };
            var resp = await _httpClient.PostAsJsonAsync($"{_hookBaseUrl}/api/sns_get_user_page2", payload);
            return (true, "已向微信发送拉取朋友圈相册请求，微信后台将异步刷新");
        }
        catch (Exception ex)
        {
            return (false, $"拉取朋友圈失败: {ex.Message}");
        }
    }

    // ================= 辅助解析方法 =================

    private async Task<List<string>> GetMessageDbNamesAsync()
    {
        var dbs = new List<string>();
        try
        {
            var resp = await _httpClient.PostAsJsonAsync($"{_hookBaseUrl}/api/get_db_handle", new { });
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

    private static string ParseMessageContent(int localType, string raw)
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

    private static MomentItem? ParseTimelineXml(string xml, long defaultTime, string defaultNickname)
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

            return new MomentItem
            {
                Date = dateStr,
                Content = string.IsNullOrWhiteSpace(content) ? (images.Count > 0 ? "[分享图片]" : "") : content,
                Images = images,
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
