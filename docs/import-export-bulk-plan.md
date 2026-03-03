# Plan: Bulk-Import / Bulk-Speicherung für SqlFrögä

## Ziel
Das Dokument beschreibt die nächsten Schritte für **Import und Massen-Speicherung** von Skripten.
Der Fokus liegt auf den aktuell relevanten Umsetzungsbereichen:
- **Desktop-App**
- **API**

Manueller **Export** ist aktuell **nicht** Teil des Umfangs.

## Scope (aktuell)

### UC-1: SSMS-Extension Import
Referenz: `Docs/ssms-extension-plan.md`

- Die SSMS-Extension liefert Import-/Bulk-Payloads an die API.
- Die API validiert die Payload (Schema, Pflichtfelder, Referenzen).
- Ziel: gleicher technischer Contract für Desktop-App und SSMS-Extension.

### UC-2: Massen-Speicherung über API (auch für SSMS-Extension)
Referenz: `Docs/ssms-extension-plan.md`

- Mehrere Skripte/Objekte werden in einem Vorgang geladen und verarbeitet.
- Die API unterstützt zwei Lade-Modi:
  - **ReadOnly-Modus**: Datensätze werden nur gelesen/geprüft, aber nicht überschrieben.
  - **Bearbeiten-Modus**: Datensätze werden zum Bearbeiten geladen und dürfen danach gespeichert werden.
- Ergebnisreport enthält mindestens: `created`, `updated`, `skipped`, `conflict`, `failed`.

### UC-3: Manueller Import im Tool (Desktop-App)

- In der Desktop-App sollen **standardmässig nur neue Skripte** importiert werden.
- Bereits vorhandene Skripte werden im Desktop-Standardflow **nicht bearbeitet/überschrieben**.
- Vor dem Speichern wird ein Preview-/Validierungsreport angezeigt.
- Fokus ist Bedienbarkeit im Import-Flow, nicht Export.

## Neu vs. Bearbeiten (fachliche Trennung)

### Neu (Create)
- Datensatz existiert im Ziel noch nicht.
- Import ist in Desktop-App und API erlaubt.
- Ergebnisstatus: `created`.

### Bearbeiten (Update)
- Datensatz existiert bereits im Ziel.
- Desktop-App-Standardflow: **nicht erlaubt** (wird als `skipped` oder `conflict` markiert).
- API/SSMS-Extension: nur im **Bearbeiten-Modus** erlaubt.
- Ergebnisstatus: `updated` oder `conflict`.

## Nicht-Ziele (aktuell)
- Manueller Export in der Desktop-App.
- Separater Export-Flow in der SSMS-Extension.
- Live-Synchronisation zwischen Umgebungen.

## Technischer Contract (Import + Massen-Speicherung)

### API-Endpunkte (Vorschlag)
- `POST /api/bulk/import/preview`
  - Input: Import-Payload + `loadMode` (`readonly` | `edit`)
  - Output: Validierungsreport (`new`, `update`, `conflict`, `error`, `warnings`)

- `POST /api/bulk/import/commit`
  - Input: identische Payload + `loadMode` + Konfliktstrategie + optional Batch-Optionen
  - Output: Persistenz-Report (`created`, `updated`, `skipped`, `conflict`, `failed`)

### Lademodi
- `readonly`
  - Keine direkten Updates auf bestehenden Skripten.
  - Erlaubt: Validierung, Vergleich, optional Import als **neues** Skript (z. B. mit neuer ID).
- `edit`
  - Bestehende Skripte dürfen aktualisiert werden.
  - Voraussetzung: erfolgreiche Konfliktprüfung (siehe Concurrency-Regel).

### Konfliktstrategien
- `SkipExisting`: bestehende Einträge nie überschreiben.
- `UpdateIfUnchanged`: nur überschreiben, wenn Ziel seit dem Laden unverändert ist.
- `ForceUpdate`: immer überschreiben (nur Admin/Power User, auditpflichtig).

### Concurrency- und Merge-Conflict-Regel
- Jeder Datensatz enthält eine Revisionsinformation (z. B. `rowVersion` oder `lastModifiedAt`).
- Beim Laden im **Bearbeiten-Modus** wird diese Revision mitgegeben.
- Beim Commit vergleicht die API geladene Revision vs. aktuelle Revision im Ziel.
- Bei Abweichung wird ein **Konflikt** zurückgegeben (analog Merge-Conflict), damit verhindert wird, dass zwei Personen zeitgleich blind überschreiben.

### Validierungen
- Pflichtfelder pro Datensatz: `id`, `name`, `scope`, SQL-Inhalt.
- Format-/Schema-Version muss unterstützt sein.
- Referenzen müssen auflösbar oder als Warnung markiert sein.
- Bei Batch-Verarbeitung: klare Fehlerzuordnung pro Item.

## UI-/Client-Flows

### Desktop-App (UC-3, Standard = nur Neu)
1. Import-Quelle auswählen
2. Preview laden
3. Nur Datensätze mit Status `new` zum Commit zulassen
4. Commit starten
5. Ergebnisreport anzeigen (`created`, `skipped`, `conflict`, `failed`)

### API/SSMS-Extension (UC-1/UC-2)
1. Auswahl/Erzeugung der zu importierenden oder zu speichernden Payload
2. `loadMode` festlegen (`readonly` oder `edit`)
3. Preview gegen API
4. Commit mit gewählter Konfliktstrategie
5. Ergebnisreport im Extension-Kontext anzeigen

## Sicherheit & Betrieb
- Import und Massen-Speicherung nur für berechtigte Rollen.
- `edit`-Modus nur für Rollen mit Schreibrecht.
- Größenlimits/Batches zur Stabilität (kein Voll-Laden großer Datenmengen in RAM).
- Audit-Log für alle Commit-Vorgänge (wer, wann, Modus, wie viele Datensätze, Ergebnis).

## Umsetzungsetappen
1. **Etappe 1 – Contract für Neu/Bearbeiten + Lademodi (API)**
   - `loadMode` (`readonly`/`edit`) in Preview und Commit verankern
   - Einheitliche Response-Modelle für `created/updated/skipped/conflict/failed`

2. **Etappe 2 – Concurrency-Schutz (API)**
   - Revisionsfelder einführen und beim Commit prüfen
   - Konfliktantworten pro Item (Merge-Conflict-ähnlich) implementieren

3. **Etappe 3 – Desktop-App Import-Flow (nur Neu)**
   - Manueller Import mit Preview und Commit nur für `new`
   - Bestehende Datensätze als `skipped`/`conflict` anzeigen

4. **Etappe 4 – SSMS-Extension-Integration (readonly/edit)**
   - Anbindung an denselben API-Contract laut `Docs/ssms-extension-plan.md`
   - End-to-End Validierung für beide Modi inkl. Konfliktfällen

## Akzeptanzkriterien
- UC-1: SSMS-Extension kann Import-Preview und Commit über den gemeinsamen API-Contract ausführen.
- UC-2: Massen-Speicherung unterstützt `readonly` und `edit` inkl. sauberem Ergebnisreport.
- UC-3: Desktop-App importiert standardmässig nur neue Skripte; bestehende werden nicht still überschrieben.
- Bei paralleler Bearbeitung erkennt die API Revisionsabweichungen und meldet Konflikte statt blindem Überschreiben.
- Audit-Log dokumentiert alle Commit-Vorgänge nachvollziehbar (inkl. Modus und Konflikte).
