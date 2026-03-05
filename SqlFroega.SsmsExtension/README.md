# SqlFroega.SsmsExtension (Bootstrap + erster UI-Flow)

Dieses Verzeichnis enthält jetzt ein lauffähiges SSMS/VSIX-Grundgerüst mit erstem Command und ToolWindow.

## Bereits umgesetzt
- VSIX-Manifest (`source.extension.vsixmanifest`) ist vorhanden.
- CommandTable (`SqlFroegaCommands.vsct`) registriert den Menüpunkt **Tools → SqlFroega Search**.
- `AsyncPackage` initialisiert den Command beim Laden.
- `SearchToolWindow` + einfache WPF-UI als Platzhalter für die Volltextsuche.

## Nächste technische Schritte
1. API-Client gegen `SqlFroega.Api` für Volltextsuche verdrahten.
2. Trefferliste im ToolWindow mit echten Daten/Metadaten füllen.
3. Öffnen eines Treffers in SSMS-Editor (readonly/edit) implementieren.
4. Settings (API URL, Auth, CustomerShortcode) nachziehen.
