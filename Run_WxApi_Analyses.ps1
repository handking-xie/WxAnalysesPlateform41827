# WxApi.Analyses 一键启动脚本
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "🚀 正在启动 WxApi.Analyses 微信好友微创业意向全景分析工作台..." -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Green

$CurrentDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $CurrentDir

# 检查当前端口是否被占用，如有则清理
$port = 5288
$tcp = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
if ($tcp) {
    Write-Host "端口 $port 正在被使用，正在尝试释放..." -ForegroundColor Yellow
    $pids = $tcp | Select-Object -ExpandProperty OwningProcess -Unique | Where-Object { $_ -gt 4 }
    foreach ($p in $pids) {
        Stop-Process -Id $p -Force -ErrorAction SilentlyContinue
    }
}

# 启动浏览器
Start-Process "http://localhost:5288"

# 启动 Web 服务
dotnet run --project "$CurrentDir\WxApi.Analyses\WxApi.Analyses.csproj" --urls "http://localhost:5288"
