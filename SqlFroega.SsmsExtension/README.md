# SqlFroega.SsmsExtension (Search + Open-Flow)

Dieses Verzeichnis enthält ein SSMS/VSIX-Grundgerüst mit Search-Flow und erstem Open-in-Editor-Pfad.

## Bereits umgesetzt
- VSIX-Manifest (`source.extension.vsixmanifest`) ist vorhanden.
- CommandTable (`SqlFroegaCommands.vsct`) registriert den Menüpunkt **Tools → SqlFroega Search**.
- `AsyncPackage` initialisiert den Command beim Laden.
- `SearchToolWindow` + WPF-UI mit Suchfeld, Such-Button, Ergebnisliste und Open-Aktion.
- `SqlFroegaApiClient` unterstützt Login, Volltextsuche und Script-Detail-Load.
- `WorkspaceManager` speichert geöffnete Skripte lokal und pflegt `workspace-index.json`.
- Double-Click oder Open-Button lädt Script-Detail und öffnet die lokale Datei im Host-Editor.

## Runtime-Settings via Umgebungsvariablen
- `SQLFROEGA_API_BASEURL`
- `SQLFROEGA_USERNAME`
- `SQLFROEGA_PASSWORD`
- `SQLFROEGA_TENANT_CONTEXT`
- `SQLFROEGA_SEARCH_TAKE`

## Nächste technische Schritte
1. Open-Modus sauber trennen (readonly/edit inkl. Änderungsüberwachung).
2. Save-Intercept und Pflichtfeld `changeReason` ergänzen.
3. Folder Search + Bulk Read auf ToolWindow-Ebene integrieren.
4. Persistente Settings-UI statt reiner Environment-Konfiguration ergänzen.
