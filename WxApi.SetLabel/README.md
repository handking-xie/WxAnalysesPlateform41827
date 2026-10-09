# WxApi.SetLabel - 微信 4.x 好友批量标签同步工具

针对 **微信 PC 4.x (4.1.8.27 64位)** Hook 接口开发的控制台小工具。支持读取意向透视表 Excel 文件，自动在微信中创建对应的分类标签，并批量为目标联系人设置指定标签。

---

## 核心功能与工作流

1. **自动读取 Excel 透视表**：
   - 自动定位透视表：优先读取当前运行目录下的 `surry_全部好友副业微创业意向全景透视表.xlsx`（兼容 `01_samuelsurry_全部好友副业微创业意向全景透视表.xlsx`）。
   - 解析各个分类 Sheet（如 `S级-强烈意向`、`A级-明确意向`、`B级-潜在意向`、`C级-观望中立`、`D级-拒绝或偏好固定`、`E级-删除拉黑或僵尸粉`）。
   - 默认智能跳过概览总表 `全量2529位好友分析`（可通过 `--all` 参数选择包含）。

2. **微信标签同步与自愈**：
   - 调用 `POST /api/get_label_lists` 查询微信中已有的标签与对应 `labelId`。
   - 若目标标签尚未创建，自动调用 `POST /api/add_label` 新建标签并获取新分配的 `labelId`。
   - 若已存在，则复用现有 `labelId`。

3. **批量打标与频率保护**：
   - 针对各 Sheet 内的好友列表，调用 `POST /api/modify_contact_label`：
     ```json
     {
       "wxids": "wxid_xxxx",
       "labelId": "12"
     }
     ```
   - 内置请求间隔延时保护（默认间隔 5000ms / 5秒，可在 `appsettings.json` 或命令行修改），避免瞬时并发触发微信风控机制。每次向微信发起操作（创建标签或设置联系人标签）后均严格休眠 5 秒，再进行下一次 POST。

4. **审计日志与执行统计**：
   - 实时终端进度条（基于 Spectre.Console 渲染）。
   - 每次运行自动生成附带时间戳的完整审计 JSON 报告文件（如 `set_label_report_YYYYMMDD_HHmmss.json`）。

---

## 项目结构

```
d:/WxHookSource41827/WxApi.SetLabel/
├── Config/
│   └── AppConfig.cs               # 配置加载与管理
├── Models/
│   └── LabelModels.cs             # Hook API 请求/响应 DTO 与联系人实体
├── Services/
│   ├── WxApiClient.cs             # 微信 Hook HTTP 客户端封装
│   ├── ExcelReaderService.cs      # Excel 解析服务 (MiniExcel)
│   └── LabelSyncEngine.cs         # 核心同步引擎与审计报表生成
├── appsettings.json               # 配置文件
├── Program.cs                     # 控制台入口与交互/命令行处理
└── WxApi.SetLabel.csproj          # .NET 9 项目文件
```

---

## 快速运行

### 1. 前置条件
确保微信已登录，并且通过注入器挂载了 `libGLESv1.dll`，本地接口处于监听状态：
```
http://127.0.0.1:19088
```

### 2. 交互模式运行
在项目目录下直接运行：
```powershell
cd d:\WxHookSource41827\WxApi.SetLabel
dotnet run
```
程序会自动检测环境并列出各 Sheet 分类人数，等待您手动确认后执行。

### 3. 命令行参数运行

| 参数 | 说明 | 示例 |
| :--- | :--- | :--- |
| `--dry-run` | 模拟演练模式（不真实修改微信标签） | `dotnet run -- --dry-run` |
| `-y, --run` | 自动确认并直接运行 | `dotnet run -- -y` |
| `--sheet <name>` | 仅处理指定的单一 Sheet | `dotnet run -- --sheet "S级-强烈意向"` |
| `--delay <ms>` | 自定义每个好友设置标签的间隔（毫秒） | `dotnet run -- --delay 500` |
| `--api <url>` | 自定义微信 Hook HTTP 接口地址 | `dotnet run -- --api http://127.0.0.1:19088` |
| `--excel <path>` | 指定 Excel 文件路径 | `dotnet run -- --excel "D:\path\to\file.xlsx"` |
| `--all` | 包含 `全量2529位好友分析` 概览表 | `dotnet run -- --all` |
| `-h, --help` | 查看帮助选项 | `dotnet run -- --help` |

---

## 示例测试命令

```powershell
# 1. 模拟运行单个 S 级意向分组（无风险测试）
dotnet run -- --dry-run --sheet "S级-强烈意向" -y

# 2. 真实批量打标（微信服务启动后执行）
dotnet run -- -y
```
