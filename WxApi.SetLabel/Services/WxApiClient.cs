using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WxApi.SetLabel.Models;

namespace WxApi.SetLabel.Services;

public class WxApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public WxApiClient(string baseUrl = "http://127.0.0.1:19088")
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    public bool IsAvailable { get; private set; }

    /// <summary>
    /// 检查微信 Hook HTTP 服务连通性
    /// </summary>
    public async Task<(bool Available, string Message)> CheckHealthAsync()
    {
        try
        {
            var content = new StringContent("{}", Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/api/get_label_lists", content);
            if (response.IsSuccessStatusCode)
            {
                IsAvailable = true;
                return (true, $"服务正常响应 (HTTP {(int)response.StatusCode} {response.StatusCode})");
            }
            IsAvailable = false;
            return (false, $"服务返回状态码: {response.StatusCode}");
        }
        catch (HttpRequestException ex)
        {
            IsAvailable = false;
            return (false, $"连接被拒绝或服务未就绪: {ex.Message}");
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            return (false, $"检查异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取微信已有所有标签列表
    /// POST http://127.0.0.1:19088/api/get_label_lists
    /// </summary>
    public async Task<List<LabelPair>> GetLabelListsAsync()
    {
        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("/api/get_label_lists", content);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<GetLabelListsResponse>(body, _jsonOptions);
        return result?.LabelPairList ?? new List<LabelPair>();
    }

    /// <summary>
    /// 添加新标签
    /// POST http://127.0.0.1:19088/api/add_label
    /// Request: {"label": "标签名字"}
    /// Response: {"baseResponse":{"ret":0},"labelCount":1,"labelPairList":[{"labelName":"我的标签","labelId":12}]}
    /// </summary>
    public async Task<(bool Success, int? LabelId, string RawResponse)> AddLabelAsync(string labelName)
    {
        var reqObj = new AddLabelRequest { Label = labelName };
        var jsonStr = JsonSerializer.Serialize(reqObj);
        var content = new StringContent(jsonStr, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/api/add_label", content);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return (false, null, $"HTTP {(int)response.StatusCode}: {body}");
        }

        try
        {
            var result = JsonSerializer.Deserialize<AddLabelResponse>(body, _jsonOptions);
            if (result?.LabelPairList != null && result.LabelPairList.Count > 0)
            {
                var matched = result.LabelPairList.FirstOrDefault(l => l.LabelName == labelName) 
                              ?? result.LabelPairList[0];
                return (true, matched.LabelId, body);
            }

            // 若返回未带 pairList，重新拉取标签列表查找
            var allLabels = await GetLabelListsAsync();
            var found = allLabels.FirstOrDefault(l => l.LabelName == labelName);
            if (found != null)
            {
                return (true, found.LabelId, body);
            }

            return (false, null, body);
        }
        catch (Exception ex)
        {
            return (false, null, $"解析响应异常: {ex.Message}, Body: {body}");
        }
    }

    /// <summary>
    /// 为好友设置标签
    /// POST http://127.0.0.1:19088/api/modify_contact_label
    /// Request: {"wxids": "wxid_xxx", "labelId": "12"}
    /// </summary>
    public async Task<(bool Success, string RawResponse)> ModifyContactLabelAsync(string wxid, string labelId)
    {
        var reqObj = new ModifyContactLabelRequest
        {
            Wxids = wxid,
            LabelId = labelId
        };
        var jsonStr = JsonSerializer.Serialize(reqObj);
        var content = new StringContent(jsonStr, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/api/modify_contact_label", content);
        var body = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            return (true, body);
        }

        return (false, $"HTTP {(int)response.StatusCode}: {body}");
    }

    /// <summary>
    /// 快速查找联系人信息（获取 contact.labelIdlist 等）
    /// POST http://127.0.0.1:19088/api/get_contact_fast
    /// </summary>
    public async Task<(bool Success, string LabelIdList, string? Error)> GetContactFastAsync(string wxid)
    {
        try
        {
            var reqObj = new { wxid };
            var jsonStr = JsonSerializer.Serialize(reqObj);
            var content = new StringContent(jsonStr, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/get_contact_fast", content);
            if (!response.IsSuccessStatusCode)
            {
                return (false, string.Empty, $"HTTP {(int)response.StatusCode}");
            }

            var body = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<GetContactFastResponse>(body, _jsonOptions);
            return (true, result?.Contact?.LabelIdlist ?? string.Empty, null);
        }
        catch (Exception ex)
        {
            return (false, string.Empty, ex.Message);
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
