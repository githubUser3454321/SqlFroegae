param(
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$outBase = Join-Path $root "artifacts"
$apiOut = Join-Path $outBase "api/$Runtime"
$desktopOut = Join-Path $outBase "desktop/$Runtime"

Write-Host "==> Building API ($Runtime)"
dotnet publish (Join-Path $root "SqlFroega.Api/SqlFroega.Api.csproj") `
  -c Release `
  -r $Runtime `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o $apiOut

Write-Host "==> Building Desktop MSIX ($Runtime, unsigned)"
dotnet publish (Join-Path $root "SqlFrögä/SqlFroega.csproj") `
  -c Release `
  -r $Runtime `
  -p:WindowsPackageType=MSIX `
  -p:GenerateAppxPackageOnBuild=true `
  -p:AppxPackageSigningEnabled=false `
  -o $desktopOut

Write-Host "Done. Artifacts in: $outBase"
