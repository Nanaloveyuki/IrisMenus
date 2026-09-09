[CmdletBinding()]
param([string]$UnityPath, [switch]$SkipGpu)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
& dotnet build (Join-Path $root 'tests\ApiConsumer\ApiConsumer.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'API consumer build failed.' }
& dotnet run --project (Join-Path $root 'tests\RuntimeChecks\RuntimeChecks.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Runtime checks failed.' }
foreach ($directory in @('About', '1.6\Defs', '1.6\Languages')) {
    foreach ($file in Get-ChildItem (Join-Path $root $directory) -Recurse -Filter *.xml) {
        [xml]$document = Get-Content -LiteralPath $file.FullName -Raw
    }
}
[xml]$en = Get-Content (Join-Path $root '1.6\Languages\English\Keyed\IrisMenus.xml') -Raw
[xml]$zh = Get-Content (Join-Path $root '1.6\Languages\ChineseSimplified\Keyed\IrisMenus.xml') -Raw
$english = @($en.LanguageData.ChildNodes | ForEach-Object Name)
$chinese = @($zh.LanguageData.ChildNodes | ForEach-Object Name)
if (Compare-Object $english $chinese) { throw 'Localization key mismatch.' }
if (@($english | Select-Object -Unique).Count -ne $english.Count) { throw 'Duplicate English key.' }
if (@($chinese | Select-Object -Unique).Count -ne $chinese.Count) { throw 'Duplicate Chinese key.' }
$used = @(Get-ChildItem (Join-Path $root 'Source') -Filter *.cs |
    Select-String -Pattern '"(IM_[A-Za-z]+)"' -AllMatches | ForEach-Object { $_.Matches } |
    ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
foreach ($key in $used) { if ($key -notin $english) { throw "Missing translation: $key" } }
Write-Host "XML/localization validation passed: $($english.Count) keys."
if (-not $SkipGpu) { & (Join-Path $PSScriptRoot 'build-shader-bundle.ps1') -UnityPath $UnityPath }
& (Join-Path $PSScriptRoot 'build-and-deploy.ps1') -SkipBuild -GameModPath (Join-Path $root 'tmp\deployment-smoke')
& git -C $root diff --check
if ($LASTEXITCODE -ne 0) { throw 'Diff check failed.' }
