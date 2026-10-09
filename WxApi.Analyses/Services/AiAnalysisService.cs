using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WxApi.Analyses.Models;

namespace WxApi.Analyses.Services;

public interface IAiAnalysisService
{
    Task<AiInsight> AnalyzeAsync(ContactItem contact, List<ChatMessage> chatHistory, List<MomentItem> moments);
}

public class AiAnalysisService : IAiAnalysisService
{
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiAnalysisService> _logger;
    private readonly IStorageService _storageService;

    public AiAnalysisService(
        IConfiguration config, 
        HttpClient httpClient, 
        ILogger<AiAnalysisService> logger,
        IStorageService storageService)
    {
        _config = config;
        _httpClient = httpClient;
        _logger = logger;
        _storageService = storageService;
    }

    public async Task<AiInsight> AnalyzeAsync(ContactItem contact, List<ChatMessage> chatHistory, List<MomentItem> moments)
    {
        var enableExternal = _config.GetValue<bool>("AppConfig:AiSettings:EnableExternalApi");
        var apiKey = _config["AppConfig:AiSettings:ApiKey"];
        var endpoint = _config["AppConfig:AiSettings:ApiEndpoint"];

        List<AiTrainingSample> trainingSamples = new();
        try
        {
            trainingSamples = await _storageService.GetTrainingSamplesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取 AI 训练样本数据失败");
        }

        if (enableExternal && !string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(endpoint))
        {
            try
            {
                return await CallExternalAiApiAsync(contact, chatHistory, moments, endpoint, apiKey, trainingSamples);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "调用外部大模型 API 失败，自动切换为内置专家分析引擎");
            }
        }

        return GenerateExpertAnalysis(contact, chatHistory, moments, trainingSamples);
    }

    private static AiInsight GenerateExpertAnalysis(
        ContactItem contact, 
        List<ChatMessage> chatHistory, 
        List<MomentItem> moments,
        List<AiTrainingSample>? trainingSamples)
    {
        var name = contact.Name;
        var remark = contact.Remark;
        var tier = contact.Tier;

        string painPoint;
        string profile;
        string interestLevel;
        string recommendedPitch;
        string timingAdvice;
        bool learnedFromSample = false;
        string? sampleSourceId = null;

        bool hasChat = chatHistory != null && chatHistory.Count > 0;
        bool hasMoments = moments != null && moments.Count > 0;

        switch (tier)
        {
            case "S":
                interestLevel = "95% (S级-强烈意向)";
                painPoint = "对传统主业/重资产模式的内卷与瓶颈深有痛感；迫切需要低风险、轻资产、高现金流的私域微创业项目建立第二增长曲线。";
                profile = $"执行力强，对商业敏锐度高，属于关键高价值合伙人梯队。" + (!string.IsNullOrWhiteSpace(remark) ? $" (透视表备注: {remark})" : "");
                if (hasChat || hasMoments)
                {
                    recommendedPitch = $"{name}好！结合你近期的互动，传统模式确实太耗精力了。针对你手头的资源底子，我们刚好落地了一套『轻资产·私域高复购微创业闭环模型』，毛利高且无需压资金。我把核心操盘速览脑图发你先瞅瞅？合适的话我们详细碰撞一下！";
                    timingAdvice = "对方近期有互动或动态记录，建议在今日午间 (12:00~13:30) 或晚间 (20:00~21:30) 直接切入，转化率极高。";
                }
                else
                {
                    recommendedPitch = $"{name}好！好久没联系了，最近都挺顺利吧？我们团队近期落地了一套针对行业好友的『轻资产·私域高现金流微创业闭环模型』，毛利高且无需压本金。我把核心操盘速览脑图先发你瞅瞅？若有共鸣我们详细碰撞一下！";
                    timingAdvice = "透视表意向极强但近期无直接聊天，建议以干货资料分享或温馨问候作为破冰起点，避免生硬推销。";
                }
                break;

            case "A":
                interestLevel = "85% (A级-明确意向)";
                painPoint = "主业面临天花板或不确定性，积极寻求标准化、轻量级的下班后副业管道，看重落地可行性与真实案例。";
                profile = "注重稳健可落地，排斥夸大宣传，倾向有真实交付背书的合作。" + (!string.IsNullOrWhiteSpace(remark) ? $" (透视表备注: {remark})" : "");
                recommendedPitch = $"{name}好！好久不见，最近忙些什么呢？看大家都在琢磨建立第二增长曲线，我们在跑的这套微创业项目刚好很契合兼职/轻量启动，每天投入1-2小时，标准化自动化交付。发你一份5分钟实操复盘拆解交流下？";
                timingAdvice = "建议以探讨案例或复盘干货作为切入钩子，温和唤醒。";
                break;

            case "B":
                interestLevel = "68% (B级-潜在意向)";
                painPoint = "对副业增收有认知但缺少切入契机；担心时间精力不够或启动门槛高，处于观望考量期。";
                profile = "谨慎理性，需要低门槛示范与安全感信任传递。" + (!string.IsNullOrWhiteSpace(remark) ? $" (透视表备注: {remark})" : "");
                recommendedPitch = $"{name}好久不见！最近工作生活都挺顺畅吧？我们近期梳理了一套针对职场朋友的零门槛轻量微创业入门手册，不少兼职朋友都拿到了不错的正向反馈，发你参考看看，或许对你有灵感启发！";
                timingAdvice = "建议先发问候，随后以无压力干货分享切入。";
                break;

            case "D":
                interestLevel = "35% (D级-偏好固定)";
                painPoint = "当前主业较为稳定或偏好较为固定，暂无强烈的副业转型冲动，但保持基本好友弱连接。";
                profile = "自我状态稳固，不宜推销商业项目，适合作为行业人脉长期维护。";
                recommendedPitch = $"{name}好！最近一切都挺顺畅吧？改天有机会多交流行业心得，祝工作顺利！";
                timingAdvice = "仅做定期弱连接维系，可优先标记为忽略或低频跟进。";
                break;

            default: // C
                interestLevel = "50% (C级-观望中立)";
                painPoint = "了解微创业概念但缺乏具体行动导火索，需要看到身边同行的真实结果反馈。";
                profile = "中立观望型，随大流倾向明显，需要标杆案例刺激。" + (!string.IsNullOrWhiteSpace(remark) ? $" (透视表备注: {remark})" : "");
                recommendedPitch = $"{name}好！好久没联系了，最近大家都在讨论打造抗风险的第二副业管道。我们这边刚好整理了各行业伙伴的轻创业轻量落地总结，干货满满，发你一份交流探讨！";
                timingAdvice = "可结合其所处行业痛点抛出话题，测试其互动意向。";
                break;
        }

        // ==================== 智能融合训练数据进行优化 ====================
        if (trainingSamples != null && trainingSamples.Count > 0)
        {
            // 1. 精确匹配：该好友曾经被人工精修过专属话术
            var exactSample = trainingSamples.FirstOrDefault(s => string.Equals(s.Wxid, contact.Wxid, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(s.EditedPitch));
            if (exactSample != null)
            {
                recommendedPitch = exactSample.EditedPitch;
                learnedFromSample = true;
                sampleSourceId = exactSample.Id;
            }
            else
            {
                // 2. 意向层级匹配：自动学习该等级下最新人工精修沉淀的优质模板
                var tierSample = trainingSamples.FirstOrDefault(s => string.Equals(s.Tier, tier, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(s.EditedPitch));
                if (tierSample != null)
                {
                    recommendedPitch = AdaptPitchToContact(tierSample.EditedPitch, tierSample.Name, name);
                    learnedFromSample = true;
                    sampleSourceId = tierSample.Id;
                }
            }
        }

        return new AiInsight
        {
            PainPoint = painPoint,
            Profile = profile,
            InterestLevel = interestLevel,
            RecommendedPitch = recommendedPitch,
            TimingAdvice = timingAdvice,
            LearnedFromSample = learnedFromSample,
            SampleSourceId = sampleSourceId
        };
    }

    private static string AdaptPitchToContact(string templatePitch, string sampleName, string targetName)
    {
        if (string.IsNullOrWhiteSpace(templatePitch)) return templatePitch;

        // 如果模板中含有样本姓名，直接替换为当前目标姓名
        if (!string.IsNullOrWhiteSpace(sampleName) && templatePitch.Contains(sampleName))
        {
            return templatePitch.Replace(sampleName, targetName);
        }

        // 智能识别开头的打招呼称谓并替换
        var firstPunc = templatePitch.IndexOfAny(new[] { '！', '!', '，', ',', ' ' });
        if (firstPunc > 0 && firstPunc <= 10 && (templatePitch.Substring(0, firstPunc).Contains("好") || templatePitch.Substring(0, firstPunc).Length <= 4))
        {
            var rest = templatePitch.Substring(firstPunc + 1).TrimStart();
            return $"{targetName}好！{rest}";
        }

        return $"{targetName}好！{templatePitch}";
    }

    private async Task<AiInsight> CallExternalAiApiAsync(
        ContactItem contact, 
        List<ChatMessage> chatHistory, 
        List<MomentItem> moments, 
        string endpoint, 
        string apiKey,
        List<AiTrainingSample>? trainingSamples)
    {
        var model = _config["AppConfig:AiSettings:Model"] ?? "gpt-4o-mini";

        string fewShotSection = string.Empty;
        if (trainingSamples != null && trainingSamples.Count > 0)
        {
            var relevantSamples = trainingSamples
                .OrderByDescending(s => string.Equals(s.Tier, contact.Tier, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(s => s.CreatedAt)
                .Take(3)
                .ToList();

            if (relevantSamples.Count > 0)
            {
                var sampleLines = relevantSamples.Select((s, i) =>
                    $"【人工精修训练样本 {i + 1}】（目标意向等级: {s.Tier}级）:\n" +
                    (!string.IsNullOrWhiteSpace(s.OriginalPitch) ? $"原系统建议: {s.OriginalPitch}\n" : "") +
                    $"人工校准优化话术: {s.EditedPitch}");

                fewShotSection = $@"
【人工精修高转化训练范例库（请重点学习以下人工校准话术的语调、去推销感、利益点呈现与高情商沟通方式）】：
{string.Join("\n\n", sampleLines)}

请在输出 recommendedPitch 时，充分吸收上述训练范例的优秀表达技巧与亲切风格，针对当前好友定制生成！
";
            }
        }

        var prompt = $@"
你是一位微信私域创业与高情商沟通专家。请结合以下微信好友的资料、历史聊天记录与朋友圈动态，分析其副业/微创业意向，并给出最合适的链接（发送）对方的消息。
{fewShotSection}

【好友资料】
姓名：{contact.Name}
备注：{contact.Remark}
意向等级：{contact.TierLabel}

【聊天历史】
{string.Join("\n", chatHistory.Select(m => $"{(m.Sender == "self" ? "我" : contact.Name)}: {m.Text}"))}

【朋友圈动态】
{string.Join("\n", moments.Select(m => $"[{m.Date}] {m.Content}"))}

请输出纯 JSON 格式：
{{
  ""painPoint"": ""核心痛点分析"",
  ""profile"": ""人群画像与沟通偏好"",
  ""interestLevel"": ""意向百分比及等级"",
  ""recommendedPitch"": ""最合适发送给对方的一键破冰/链接触达消息（真实、亲切、高情商、不生硬推销）"",
  ""timingAdvice"": ""最佳触达时机与策略建议""
}}
";

        var requestBody = new
        {
            model = model,
            messages = new[]
            {
                new { role = "system", content = "你是一个只输出有效 JSON 的专业私域分析助理。" },
                new { role = "user", content = prompt }
            },
            temperature = 0.7
        };

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Content = JsonContent.Create(requestBody);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var resJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(resJson);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (content != null)
        {
            var cleanJson = content.Trim();
            if (cleanJson.StartsWith("```json")) cleanJson = cleanJson.Substring(7);
            if (cleanJson.StartsWith("```")) cleanJson = cleanJson.Substring(3);
            if (cleanJson.EndsWith("```")) cleanJson = cleanJson.Substring(0, cleanJson.Length - 3);
            cleanJson = cleanJson.Trim();

            var parsed = JsonSerializer.Deserialize<AiInsight>(cleanJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (parsed != null && !string.IsNullOrWhiteSpace(parsed.RecommendedPitch))
            {
                parsed.LearnedFromSample = trainingSamples != null && trainingSamples.Count > 0;
                return parsed;
            }
        }

        return GenerateExpertAnalysis(contact, chatHistory, moments, trainingSamples);
    }
}
