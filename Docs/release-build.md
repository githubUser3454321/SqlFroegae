# Release-Build (API + Desktop)

Diese Anleitung erzeugt:

1. eine **selbstenthaltende API-EXE** (`SqlFroega.Api.exe`)
2. eine **Desktop-Auslieferung ohne MSIX** als **portable EXE-Ordner** (ohne Signatur)
3. optional weiterhin ein **MSIX-Paket** (falls später wieder Signierung möglich ist)

> Da deine aktuelle MSIX-Variante ohne Signatur nicht zuverlässig einsetzbar ist, ist der pragmatische Weg aktuell: **Unpackaged / self-contained Publish** und den kompletten Ausgabeordner versenden.

## Voraussetzungen

- Windows 10/11
- .NET 8 SDK
- Für Desktop-Builds: Visual Studio 2022 Build Tools (mit Windows SDK)

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

## 2) Desktop als „alles drin“-EXE-Bundle bauen (ohne MSIX)

> Ziel: Eine Auslieferung, die ohne extra Runtime-Installation startet und einfach als kompletter Ordner (oder ZIP) verschickt werden kann.

### Schritt-für-Schritt

1. **PowerShell im Repo-Root öffnen**.
2. **Desktop-App self-contained publishen (unpackaged):**

```powershell
dotnet publish .\SqlFrögä\SqlFroega.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:WindowsPackageType=None `
  -p:PublishSingleFile=false `
  -p:WindowsAppSDKSelfContained=true `
  -o .\artifacts\desktop-portable\win-x64
```

3. **Ausgabe prüfen** in `artifacts\desktop-portable\win-x64`.
   - Dort liegt die startbare `SqlFroega.exe`.
   - Außerdem liegen dort alle benötigten DLLs/Runtime-Dateien.
4. **Wichtig für Versand:** Nicht nur die EXE verschicken, sondern **den kompletten Ordnerinhalt**.
5. **Optional als ZIP verpacken** (empfohlen für Weitergabe):

```powershell
Compress-Archive `
  -Path .\artifacts\desktop-portable\win-x64\* `
  -DestinationPath .\artifacts\SqlFroega-desktop-portable-win-x64.zip `
  -Force
```

6. **Empfänger-Anleitung:** ZIP entpacken und `SqlFroega.exe` starten.

### Hinweise

- Diese Variante braucht **keine MSIX-Signatur**.
- Da es kein klassischer Installer ist, gibt es kein automatisches Setup/Uninstall über Windows „Apps & Features“.
- Für maximale Kompatibilität kann zusätzlich `win-x86` oder `win-arm64` gebaut werden (analoger Befehl mit anderem `-r`).

## 3) Optional: SqlFrögä weiterhin als MSIX (falls wieder verfügbar)

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

## Release-Empfehlung

- **API** als Service betreiben (Windows Service, IIS oder Container) statt auf Client-PCs verteilen.
- **Desktop aktuell** als portable self-contained Build (`desktop-portable`) ausliefern.
- Falls später möglich: wieder auf **signiertes MSIX** für saubere Installation/Updates wechseln.
- Release-Artefakte pro Version in `artifacts/<version>/...` ablegen.

## Optional: Beide Artefakte in einem Lauf bauen

Beispielskript:

```powershell
./scripts/build-release.ps1 -Runtime win-x64
```

## Optional: Test-Zertifikat erstellen (nur intern)

```powershell
$cert = New-SelfSignedCertificate `
  -Type CodeSigningCert `
  -Subject "CN=SqlFroega Test" `
  -CertStoreLocation "Cert:\CurrentUser\My" `
  -KeyExportPolicy Exportable `
  -HashAlgorithm sha256 `
  -KeyLength 2048

$pwd = ConvertTo-SecureString -String "passwort123" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath .\SqlFroegaTest.pfx -Password $pwd
Export-Certificate -Cert $cert -FilePath .\SqlFroegaTest.cer
```
