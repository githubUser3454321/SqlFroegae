param(
    [string]$Runtime = "win-x64",
    [switch]$SignDesktopPackage,
    [string]$CertificatePath,
    [string]$CertificatePassword
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

$desktopArgs = @(
  "publish", (Join-Path $root "SqlFrögä/SqlFroega.csproj"),
  "-c", "Release",
  "-r", $Runtime,
  "-p:WindowsPackageType=MSIX",
  "-p:GenerateAppxPackageOnBuild=true",
  "-o", $desktopOut
)

if ($SignDesktopPackage) {
    if ([string]::IsNullOrWhiteSpace($CertificatePath) -or [string]::IsNullOrWhiteSpace($CertificatePassword)) {
        throw "If -SignDesktopPackage is set, both -CertificatePath and -CertificatePassword are required."
    }

    Write-Host "==> Building Desktop MSIX ($Runtime, signed)"
    $desktopArgs += "-p:AppxPackageSigningEnabled=true"
    $desktopArgs += "-p:PackageCertificateKeyFile=$CertificatePath"
    $desktopArgs += "-p:PackageCertificatePassword=$CertificatePassword"
}
else {
    Write-Host "==> Building Desktop MSIX ($Runtime, unsigned)"
    $desktopArgs += "-p:AppxPackageSigningEnabled=false"
}

dotnet @desktopArgs

Write-Host "Done. Artifacts in: $outBase"
