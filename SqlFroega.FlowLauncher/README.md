# SqlFroega.FlowLauncher

FlowLauncher-Plugin (C#) für die bestehende `SqlFroega.Api`.

## v1 Features

- Skript-Suche via `GET /api/v1/scripts`
- `Copy SQL` via `GET /api/v1/scripts/{id}`
- `Copy Rendered SQL` via `POST /api/v1/render/{customerCode}`
- Login + Token-Refresh (`/auth/login`, `/auth/refresh`)
- Header-Support für `X-Tenant-Context` und `X-Correlation-Id`
- Kurzzeit-Cache für Suchergebnisse

## Plugin-Settings

- `ApiBaseUrl`
- `Username`
- `Password`
- `DefaultTenantContext` (optional)
- `DefaultCustomerCode` (optional)
- `EnableDebugLogging` (temporär aktiv, schreibt `sqlfroega-debug.log` im Plugin-Ordner)

Zusätzlich wird bei jedem Speichern eine Fallback-Datei `sqlfroega.settings.json` im Plugin-Ordner geschrieben,
damit Settings auch dann erhalten bleiben, wenn der FlowLauncher-JSON-Storage nicht zuverlässig persistiert.

## Build

```bash
dotnet build SqlFroega.FlowLauncher/SqlFroega.FlowLauncher.csproj
```

## FlowLauncher-Plugin lokal installieren (Windows)

1. **Plugin bauen** (Release empfohlen):

   ```powershell
   dotnet build .\SqlFroega.FlowLauncher\SqlFroega.FlowLauncher.csproj -c Release
   ```

2. **Plugin-Zielordner in FlowLauncher anlegen**:

   ```powershell
   $pluginId = "9A688F57E7644F33ACDF8A7D501EAF23"
   $flowPluginDir = Join-Path $env:APPDATA "FlowLauncher\Plugins\$pluginId"
   New-Item -ItemType Directory -Force -Path $flowPluginDir
   ```

3. **Build-Artefakte kopieren** (inkl. `plugin.json`, DLLs, `Images`, `Languages`):

   ```powershell
   Copy-Item .\SqlFroega.FlowLauncher\bin\Release\* -Destination $flowPluginDir -Recurse -Force
   ```

4. **FlowLauncher neu starten**.

5. **Plugin konfigurieren** in FlowLauncher:
   - Plugin `SqlFroega` öffnen
   - `ApiBaseUrl`, `Username`, `Password` setzen
   - optional: `DefaultTenantContext`, `DefaultCustomerCode`

6. **Nutzung testen**:
   - FlowLauncher öffnen und `sql <suchbegriff>` tippen
   - `Enter` auf einem Treffer kopiert das Original-SQL
   - Context-Menü zeigt zusätzliche Aktionen wie `Copy Rendered SQL`

## Fehlersuche

- **Plugin erscheint nicht**: prüfen, ob `plugin.json` direkt im Plugin-Ordner liegt (nicht in Unterordnern).
- **Keine Treffer**: `ApiBaseUrl` prüfen und sicherstellen, dass die API läuft.
- **Auth-Fehler**: Zugangsdaten in den Plugin-Settings aktualisieren.
- **Rendern funktioniert nicht**: `DefaultCustomerCode` setzen oder im Kontext-Menü den passenden Customer-Code verwenden.
- **Copy friert ein**: Debug-Log im Plugin-Ordner prüfen (`sqlfroega-debug.log`). Copy läuft jetzt in einem dedizierten STA-Worker.

## Referenz

Das Grundgerüst orientiert sich am offiziellen C#-Sample:
`https://github.com/Flow-Launcher/plugin-samples/tree/master/HelloWorldCSharp`
