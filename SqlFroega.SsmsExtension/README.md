# SqlFroega.SsmsExtension (Search + Folder Bulk Read)

Dieses Verzeichnis enthält ein SSMS/VSIX-Grundgerüst mit Search-Flow, Folder Search und erstem Bulk-Read-Open-Pfad.

## Bereits umgesetzt
- VSIX-Manifest (`source.extension.vsixmanifest`) ist vorhanden.
- CommandTable (`SqlFroegaCommands.vsct`) registriert den Menüpunkt **Tools → SqlFroega Search**.
- `AsyncPackage` initialisiert den Command beim Laden.
- `SearchToolWindow` + WPF-UI mit:
  - Volltextsuche,
  - Folder-Dropdown (aus `/folders/tree`),
  - Folder-Script-Listing,
  - Single-Open und „Alle öffnen“ (Bulk Read).
- `SqlFroegaApiClient` unterstützt Login, Volltextsuche, Folder-Tree, Folder-Script-Search und Script-Detail-Load inklusive Retry/Timeout/Circuit-Breaker für transiente API-Fehler.
- `WorkspaceManager` speichert geöffnete Skripte lokal, setzt readonly/edit Dateiattribute und pflegt `workspace-index.json` inkl. `versionToken`, `lastSyncedContentHash`, `lastKnownLocalContentHash`, `hasUnsyncedLocalChanges` als Grundlage für spätere Konflikterkennung.
- Double-Click oder Open-Button lädt ein einzelnes Script-Detail und öffnet die lokale Datei im Host-Editor.
- „Alle öffnen“ verarbeitet Bulk Read in konfigurierbaren Batches und arbeitet bei Teilfehlern mit den erfolgreichen Scripts weiter.

## Runtime-Settings via Umgebungsvariablen
- `SQLFROEGA_API_BASEURL`
- `SQLFROEGA_USERNAME`
- `SQLFROEGA_PASSWORD`
- `SQLFROEGA_TENANT_CONTEXT`
- `SQLFROEGA_SEARCH_TAKE`
- `SQLFROEGA_WORKSPACE_ROOT`
- `SQLFROEGA_BULKREAD_BATCHSIZE` (1-50, Default 8)
- `SQLFROEGA_HTTP_TIMEOUT_SECONDS` (5-300, Default 30)
- `SQLFROEGA_HTTP_RETRY_COUNT` (0-5, Default 2)
- `SQLFROEGA_HTTP_RETRY_DELAY_MS` (100-5000, Default 400)
- `SQLFROEGA_HTTP_CB_FAILURE_THRESHOLD` (1-20, Default 5)
- `SQLFROEGA_HTTP_CB_BREAK_SECONDS` (5-300, Default 20)

## Nächste technische Schritte
1. Save-Intercept und Pflichtfeld `changeReason` ergänzen.
2. Konflikt-Dialog auf Basis der Workspace-Konfliktmarker (`hasUnsyncedLocalChanges`, `versionToken`) ergänzen.
3. Persistente Settings-UI statt reiner Environment-Konfiguration ergänzen.
4. Logging/Telemetry für Bulk-/Retry-/Circuit-Breaker-Ereignisse ergänzen.
