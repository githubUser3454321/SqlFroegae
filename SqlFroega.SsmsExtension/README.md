# SqlFroega.SsmsExtension (Bootstrap + Search-Verdrahtung)

Dieses Verzeichnis enthält ein SSMS/VSIX-Grundgerüst mit erstem Search-Flow gegen die bestehende API.

## Bereits umgesetzt
- VSIX-Manifest (`source.extension.vsixmanifest`) ist vorhanden.
- CommandTable (`SqlFroegaCommands.vsct`) registriert den Menüpunkt **Tools → SqlFroega Search**.
- `AsyncPackage` initialisiert den Command beim Laden.
- `SearchToolWindow` + WPF-UI mit Suchfeld, Such-Button und Ergebnisliste.
- Einfacher `SqlFroegaApiClient` (Login + GET `/api/v1/scripts`) für Volltextsuche.
- Runtime-Settings via Umgebungsvariablen:
  - `SQLFROEGA_API_BASEURL`
  - `SQLFROEGA_USERNAME`
  - `SQLFROEGA_PASSWORD`
  - `SQLFROEGA_TENANT_CONTEXT`
  - `SQLFROEGA_SEARCH_TAKE`

## Nächste technische Schritte
1. Treffer-Double-Click in SSMS-Editor (readonly/edit) implementieren.
2. Detail-Endpoint verdrahten und lokales Datei-Mapping ergänzen.
3. Persistente Settings-UI statt reiner Environment-Konfiguration ergänzen.
4. Folder Search + Bulk Read auf ToolWindow-Ebene integrieren.
