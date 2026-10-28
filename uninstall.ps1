# AutoDayNight 卸载脚本
Stop-Process -Name 'AutoDayNight' -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 400
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'AutoDayNight' -ErrorAction SilentlyContinue
$dest = Join-Path $env:LOCALAPPDATA 'AutoDayNight'
Remove-Item -Recurse -Force $dest -ErrorAction SilentlyContinue
Write-Host '已卸载 AutoDayNight（开机自启已移除，程序目录已删除）。'
