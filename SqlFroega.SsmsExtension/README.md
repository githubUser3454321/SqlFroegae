# SqlFroega.SsmsExtension (Initial Setup)

Dieses Verzeichnis ist ein initiales Bootstrap für eine SSMS/VSIX-Extension.

## Ziel im ersten Schritt
- VSIX-fähiges C#-Projekt mit `AsyncPackage` bereitstellen.
- Platzhalter für Commands/ToolWindows/API-Integration schaffen.
- Implementierungsschritte zentral in `Docs/SSMS/Status.md` tracken.

## Nächste technische Schritte
1. VSIX-Manifest (`source.extension.vsixmanifest`) anlegen.
2. CommandTable (`.vsct`) ergänzen und Such-Command integrieren.
3. API-Client gegen `SqlFroega.Api` für:
   - Full-Text-Suche
   - Folder-Search
   - Bulk-Read
   - Bulk-Write
4. Workspace-Manager (lokale Dateien + Index) implementieren.
5. Save-Intercept + Pflichtfeld „Änderungsgrund“ implementieren.
