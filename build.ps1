param([switch]$Test, [switch]$Package)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw 'The .NET Framework 4.x compiler was not found.' }
$sources = @(Get-ChildItem (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
$out = Join-Path $root 'AutoDayNight.exe'
& $csc /nologo /target:winexe /optimize+ /platform:anycpu /main:AutoDayNight.Program "/out:$out" /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll @sources
if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }
Write-Host "Built $out"
$artifacts = Join-Path $root 'artifacts'
if ($Test) {
    New-Item -ItemType Directory -Force $artifacts | Out-Null
    $testExe = Join-Path $artifacts 'AutoDayNight.Tests.exe'
    $testSources = @(Get-ChildItem (Join-Path $root 'tests') -Filter '*.cs' | ForEach-Object { $_.FullName })
    & $csc /nologo /target:exe /optimize+ /platform:anycpu /main:AutoDayNight.Tests.TestRunner "/out:$testExe" /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll @sources @testSources
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    $previous = $env:AUTODAYNIGHT_DATA_DIR
    $env:AUTODAYNIGHT_DATA_DIR = Join-Path $artifacts ('testdata-' + [guid]::NewGuid().ToString('N'))
    try { & $testExe; if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' } }
    finally { $env:AUTODAYNIGHT_DATA_DIR = $previous }
}
if ($Package) {
    New-Item -ItemType Directory -Force $artifacts | Out-Null
    $stage = Join-Path $artifacts 'AutoDayNight-v1.0.0-windows'
    New-Item -ItemType Directory -Force $stage | Out-Null
    @('AutoDayNight.exe','config.ini','install.ps1','install.cmd','uninstall.ps1','uninstall.cmd','README.md','README.en.md','LICENSE') | ForEach-Object { Copy-Item (Join-Path $root $_) $stage -Force }
    $zip = Join-Path $artifacts 'AutoDayNight-v1.0.0-windows.zip'
    Compress-Archive -Path $stage -DestinationPath $zip -Force
    $hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -Path (Join-Path $artifacts 'SHA256SUMS.txt') -Value ($hash + '  ' + (Split-Path $zip -Leaf)) -Encoding ASCII
    Write-Host "Packaged $zip"
}
