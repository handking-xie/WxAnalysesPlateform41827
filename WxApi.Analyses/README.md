# WxApi.Analyses - 微信好友微创业意向全景分析工作台

基于 **.NET 9 + ASP.NET Core** 构建的高性能微信私域客群意向分析系统。配合微信底层 Hook 接口（WeChat 4.1.8.27 64位），实现微信好友微创业/副业意向的深度全景洞察与触达决策。

---

## 🌟 核心功能

1. **Excel 全景好友自动加载与智能排除**
   - 自动解析 `samuelsurry_全部好友副业微创业意向全景透视表.xlsx`；
   - **严格排除「E级-删除拉黑或僵尸粉」**（共 1,251 人），精准载入有效意向好友 **1,270 人**；
   - 快速意向分级筛选（S级-强烈意向 104人、A级-明确 168人、B级 211人、C级 373人、D级 414人）。

2. **双视窗交互：微信聊天历史 + 微信朋友圈相册**
   - **微信对话框**：完美还原微信 PC 与移动端对话气泡、时间戳与聊天记录，支持即时发送消息（调用微信 Hook `/api/send_text_msg`）；
   - **朋友圈时间线**：展示好友封面、头像、生活/副业动态、九宫格多图、点赞列表与互动评论，并支持一键向微信 Hook 请求刷新相册（`/api/sns_get_user_page2`）。

3. **AI 智能意向画像与商机洞察**
   - 结合好友背景、历史对话、朋友圈动态，提炼客户核心痛点与人群特征；
   - 自动生成最合适发送给对方的**破冰/链接触达消息**；
   - 支持一键复制、一键填入发送框；
   - 支持在 `appsettings.json` 中配置外部大模型 API（如 DeepSeek / Qwen / OpenAI）。

4. **决策三连击与本地持久化存储**
   - 🟢 **【链接】**：加入后继链接列表，保存至本地文件 `data/linked_contacts.json`，并自动平滑切换到下一位好友；
   - ⭐ **【重点】**：加入重点跟进列表，保存至本地文件 `data/starred_contacts.json`；
   - ⚪ **【忽略】**：加入忽略列表，保存至本地文件 `data/ignored_contacts.json`；
   - 顶栏配备【查看本地文件】弹窗，支持全量预览和导出下载。

---

## 🚀 快速启动

在终端中执行根目录下的一键脚本：
```powershell
.\Run_WxApi_Analyses.ps1
```

或直接进入项目目录运行：
```powershell
cd WxApi.Analyses
dotnet run
```

运行后将在浏览器中自动打开：
👉 **http://localhost:5288**

---

## ⚙️ 配置文件说明 (`appsettings.json`)

```json
{
  "AppConfig": {
    "ExcelPath": "../samuelsurry_全部好友副业微创业意向全景透视表.xlsx",
    "WeChatHookUrl": "http://127.0.0.1:19088",
    "DataDirectory": "data",
    "ExcludedSheets": [ "E级-删除拉黑或僵尸粉", "全量好友" ],
    "AiSettings": {
      "EnableExternalApi": false,
      "ApiEndpoint": "https://api.openai.com/v1/chat/completions",
      "ApiKey": "",
      "Model": "gpt-4o-mini"
    }
  }
}
```

---

## 📁 本地数据文件路径

当您在页面上对好友进行【链接】、【重点】或【忽略】操作时，数据将实时持久化保存至：
- `WxApi.Analyses/data/linked_contacts.json` (后继链接列表)
- `WxApi.Analyses/data/starred_contacts.json` (重点跟进列表)
- `WxApi.Analyses/data/ignored_contacts.json` (忽略列表)
