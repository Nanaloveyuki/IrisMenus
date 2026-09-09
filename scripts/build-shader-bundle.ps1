[CmdletBinding()]
param([string]$UnityPath)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $UnityPath) {
    $hub = Join-Path $env:APPDATA 'UnityHub\editors-v2.json'
    if (Test-Path -LiteralPath $hub) {
        $editors = Get-Content -LiteralPath $hub -Raw | ConvertFrom-Json
        $UnityPath = @($editors.data | Where-Object version -Like '2022.3.35*' | ForEach-Object location | Where-Object { Test-Path -LiteralPath $_ }) | Select-Object -First 1
    }
}
if (-not $UnityPath -or -not (Test-Path -LiteralPath $UnityPath)) { throw 'Specify -UnityPath for a Unity 2022.3.35 editor.' }
$project = Join-Path $root 'Tools\Unity'
$runtime = Join-Path $project 'Assets\Runtime'
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
foreach ($file in @('FrostPipeline.cs', 'MenuLayout.cs')) {
    Copy-Item -LiteralPath (Join-Path $root "Source\$file") -Destination $runtime -Force
}
$log = Join-Path $root 'tmp\unity-build.log'
New-Item -ItemType Directory -Path (Split-Path $log) -Force | Out-Null
$arguments = @('-batchmode', '-force-d3d11', '-projectPath', ('"' + $project + '"'), '-executeMethod', 'BundleBuilder.Build', '-buildTarget', 'Win64', '-logFile', ('"' + $log + '"'))
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -PassThru -WindowStyle Hidden
if (-not $process.WaitForExit(600000)) {
    Stop-Process -Id $process.Id -Force
    throw "Unity build timed out. Log: $log"
}
if ($process.ExitCode -ne 0) {
    Select-String -LiteralPath $log -Pattern 'error CS|Shader error|Exception|Validation failed|could not be loaded' -Context 1,4
    throw "Unity build failed: $($process.ExitCode)"
}
Get-Content -LiteralPath (Join-Path $root 'tmp\unity-validation.txt')
