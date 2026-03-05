# SSMS Extension – Research + Initialstatus

> Stand: Initiale Recherche und Bootstrap im Repo.

## 1) Zielbild (MVP)

- /InProgress/ SSMS Extension als eigener Client für die bestehende `SqlFroega.Api`.
- /InProgress/ Fokus-Features:
  - /InProgress/ **Bulk Read** (mehrere Scripts auf einmal laden)
  - /InProgress/ **Bulk Write** (mehrere Scripts inkl. Änderungsgrund speichern)
  - /InProgress/ **Folder Search** (Ordner laden, alle Skripte daraus öffnen)
  - /InProgress/ **Volltextsuche** (Script zielsicher über Text finden)

---

## 2) Was wir über SSMS Extensions wissen (kompakt)

- /DONE/ SSMS basiert technisch auf der Visual-Studio-Shell; Erweiterungen werden typischerweise als **VSIX + Package** umgesetzt.
- /DONE/ Kern-Baustein ist ein `AsyncPackage` (Registrierung per Attributes + GUID).
- /DONE/ UI-Integration läuft i. d. R. über:
  - Commands (`.vsct`),
  - Tool Window,
  - ggf. Menüeinträge/Context Menus.
- /DONE/ Für SQL-Dateien in SSMS ist ein lokaler Workspace sinnvoll (Datei-Mapping zwischen `scriptId` und lokalem Pfad).
- /DONE/ Save-Flow sollte Concurrency (Version/ETag) + Audit (`changeReason`) erzwingen.

---

## 3) Online recherchierte Referenzen / Beispielprojekte

> Hinweis: Einige Beispiele sind VS/SQL-Tools-nah und dienen als Architektur-/Code-Referenz für VSIX, Command-Handling und SQL-Workflows.

### A) Repositories / Projekte

- /DONE/ `ErikEJ/SqlCeToolbox`  
  Link: https://github.com/ErikEJ/SqlCeToolbox  
  Nutzen: Reifes SQL-Tooling-Plugin mit vielen Extension-Patterns.

- /DONE/ `martinnormark/DataDive`  
  Link: https://github.com/martinnormark/DataDive  
  Nutzen: VSIX/Visual-Studio-Extension-Struktur als Referenz für Package/UX.

- /DONE/ `krzysztofmatuszczyk/SQLVersionToolsPublic`  
  Link: https://github.com/krzysztofmatuszczyk/SQLVersionToolsPublic  
  Nutzen: SQL-Change/Versioning-Tooling-Ansätze.

- /DONE/ `MayNotNoob/SSMS2017_AddIn`  
  Link: https://github.com/MayNotNoob/SSMS2017_AddIn  
  Nutzen: SSMS-spezifischer AddIn-Ansatz (historisch, aber hilfreich für Host-Verhalten).

### B) Doku/How-To Suchrichtungen (für nächste Iteration)

- /NOT DONE/ Offizielle Microsoft-Referenz gezielt auf SSMS-Version + kompatible VS SDK Version festnageln.
- /NOT DONE/ Verifizieren, welche SSMS-Major-Version intern welche VS-Shell/SDK-Baseline nutzt.
- /NOT DONE/ Paketierung/Deployment-Flow auf Zielsystemen (Enterprise Rollout) dokumentieren.

---

## 4) Technischer Vorschlag (Architektur)

### 4.1 Komponenten

- /InProgress/ `SqlFroega.SsmsExtension` (neu, VSIX/Package-Host)
- /InProgress/ `SearchPanel` (Tool Window) für Volltext + Folder Search
- /InProgress/ `WorkspaceManager` (lokales Dateisystem + Index)
- /InProgress/ `SqlFroegaApiClient` (HTTP + Auth + Retries)
- /NOT DONE/ `SaveInterceptor` (Save-Hook + Reason-Dialog + Conflict Handling)

### 4.2 API-Nutzung (gegen bestehendes Backend)

- /InProgress/ Volltextsuche: bestehende Search-Endpoints adaptieren
- /InProgress/ Folder Search: Folder/Scripts Endpoints adaptieren
- /InProgress/ Bulk Read: Bulk-Get Endpunkt nutzen/ergänzen
- /NOT DONE/ Bulk Write: Bulk-Update Endpunkt final definieren + umsetzen

---

## 5) Initiales Setup im Repo

- /DONE/ Neuer Ordner `SqlFroega.SsmsExtension/` erstellt.
- /DONE/ Initiales Projektfile `SqlFroega.SsmsExtension.csproj` angelegt (net472 + VS SDK BuildTools Referenzen).
- /DONE/ `ExtensionPackage.cs` (Bootstrap `AsyncPackage`) angelegt.
- /DONE/ `README.md` mit Next Steps angelegt.
- /DONE/ In `SqlFrögä.slnx` aufgenommen.
- /DONE/ VSIX Manifest (`source.extension.vsixmanifest`) angelegt.
- /DONE/ Command-Definition (`.vsct`) und erstes Menü/Command angelegt.

---

## 6) Use Cases (fachlich)

### UC-01: Volltextsuche und einzelnes Script öffnen
- /InProgress/ User gibt Suchtext ein.
- /InProgress/ Trefferliste mit Metadaten (Name, Scope, Modul, Beschreibung) im ToolWindow.
- /InProgress/ „Open Readonly“ oder „Open Edit“ (Readonly-Flag + Open-Button/DoubleClick vorhanden).
- /InProgress/ Lokale Datei wird erzeugt (Workspace) und in Host-Editor geöffnet.

### UC-02: Folder Search und Bulk Read
- /InProgress/ User wählt Folder.
- /NOT DONE/ Extension lädt alle Script-IDs aus Folder.
- /NOT DONE/ Bulk Read lädt Inhalte in einem Request/Batches.
- /NOT DONE/ Alle Dateien werden als Tabs geöffnet.

### UC-03: Editieren und Bulk Write
- /NOT DONE/ User ändert mehrere geöffnete Skripte.
- /NOT DONE/ „Save All“ triggert Sammel-Commit.
- /NOT DONE/ Pro Commit verpflichtendes Feld „Änderungsgrund“.
- /NOT DONE/ API aktualisiert mehrere Skripte inkl. Audit-Daten.

### UC-04: Konfliktfall bei veralteter Version
- /NOT DONE/ Save erkennt Versionskonflikt (ETag/Version).
- /NOT DONE/ User sieht Optionen (Reload, Force, Abbrechen).
- /NOT DONE/ Konfliktauflösung wird nachvollziehbar geloggt.

### UC-05: Copy / Copy Rendered
- /NOT DONE/ Kontextmenü im Editor stellt „Copy“ bereit.
- /NOT DONE/ „Copy Rendered“ ersetzt Platzhalter anhand Kontextdaten.
- /NOT DONE/ Ergebnis landet in der Zwischenablage.

---

## 7) Konkrete Implementierungs-Backlogliste

## P0 – Fundament
- /DONE/ Status-Dokument + initiale Zielarchitektur erstellt.
- /DONE/ Initiales SSMS-Extension-Projekt erstellt.
- /DONE/ VSIX Manifest ergänzt.
- /InProgress/ Settings (API URL/Auth via Environment Variablen) implementieren.
- /InProgress/ Workspace-Index (`workspace-index.json`) implementiert (Basis-Mapping beim Öffnen).

## P1 – Suche
- /DONE/ ToolWindow für Volltextsuche (UI-Placeholder) angelegt.
- /DONE/ Treffer-Rendering (DataGrid) + Single-Open (Button/DoubleClick) umgesetzt.
- /NOT DONE/ Folder Browser + Folder-Script-Listing.

## P2 – Bulk Read/Bulk Write
- /NOT DONE/ Bulk Read mit Batching + Fehlerstrategie.
- /NOT DONE/ Bulk Write inkl. `changeReason` Pflicht.
- /NOT DONE/ Partial-Failure Handling (einige Saves fehlgeschlagen).

## P3 – Qualität & Betrieb
- /NOT DONE/ Logging/Telemetry.
- /NOT DONE/ Retry/Timeout/Circuit-Breaker Verhalten.
- /NOT DONE/ Paketierung + Installationsleitfaden + Rollout.

---

## 8) Risiken / Offene Punkte

- /InProgress/ Exakte Kompatibilitätsmatrix SSMS-Version ↔ VS SDK ↔ .NET Framework.
- /NOT DONE/ Security-Konzept für Token-Speicherung (DPAPI/Windows Credential Manager).
- /NOT DONE/ Verhalten bei sehr großen Foldern (Paging, Streaming, UI-Responsiveness).
- /NOT DONE/ Klären, ob Bulk Write serverseitig transaktional oder „best effort“ sein soll.

---

## 9) Nächster Schritt (direkt umsetzbar)

1. /InProgress/ Open-Modus finalisieren: echtes readonly/edit-Verhalten + Editor-State-Tracking.
2. /InProgress/ Workspace-Index erweitern (Version, LastSynced, Konfliktinfos) und robustes Remapping.
3. /NOT DONE/ Save-Intercept inkl. Pflichtfeld `changeReason` und Konflikt-Dialog umsetzen.
4. /NOT DONE/ Folder Search + Bulk Read in das ToolWindow integrieren.
