using System;
using System.Collections.Generic;

namespace WxApi.Analyses.Models;

public class GetContactFastResult
{
    public bool Success { get; set; }
    public string Wxid { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public string SmallHeadImgUrl { get; set; } = string.Empty;
    public string BigHeadImgUrl { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public int Sex { get; set; } // 1: 男, 2: 女, 0: 未知
    public string Signature { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Source { get; set; } = "HookApi"; // "HookApi" or "ExcelCache"
    public string? ErrorMessage { get; set; }
}

public class CandidateCvInfo
{
    public bool Found { get; set; }
    public string MatchedKey { get; set; } = string.Empty;
    public string MatchReason { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string FileSizeFormatted { get; set; } = string.Empty;
    public bool IsImage { get; set; }

    // 核心信息 (图2)
    public string Name { get; set; } = string.Empty;
    public string AvatarChar { get; set; } = "A";
    public string AvatarUrl { get; set; } = string.Empty;
    public string Age { get; set; } = "21岁";
    public string Gender { get; set; } = "保密";
    public string JobTitle { get; set; } = "会计主管";
    public string ExpectedSalary { get; set; } = "11-22K";
    public string MatchRate { get; set; } = "95% 匹配度";
    public string WorkStatus { get; set; } = "离校-正在找工作";
    public string Location { get; set; } = "上海";
    public string Hukou { get; set; } = string.Empty;
    public string PoliticalStatus { get; set; } = string.Empty;
    public string Education { get; set; } = "本科 · 重点院校";
    public string ExperienceYears { get; set; } = string.Empty;

    // 联系方式
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string IntendedCity { get; set; } = "广州 / 深圳";

    // 自我评价与优势 (图2 左上卡片)
    public string SelfEvaluation { get; set; } = string.Empty;
    public List<string> HighlightBullets { get; set; } = new();

    // 工作经历与项目经历 (图2 左下卡片)
    public List<CvWorkExperience> WorkExperiences { get; set; } = new();
    public List<CvProjectExperience> Projects { get; set; } = new();

    // 其他候选匹配文件 (便于多版本切换)
    public List<MatchedCvFileItem> AlternativeFiles { get; set; } = new();

    // 微信好友资料 (由 get_contact_fast 提供)
    public GetContactFastResult? FastInfo { get; set; }
    public ContactItem? Contact { get; set; }
}

public class CvWorkExperience
{
    public string Company { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string Salary { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class CvProjectExperience
{
    public string ProjectName { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class MatchedCvFileItem
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string SizeFormatted { get; set; } = string.Empty;
    public string Ext { get; set; } = string.Empty;
}

public class OpenFileRequest
{
    public string FilePath { get; set; } = string.Empty;
}
