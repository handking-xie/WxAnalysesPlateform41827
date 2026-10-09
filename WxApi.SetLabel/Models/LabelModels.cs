using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WxApi.SetLabel.Models;

public class BaseResponse
{
    [JsonPropertyName("ret")]
    public int Ret { get; set; }

    [JsonPropertyName("errMsg")]
    public object? ErrMsg { get; set; }
}

public class LabelPair
{
    [JsonPropertyName("labelName")]
    public string LabelName { get; set; } = string.Empty;

    [JsonPropertyName("labelId")]
    public int LabelId { get; set; }
}

public class GetLabelListsResponse
{
    [JsonPropertyName("baseResponse")]
    public BaseResponse? BaseResponse { get; set; }

    [JsonPropertyName("labelCount")]
    public int LabelCount { get; set; }

    [JsonPropertyName("labelPairList")]
    public List<LabelPair>? LabelPairList { get; set; }
}

public class AddLabelRequest
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;
}

public class AddLabelResponse
{
    [JsonPropertyName("baseResponse")]
    public BaseResponse? BaseResponse { get; set; }

    [JsonPropertyName("labelCount")]
    public int LabelCount { get; set; }

    [JsonPropertyName("labelPairList")]
    public List<LabelPair>? LabelPairList { get; set; }
}

public class ModifyContactLabelRequest
{
    [JsonPropertyName("wxids")]
    public string Wxids { get; set; } = string.Empty;

    [JsonPropertyName("labelId")]
    public string LabelId { get; set; } = string.Empty;
}

public class ContactDetails
{
    [JsonPropertyName("alias")]
    public string? Alias { get; set; }

    [JsonPropertyName("labelIdlist")]
    public string? LabelIdlist { get; set; }
}

public class GetContactFastResponse
{
    [JsonPropertyName("contact")]
    public ContactDetails? Contact { get; set; }

    [JsonPropertyName("ret")]
    public int Ret { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }
}

public class ContactInfo
{
    public string Wxid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
    public string IntentionGrade { get; set; } = string.Empty;
    public List<string> ExistingLabelIds { get; set; } = new();
    public bool AlreadyHasTargetLabel { get; set; }
}

public class SheetLabelPlan
{
    public string SheetName { get; set; } = string.Empty;
    public string TargetLabelName { get; set; } = string.Empty;
    public int? LabelId { get; set; }
    public bool LabelExisted { get; set; }
    public List<ContactInfo> Contacts { get; set; } = new();
}

public class ExecutionRecord
{
    public string SheetName { get; set; } = string.Empty;
    public string LabelName { get; set; } = string.Empty;
    public int LabelId { get; set; }
    public string Wxid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Success { get; set; }
    public bool Skipped { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
