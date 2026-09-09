[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$GameModPath,
    [string]$UnityPath,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repoRoot 'Source\IrisMenus.csproj'
if (-not $SkipBuild) {
    & dotnet build $project --configuration $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
    & (Join-Path $PSScriptRoot 'build-shader-bundle.ps1') -UnityPath $UnityPath
}
$assembly = Join-Path $repoRoot '1.6\Assemblies\IrisMenus.dll'
if (-not (Test-Path -LiteralPath $assembly)) { throw "Assembly not found: $assembly" }
$bundle = Join-Path $repoRoot '1.6\AssetBundles\irismenus_frost'
if (-not (Test-Path -LiteralPath $bundle)) { throw "Shader bundle not found: $bundle" }
if (-not $GameModPath) {
    Write-Host "Built: $assembly"
    return
}

$target = [IO.Path]::GetFullPath($GameModPath)
if ($target.TrimEnd('\', '/') -eq $repoRoot.TrimEnd('\', '/')) {
    throw 'Deployment target must not be the source repository.'
}
$targetAbout = Join-Path $target 'About\About.xml'
if (Test-Path -LiteralPath $target) {
    if (-not (Test-Path -LiteralPath $targetAbout)) { throw "Target has no About.xml: $target" }
    [xml]$metadata = Get-Content -LiteralPath $targetAbout -Raw
    if ($metadata.ModMetaData.packageId -ne 'Nanaloveyuki.IrisMenus') {
        throw "Target is not IrisMenus: $target"
    }
}

$files = @('About\About.xml', 'LICENSE', 'guide.md', 'guide_agents.md', '1.6\Assemblies\IrisMenus.dll', '1.6\AssetBundles\irismenus_frost')
foreach ($directory in @('1.6\Defs', '1.6\Languages')) {
    $files += Get-ChildItem -LiteralPath (Join-Path $repoRoot $directory) -Recurse -File |
        ForEach-Object { $_.FullName.Substring($repoRoot.Length + 1) }
}
foreach ($relative in $files) {
    $source = Join-Path $repoRoot $relative
    $destination = Join-Path $target $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Force
    if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) {
        throw "Deployment verification failed: $destination"
    }
}
Write-Host "Deployed and verified $($files.Count) files: $target"
