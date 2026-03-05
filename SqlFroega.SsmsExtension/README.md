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
- `SqlFroegaApiClient` unterstützt Login, Volltextsuche, Folder-Tree, Folder-Script-Search und Script-Detail-Load.
- `WorkspaceManager` speichert geöffnete Skripte lokal, setzt readonly/edit Dateiattribute und pflegt `workspace-index.json` mit `scriptId`, `numberId`, `localPath`, `lastOpenedUtc`, `lastSyncedUtc`, `openMode`.
- Double-Click, Open-Button oder „Alle öffnen“ lädt Script-Details und öffnet die lokalen Dateien im Host-Editor.

## Runtime-Settings via Umgebungsvariablen
- `SQLFROEGA_API_BASEURL`
- `SQLFROEGA_USERNAME`
- `SQLFROEGA_PASSWORD`
- `SQLFROEGA_TENANT_CONTEXT`
- `SQLFROEGA_SEARCH_TAKE`
- `SQLFROEGA_WORKSPACE_ROOT`

## Nächste technische Schritte
1. Save-Intercept und Pflichtfeld `changeReason` ergänzen.
2. Workspace-Index um Version/ETag für Konflikterkennung erweitern.
3. Bulk Read optimieren (Batching + Partial-Failure Handling).
4. Persistente Settings-UI statt reiner Environment-Konfiguration ergänzen.
