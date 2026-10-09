# WxApi.Analyses 启动脚本 (子目录版)
$port = 5288
$tcp = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
if ($tcp) {
    Get-Process -Id $tcp.OwningProcess -ErrorAction SilentlyContinue | Stop-Process -Force
}
Start-Process "http://localhost:5288"
dotnet run
