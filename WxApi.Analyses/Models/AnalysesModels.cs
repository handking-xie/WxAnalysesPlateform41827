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
    public string? RealWxid { get; set; }
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
    public long CommentId { get; set; }
    public string User { get; set; } = string.Empty; // 评论者昵称
    public string? UserName { get; set; } // 评论者 wxid
    public string Text { get; set; } = string.Empty; // 评论内容
    public string? ReplyToUser { get; set; } // 被回复者昵称
    public string? ReplyToUserName { get; set; } // 被回复者 wxid
    public long ReplyCommentId { get; set; } // 被回复的评论 ID
    public string? Time { get; set; } // 评论时间
    public bool IsSelf { get; set; } // 是否为本人评论
}

public class MomentItem
{
    public string Id { get; set; } = string.Empty; // 朋友圈唯一 sns_id
    public string Date { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string> Images { get; set; } = new();
    public List<string> Likes { get; set; } = new();
    public List<MomentComment> Comments { get; set; } = new();

    // 朋友圈发布者信息 (多发布者时间线支持)
    public string? UserName { get; set; } // 发布者 wxid (to_wxid)
    public string? Nickname { get; set; }
    public string? Avatar { get; set; }
    public string? Tier { get; set; }
    public string? Remark { get; set; }

    // 点赞与评论状态标记
    public bool IsLiked { get; set; }
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
}

public class SnsLikeRequest
{
    public string SnsId { get; set; } = string.Empty;
    public string ToWxid { get; set; } = string.Empty;
}

public class SnsCommentRequest
{
    public string SnsId { get; set; } = string.Empty;
    public string ToWxid { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}

public class SnsReplyRequest
{
    public string SnsId { get; set; } = string.Empty;
    public long CommentId { get; set; }
    public string Content { get; set; } = string.Empty;
}

public class SnsDeleteCommentRequest
{
    public string SnsId { get; set; } = string.Empty;
    public string CommentId { get; set; } = string.Empty;
}

public class SnsNextPageRequest
{
    public string LastItemId { get; set; } = string.Empty;
    public string? FirstPageMd5 { get; set; }
    public string? Session { get; set; }
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

