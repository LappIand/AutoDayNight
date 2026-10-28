# Install/upgrade AutoDayNight for the current user. No elevation required.
$ErrorActionPreference = 'Stop'
$src = $PSScriptRoot
$dest = Join-Path $env:LOCALAPPDATA 'AutoDayNight'
$exe = Join-Path $dest 'AutoDayNight.exe'
$sourceExe = Join-Path $src 'AutoDayNight.exe'
if (-not (Test-Path $sourceExe)) { throw 'Build first (build.cmd) or download the release ZIP.' }
# Stop BEFORE replacing an in-use executable. Never overwrite existing preferences.
Get-Process -Name 'AutoDayNight' -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe } | Stop-Process -Force
Start-Sleep -Milliseconds 300
New-Item -ItemType Directory -Force -Path $dest | Out-Null
if ([IO.Path]::GetFullPath($sourceExe) -ne [IO.Path]::GetFullPath($exe)) { Copy-Item -Force $sourceExe $exe }
$config = Join-Path $dest 'config.ini'
if (-not (Test-Path $config) -and (Test-Path (Join-Path $src 'config.ini'))) { Copy-Item (Join-Path $src 'config.ini') $config }
$run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
New-Item -Path $run -Force | Out-Null
Set-ItemProperty -Path $run -Name 'AutoDayNight' -Value ('"' + $exe + '"')
Start-Process -FilePath $exe
Write-Host "Installed: $exe"
Write-Host 'First run: choose your location. Low-battery automatic shutdown is opt-in.'
