using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WxApi.Analyses.Models;

public class ContactItem
{
    public string Wxid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
    public string Tier { get; set; } = "C"; // S, A, B, C, D
    public string TierLabel { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING"; // PENDING, LINKED, STARRED, IGNORED
    public string LastActive { get; set; } = "近期";
    public string Avatar { get; set; } = string.Empty;
    public string Cover { get; set; } = string.Empty;
}

public class ChatMessage
{
    public string Sender { get; set; } = "peer"; // "self" or "peer"
    public string Time { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string MsgType { get; set; } = "text";
}

public class MomentComment
{
    public string User { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public class MomentItem
{
    public string Date { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string> Images { get; set; } = new();
    public List<string> Likes { get; set; } = new();
    public List<MomentComment> Comments { get; set; } = new();
}

public class AiInsight
{
    public string PainPoint { get; set; } = string.Empty;
    public string Profile { get; set; } = string.Empty;
    public string InterestLevel { get; set; } = string.Empty;
    public string RecommendedPitch { get; set; } = string.Empty;
    public string TimingAdvice { get; set; } = string.Empty;
    public bool LearnedFromSample { get; set; } = false;
    public string? SampleSourceId { get; set; }
}

public class ContactDetailResponse
{
    public ContactItem Contact { get; set; } = new();
    public List<ChatMessage> ChatHistory { get; set; } = new();
    public List<MomentItem> Moments { get; set; } = new();
    public AiInsight AiInsight { get; set; } = new();
    public string? SnsNotice { get; set; }
}

public class DecisionRequest
{
    public string Action { get; set; } = string.Empty; // "LINKED", "STARRED", "IGNORED"
    public string? CustomPitch { get; set; }
}

public class DecisionRecord
{
    public string Wxid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // LINKED, STARRED, IGNORED
    public string Pitch { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string SavedTime => CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
}

public class SendMessageRequest
{
    public string Wxid { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class StatsOverview
{
    public int TotalContacts { get; set; }
    public int LinkedCount { get; set; }
    public int StarredCount { get; set; }
    public int IgnoredCount { get; set; }
    public int PendingCount { get; set; }
    public Dictionary<string, int> TierDistribution { get; set; } = new();
}

public class AiTrainingSample
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Wxid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public string OriginalPitch { get; set; } = string.Empty;
    public string EditedPitch { get; set; } = string.Empty;
    public string? PainPoint { get; set; }
    public string? Profile { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string SavedTime => CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
}

public class SaveAiTrainingSampleRequest
{
    public string Wxid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public string OriginalPitch { get; set; } = string.Empty;
    public string EditedPitch { get; set; } = string.Empty;
    public string? PainPoint { get; set; }
    public string? Profile { get; set; }
}

public class ExcelFileInfo
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FileSizeFormatted { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public string LastModifiedFormatted => LastModified.ToString("yyyy-MM-dd HH:mm:ss");
    public bool IsCurrent { get; set; }
    public string HookUrl { get; set; } = string.Empty;
    public string AccountKey { get; set; } = string.Empty;
}

public class SelectExcelRequest
{
    public string? FileName { get; set; }
    public string? FilePath { get; set; }
}

