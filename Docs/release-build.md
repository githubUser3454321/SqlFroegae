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

⚠️ Unsigned MSIX ist nur für sehr lokale Tests sinnvoll. Auf fremden Rechnern ist der Install-Button häufig deaktiviert.

### B) Signiertes Paket (Distribution, empfohlen)

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
- diese Datei ist der Installer für Endanwender

## 3) Problemfall: „Publisher unbekannt“ + Installieren-Button ausgegraut

Das passiert, wenn Windows der Signatur nicht vertraut (oder gar keine Signatur vorhanden ist).

### Lösung für interne Tests (ohne öffentliches Zertifikat)

1. Test-Zertifikat erstellen (auf Build-Rechner):

```powershell
$cert = New-SelfSignedCertificate `
  -Type CodeSigningCert `
  -Subject "CN=SqlFroega Test Publisher" `
  -KeyAlgorithm RSA `
  -KeyLength 2048 `
  -CertStoreLocation "Cert:\CurrentUser\My"

$pwd = ConvertTo-SecureString "Test123!" -AsPlainText -Force
Export-PfxCertificate -Cert $cert -FilePath "C:\certs\SqlFroega-Test.pfx" -Password $pwd
Export-Certificate -Cert $cert -FilePath "C:\certs\SqlFroega-Test.cer"
```

2. Mit diesem PFX paketieren (`PackageCertificateKeyFile`, `PackageCertificatePassword`).
3. Auf dem Zielrechner **vor der Installation** die `SqlFroega-Test.cer` in „Vertrauenswürdige Personen“ (oder „Vertrauenswürdige Stammzertifizierungsstellen“ für interne CA-Szenarien) importieren:

```powershell
certutil -addstore TrustedPeople .\SqlFroega-Test.cer
```

4. Danach MSIX erneut öffnen/installieren.

### Zusätzlich prüfen, wenn der Button weiterhin deaktiviert ist

- **Sideloading/Developer Mode** erlaubt?  
  *Einstellungen → Datenschutz & Sicherheit → Für Entwickler*
- Installation per PowerShell testen (liefert klarere Fehler):

```powershell
Add-AppxPackage .\SqlFroega.msix
```

- Wenn Abhängigkeiten fehlen (z. B. Windows App Runtime), diese mitliefern oder per AppInstaller installieren.

## 4) Release-Empfehlung

- **API** als Service betreiben (Windows Service, IIS oder Container) statt auf Client-PCs verteilen.
- **Desktop** als **signiertes** MSIX ausliefern (einheitlicher Installer, saubere Updates).
- Für produktive Verteilung ein Zertifikat nutzen, dem Zielgeräte bereits vertrauen (öffentliche CA oder Unternehmens-PKI via GPO/Intune).

## Optional: Beide Artefakte in einem Lauf bauen

Beispielskript:

```powershell
# Unsigned (nur lokal)
./scripts/build-release.ps1 -Runtime win-x64

# Signed (für Verteilung)
./scripts/build-release.ps1 -Runtime win-x64 `
  -SignDesktopPackage `
  -CertificatePath "C:\certs\sqlfroega-signing.pfx" `
  -CertificatePassword "<PASSWORT>"
```

