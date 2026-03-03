# PO-Plan: SSMS Extension (Suche, Öffnen, Edit/Readonly, Speichern)

## Kontext
Dieser Plan baut auf folgenden bestehenden Planungsdokumenten auf:
- `docs/import-export-bulk-plan.md` (Bulk-Import/-Export, API-Verträge, Manifest/Versionierung)
- `docs/search-and-folder-unified-plan.md` (Spotlight-/Volltextsuche, Folder-orientierte Navigation)

Ziel ist eine klare MVP-Planung für die SSMS Extension mit Fokus auf Suchflows, Dateihandling in SSMS, Bearbeitungsmodi und Save-/Overwrite-Prozess mit Änderungsgrund.

---

## 1) Zielbild (MVP)
Die SSMS Extension ermöglicht zwei klar getrennte Suchpfade und ein kontrolliertes Arbeiten mit SQL-Skripten direkt in SSMS:

1. **Single-Skript-Suche (Volltext)**
   - Funktionsgleich zur Flowlauncher-Volltextsuche.
   - Ergebnis ist ein einzelnes Skript, das in SSMS geöffnet wird.

2. **Folder-Suche (Mehrfachöffnung)**
   - Auswahl eines Folders/Ordners.
   - Danach werden alle enthaltenen Skripte als einzelne T-SQL-Editor-Tabs in SSMS geöffnet.

Zusätzlich gibt es pro geöffnetem Skript die Modi **Edit** und **Readonly** inkl. Aktionen **Copy Rendered** und **Copy**.

---

## 2) Produktanforderungen (funktional)

### FR-1: Zwei Sucharten in der Extension
- **FR-1.1 Volltextsuche**
  - Relevanz-/Trefferlogik analog zur bestehenden Flowlauncher-Suche.
  - Ziel: schnelles Öffnen einzelner Skripte.
- **FR-1.2 Folder-Suche**
  - Ordnerauswahl über API.
  - Öffnet alle Skripte des Ordners als T-SQL-Dokumente in SSMS.

### FR-2: Dokument-Modi und Copy-Funktionen
- Jeder geöffnete Tab kennt einen Modus:
  - **Readonly** (Default für sichere Sichtung)
  - **Edit** (änderbar + speicherbar)
- Aktionen:
  - **Copy**: rohen SQL-Inhalt kopieren.
  - **Copy Rendered**: gerenderten/aufgelösten Inhalt kopieren (z. B. mit ersetzten Platzhaltern).

### FR-3: Extension-Einstellungen mit Kundenkürzel
- Einstellungsfeld: `Kundenkürzel` (optional).
- Beim Speichern/Anlegen muss validiert werden:
  - Kundenkürzel ist systemweit **eindeutig**.
  - Kundenname ist systemweit **eindeutig**.
- Validierungsfeedback muss direkt und verständlich sein (z. B. "Kürzel bereits vergeben").

### FR-4: Öffnen und speichern editierbarer Dateien
- Wenn Skripte in **Edit** geöffnet werden, müssen sie lokal als bearbeitbare Dateien vorliegen.
- Präferierter Ablauf:
  1. Skripte per API **im Bulk** abrufen.
  2. In einen lokalen Workspace schreiben (nicht lose temporär).
  3. SSMS öffnet diese lokalen Dateien.

### FR-5: Save-/Overwrite-Dialog mit Änderungsgrund
- Beim Speichern einer bearbeiteten Datei:
  1. Extension erkennt Save-Event.
  2. Dialog fragt: „Aktuelle Version überschreiben?“
  3. Pflichtfeld: **Änderungsgrund**.
  4. Erst danach API-Update (inkl. Reason im Payload/Audit).

---

## 3) Technische Leitentscheidung: Lokaler Workspace statt reiner Temp-Dateien

### Empfehlung
Statt eines instabilen OS-Temp-Ordners wird ein dedizierter Workspace genutzt, z. B.:
- `%LocalAppData%/SqlFroegae/SsmsExtension/workspaces/<user-or-connection>/...`

### Gründe
- Stabilere Dateireferenzen für SSMS-Tabs.
- Reopen-/Recover-Szenarien robuster.
- Saubere Zuordnung von lokalem File ↔ Remote-Skript-ID.
- Kontrollierte Bereinigung möglich (TTL/Startup Cleanup).

### Minimales Dateimapping (MVP)
- `workspace-index.json` pro Workspace:
  - `localPath`
  - `scriptId`
  - `version` (remote)
  - `mode` (readonly/edit)
  - `openedAt` / `lastSavedAt`

---

## 4) End-to-End Flows

### Flow A: Single-Skript via Volltext
1. User sucht per Volltext.
2. User öffnet Treffer als Readonly oder Edit.
3. Extension lädt Skriptinhalt und erstellt lokales File.
4. SSMS öffnet Tab.
5. Bei Edit + Save → Overwrite-Dialog + Änderungsgrund → Commit.

### Flow B: Folder-Suche mit Bulk-Open
1. User wählt Folder.
2. Extension lädt Skriptliste + Inhalte per Bulk-Endpunkt.
3. Für jedes Skript wird lokales File erstellt.
4. SSMS öffnet alle Tabs in T-SQL.
5. Edit-Saves laufen pro Datei über denselben Commit-Dialog.

### Flow C: Save-Konflikt (Version hat sich geändert)
1. Save erkannt.
2. Extension prüft Version/ETag.
3. Wenn Konflikt: Dialog mit Optionen
   - Neu laden (lokale Änderungen verwerfen)
   - Erneut versuchen mit "Force" (berechtigt)
   - Abbrechen
4. Bei Erfolg immer mit Änderungsgrund auditieren.

---

## 5) API-Bedarfe (ergänzend zu bestehenden Plänen)

### Suche
- `GET /api/search/fulltext?q=...`
- `GET /api/folders`
- `GET /api/folders/{id}/scripts`

### Bulk-Laden für Extension
- `POST /api/scripts/bulk-get`
  - Input: Liste `scriptIds`
  - Output: Inhalte + Metadaten + Version/ETag

### Speichern
- `PUT /api/scripts/{id}`
  - Header/Body: Version/ETag für Concurrency
  - Body enthält:
    - SQL-Inhalt
    - `changeReason` (pflicht)
    - optional `customerShortcode` Kontext

### Validierung Kundenstammdaten
- `POST /api/customers/validate-unique`
  - Prüft Eindeutigkeit für `customerShortcode` und `customerName`.

---

## 6) UX/Interaktion (MVP)
- Such-Einstieg mit zwei klaren Aktionen:
  - „Skript suchen“
  - „Folder öffnen“
- Modus sichtbar pro Tab (Badge/Status): Readonly | Edit.
- Copy-Aktionen prominent im Kontextmenü und Toolbar.
- Save-Dialog blockiert Commit ohne Änderungsgrund.
- In Readonly kein versehentliches Persistieren.

---

## 7) Sicherheit, Audit, Governance
- Schreiboperationen nur mit passender Rolle.
- Jeder Commit speichert:
  - User
  - Zeit
  - alter/neuer Versionsstand
  - Änderungsgrund
- Optional: Telemetrie für Suchtyp-Nutzung (Volltext vs Folder) zur Priorisierung.

---

## 8) Umsetzungsetappen (priorisiert)

### P0 – Fundament
- Extension-Settings inkl. optionalem Kundenkürzel.
- Unique-Validation für Kundenkürzel und Kundenname.
- Lokaler Workspace + Mapping-Datei.

### P1 – Suchpfad 1
- Volltextsuche (Flowlauncher-paritätisch) + Single-Open.
- Readonly/Edit Umschaltung.
- Copy / Copy Rendered.

### P2 – Suchpfad 2
- Folder-Suche + Bulk-Open aller Folder-Skripte in SSMS.
- Performance-Absicherung (Batching, Progress, Error-Handling).

### P3 – Persistenz
- Save-Interception.
- Overwrite-Dialog mit Pflichtfeld Änderungsgrund.
- Version-/Konfliktbehandlung (ETag).

### P4 – Stabilisierung
- Recovery/Cleanup-Strategie Workspace.
- Verbesserte Konfliktdialoge.
- Metriken + UX-Polish.

---

## 9) Abnahmekriterien (DoD)
1. Zwei Sucharten sind in der Extension verfügbar und getrennt nutzbar.
2. Volltextsuche öffnet einzelne Skripte korrekt in SSMS.
3. Folder-Suche öffnet alle Skripte eines Folders als T-SQL-Tabs.
4. Pro geöffnetem Skript sind Readonly/Edit sowie Copy/Copy Rendered verfügbar.
5. Kundenkürzel und Kundenname werden auf Eindeutigkeit validiert.
6. Editierte Dateien werden lokal im Workspace geführt und sind in SSMS normal speicherbar.
7. Bei Save erscheint Overwrite-Dialog mit Pflichtfeld Änderungsgrund.
8. Ohne Änderungsgrund erfolgt kein Persist-Commit.
9. Jede erfolgreiche Änderung ist auditierbar.

---

## 10) Offene Entscheidungen
- Exakter Render-Algorithmus für „Copy Rendered“ (Server vs Client).
- Regeln für Folder-Bulk-Open bei sehr grossen Ordnern (Limit, Paging, Warnung).
- Verhalten bei Offline/Timeout während Save.
- Force-Overwrite-Berechtigungen (nur Admin oder auch Power User).
