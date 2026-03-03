# Plan: Bulk-Import / Bulk-Speicherung für SqlFrögä

## 0) Analyse-Update (Stand: 2026-03-03)
Dieses Dokument wurde gegen den aktuellen Backend-Code und die vorhandenen Pläne abgeglichen.

### Was ist bereits im Backend vorhanden (relevante Grundlage)
- Folder-/Collections-Struktur inkl. APIs und Validierung ist vorhanden (`/api/v1/folders/tree`, `/api/v1/folders`, `/api/v1/collections`, `/api/v1/navigation`, `folderId`/`collectionId`-Filter in Script-Suche).
- Spotlight-/Search-Profile-Themen sind weit fortgeschritten und separat dokumentiert.

### Was für Bulk-Import/-Speicherung **noch nicht** vorhanden ist
- Keine dedizierten Bulk-Import-Endpunkte (`/api/bulk/import/preview`, `/api/bulk/import/commit`).
- Kein Import-Preview-/Commit-Workflow in Desktop-App/SSMS im aktuellen Codebestand.

### Überschneidungen mit anderen Dokumenten
- `docs/search-and-folder-unified-plan.md` bleibt führend für Navigation, Folder-Tree, Collections, Spotlight.
- `Docs/ssms-extension-plan.md` bleibt führend für SSMS-UX/Workspace/Save-Interception.
- Dieses Dokument fokussiert nur auf Bulk-Import/-Speicherung und referenziert die bereits vorhandene Folder-/Collection-Basis, statt sie erneut zu planen.

---

## 1) Ziel (bereinigt)
Bulk-Import und Massen-Speicherung sollen auf der bereits vorhandenen Script-Organisation aufbauen:
- Dateisystem-Quelle (Windows Folder) **und/oder**
- SqlFrögä Folder-/Collection-Zuordnung

Export bleibt außerhalb des Scopes.

**Status:** IN PROGRESS

---

## 2) Scope (bereinigt, ohne Doppelplanung)

### UC-1: Bulk-Import via API (für Desktop + SSMS)
- Gemeinsamer API-Contract für Preview + Commit.
- Einheitliche Validierungs-/Ergebnisreports für beide Clients.
- Nutzung der vorhandenen Folder-/Collection-Logik zur Zuordnung.

**Status:** NOT DONE

### UC-2: Massen-Speicherung bestehender/neuer Skripte
- Verarbeitung großer Mengen in Batches/Transaktionen.
- Upsert-Verhalten für bestehende und neue Skripte.
- Ergebnisreport: `created`, `updated`, `skipped`, `failed`.

**Status:** NOT DONE

### UC-3: Importquelle „Windows Folder“
- Dateien aus lokalem Windows-Ordner einlesen.
- Mapping auf Script-Metadaten und Ziel-Folder/Collection.
- Preview vor Commit inkl. Konfliktanzeige.

**Status:** NOT DONE

### UC-4: Importziel „SqlFrögä Folder/Collection“
- Importierte/aktualisierte Skripte werden gezielt Foldern und Collections zugeordnet.
- Optionales Re-Assignment bestehender Skripte.

**Status:** IN PROGRESS

Begründung: Folder-/Collection-APIs und Assignment sind vorhanden; der eigentliche Import-Workflow fehlt noch.

---

## 3) Nicht-Ziele
- Manueller Export in der Desktop-App.
- Separater Export-Flow in der SSMS-Extension.
- Live-Synchronisation zwischen Umgebungen.

**Status:** DONE

---

## 4) Technischer Contract (API) – Zielbild + Ist-Status

### 4.1 Preview-Endpunkt
- `POST /api/v1/bulk/import/preview`
  - Input: Payload (JSON, optional Datei-Metadaten)
  - Output: Validierungsreport (`new`, `update`, `conflict`, `error`, `warnings`)

**Status:** NOT DONE

### 4.2 Commit-Endpunkt
- `POST /api/v1/bulk/import/commit`
  - Input: identische Payload + Konfliktstrategie + Batch-Optionen
  - Output: Persistenzreport (`created`, `updated`, `skipped`, `failed`)

**Status:** NOT DONE

### 4.3 Bulk-Laden bestehender Skripte (SSMS-relevant)
- `POST /api/v1/scripts/bulk-get`
  - Input: `scriptIds[]`
  - Output: Inhalte + Metadaten + Version/ETag

**Status:** NOT DONE

### 4.4 Bereits vorhandene Basis-Endpunkte (nicht neu planen)
- Folder-/Tree-/Navigation-Endpunkte
- Collections-CRUD + Script-Collection-Assignment
- Script-Suche mit `folderId` / `collectionId`

**Status:** DONE

---

## 5) Konfliktstrategien (einheitlich für Desktop + SSMS)
- `SkipExisting`: bestehende Einträge nicht überschreiben.
- `UpdateIfNewer`: nur überschreiben, wenn Quelle neuer ist.
- `ForceUpdate`: immer überschreiben (nur berechtigte Rollen).

**Status:** NOT DONE

---

## 6) Validierungen
- Pflichtfelder je Datensatz: `name`, `scope`, SQL-Inhalt (ID je nach Modus create/update).
- Unterstützte Format-/Schema-Version.
- Referenzen auflösbar oder als Warning markiert.
- Fehlerzuordnung pro Item im Batch.
- Validierung der Zielzuordnung auf vorhandene SqlFrögä Folder/Collections.

**Status:** IN PROGRESS

Begründung: Generelle API-Validierung für Folder/Collections/Spotlight ist vorhanden; import-spezifische Batch-Validierung fehlt.

---

## 7) Client-Flows

### 7.1 Desktop-App (manueller Import)
1. Quelle wählen (Windows Folder / Datei)
2. Preview laden
3. Konfliktstrategie wählen
4. Commit starten
5. Ergebnisreport anzeigen

**Status:** NOT DONE

### 7.2 SSMS-Extension
1. Payload aus Auswahl erzeugen
2. Preview gegen API
3. Commit mit Konfliktstrategie
4. Ergebnisreport im Extension-Kontext

**Status:** NOT DONE

---

## 8) Sicherheit & Betrieb
- Bulk-Import/-Commit nur für berechtigte Rollen.
- Größenlimits/Batches zur Stabilität.
- Audit-Log für Commit-Vorgänge.

**Status:** IN PROGRESS

Begründung: Rollen-/Auth-/Audit-Bausteine sind API-weit vorhanden; bulk-spezifische Policies/Audit-Events fehlen.

---

## 9) Umsetzungsetappen (konsolidiert)

1. **Etappe 1 – Contract & Modelle (API)**
   - DTOs für Preview/Commit + standardisierte Reports
   - Endpunkte `bulk/import/preview` und `bulk/import/commit`

   **Status:** NOT DONE

2. **Etappe 2 – Bulk-Persistenzlogik (API)**
   - Batch-/Transaktionslogik
   - Konfliktstrategie-Ausführung
   - Folder-/Collection-Zuordnung beim Import

   **Status:** NOT DONE

3. **Etappe 3 – Desktop-Import-Flow**
   - UI für Quelle, Preview, Strategie, Commit

   **Status:** NOT DONE

4. **Etappe 4 – SSMS-Integration**
   - Anbindung an denselben Contract
   - E2E-Validierung mit realen Ordner-/Script-Mengen

   **Status:** NOT DONE

---

## 10) Akzeptanzkriterien mit Status
- AC-1: API bietet Preview + Commit für Bulk-Import über gemeinsamen Contract.
  - **Status:** NOT DONE
- AC-2: Bulk-Commit verarbeitet große Mengen stabil (Batching/Transaktion) und liefert vollständigen Ergebnisreport.
  - **Status:** NOT DONE
- AC-3: Import kann aus Windows Folder gelesen und auf SqlFrögä Folder/Collections gemappt werden.
  - **Status:** NOT DONE
- AC-4: Konfliktstrategien sind in Desktop und SSMS identisch wirksam.
  - **Status:** NOT DONE
- AC-5: Bulk-Commits sind nachvollziehbar auditierbar.
  - **Status:** IN PROGRESS
- AC-6: Vorhandene Folder-/Collection-Basis wird wiederverwendet (keine Doppelimplementierung).
  - **Status:** DONE

---

## 11) Konkrete Abgrenzung „was nicht nötig ist in diesem Dokument“
Nicht erneut detailliert planen (weil bereits in anderen Dokumenten/Code vorhanden):
- Spotlight Query Studio Details
- Allgemeine Folder-/Collection-Navigation
- Suchprofil-Management
- Allgemeine API-Basis (Auth, Rollen, Rate-Limits)

Dieses Dokument soll nur die Bulk-Import/-Speicher-Lücke beschreiben, die auf der vorhandenen Folder-Struktur aufsetzt.
