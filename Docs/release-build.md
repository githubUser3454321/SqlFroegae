# Release-Build (API + Desktop)

Diese Anleitung erzeugt:

1. eine **selbstenthaltende API-EXE** (`SqlFroega.Api.exe`)
2. ein **installierbares Desktop-Paket (MSIX)** für `SqlFrögä`

> Warum MSIX statt „nackter sqlFroega.exe“?
> Für WinUI 3 ist ein Installer/Package der Best-Practice-Weg (saubere Installation, Updates, Deinstallation, Abhängigkeiten).

## Voraussetzungen

- Windows 10/11
- .NET 8 SDK
- Für Desktop-Paket: Visual Studio 2022 mit *Windows App SDK / MSIX Packaging Tools*
- Für signiertes MSIX: Code-Signing-Zertifikat (PFX)

## 1) API als EXE veröffentlichen

PowerShell im Repo-Root:

```powershell
dotnet publish .\SqlFroega.Api\SqlFroega.Api.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o .\artifacts\api\win-x64
```

Ergebnis:

- `artifacts/api/win-x64/SqlFroega.Api.exe`
- zusätzliche Dateien (z. B. `appsettings*.json`) für den Betrieb

## 2) SqlFrögä als Installer (MSIX)

### A) Unsigniertes Paket (lokal/test)

```powershell
dotnet publish .\SqlFrögä\SqlFroega.csproj `
  -c Release `
  -r win-x64 `
  -p:WindowsPackageType=MSIX `
  -p:GenerateAppxPackageOnBuild=true `
  -p:AppxPackageSigningEnabled=false `
  -o .\artifacts\desktop\win-x64
```

### B) Signiertes Paket (Distribution)

```powershell
dotnet publish .\SqlFrögä\SqlFroega.csproj `
  -c Release `
  -r win-x64 `
  -p:WindowsPackageType=MSIX `
  -p:GenerateAppxPackageOnBuild=true `
  -p:AppxPackageSigningEnabled=true `
  -p:PackageCertificateKeyFile="C:\certs\sqlfroega-signing.pfx" `
  -p:PackageCertificatePassword="<PASSWORT>" `
  -o .\artifacts\desktop\win-x64
```

Typisches Ergebnis:

- `.msix` oder `.msixbundle` im Publish-Ausgabeordner
- Diese Datei ist der Installer für Endanwender

## Release-Empfehlung

- **API** als Service betreiben (Windows Service, IIS oder Container) statt auf Client-PCs verteilen.
- **Desktop** als MSIX ausliefern (einheitlicher Installer, saubere Updates).
- Release-Artefakte pro Version in `artifacts/<version>/...` ablegen.

## Optional: Beide Artefakte in einem Lauf bauen

Beispielskript:

```powershell
./scripts/build-release.ps1 -Runtime win-x64
```

