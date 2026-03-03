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

- Die SSMS-Extension liefert Import-Payloads an die API.
- Die API validiert die Payload (Schema, Pflichtfelder, Referenzen).
- Die Desktop-App kann denselben Import-Preview-Report anzeigen (Wiederverwendung der API).
- Ziel: gleicher technischer Contract für Desktop-App und SSMS-Extension.

### UC-2: Massen-Speicherung (auch für SSMS-Extension)
Referenz: `Docs/ssms-extension-plan.md`

- Mehrere Skripte/Objekte werden in einem Vorgang gespeichert.
- Verarbeitung erfolgt transaktional oder in klar definierten Batches.
- Ergebnisreport enthält mindestens: erstellt, aktualisiert, übersprungen, fehlerhaft.
- Konfliktstrategien sind für SSMS-Extension und Desktop-App identisch.

### UC-3: Manueller Import im Tool (Desktop-App)

- Datei/Payload wird manuell im Tool ausgewählt.
- Vor dem Speichern wird ein Preview-/Validierungsreport angezeigt.
- Nutzer wählt Konfliktstrategie und startet den Commit.
- Fokus ist Bedienbarkeit im Import-Flow, nicht Export.

## Nicht-Ziele (aktuell)
- Manueller Export in der Desktop-App.
- Separater Export-Flow in der SSMS-Extension.
- Live-Synchronisation zwischen Umgebungen.

## Technischer Contract (Import + Massen-Speicherung)

### API-Endpunkte (Vorschlag)
- `POST /api/bulk/import/preview`
  - Input: Import-Payload (Datei oder JSON-Body je Client)
  - Output: Validierungsreport (`new`, `update`, `conflict`, `error`, `warnings`)

- `POST /api/bulk/import/commit`
  - Input: identische Payload + Konfliktstrategie + optional Batch-Optionen
  - Output: Persistenz-Report (`created`, `updated`, `skipped`, `failed`)

### Konfliktstrategien
- `SkipExisting`: bestehende Einträge nie überschreiben.
- `UpdateIfNewer`: nur überschreiben, wenn Quelle neuer ist.
- `ForceUpdate`: immer überschreiben (nur Admin/Power User).

### Validierungen
- Pflichtfelder pro Datensatz: `id`, `name`, `scope`, SQL-Inhalt.
- Format-/Schema-Version muss unterstützt sein.
- Referenzen müssen auflösbar oder als Warnung markiert sein.
- Bei Batch-Verarbeitung: klare Fehlerzuordnung pro Item.

## UI-/Client-Flows

### Desktop-App (UC-3)
1. Import-Quelle auswählen
2. Preview laden
3. Konfliktstrategie wählen
4. Commit starten
5. Ergebnisreport anzeigen

### SSMS-Extension (UC-1/UC-2)
1. Auswahl/Erzeugung der zu importierenden oder zu speichernden Payload
2. Preview gegen API
3. Commit mit gewählter Konfliktstrategie
4. Ergebnisreport im Extension-Kontext anzeigen

## Sicherheit & Betrieb
- Import und Massen-Speicherung nur für berechtigte Rollen.
- Größenlimits/Batches zur Stabilität (kein Voll-Laden großer Datenmengen in RAM).
- Audit-Log für alle Commit-Vorgänge (wer, wann, wie viele Datensätze, Ergebnis).

## Umsetzungsetappen
1. **Etappe 1 – Import Contract vereinheitlichen (API)**
   - Preview-/Commit-Endpunkte für Desktop-App und SSMS-Extension finalisieren
   - Einheitliche Response-Modelle für Validierung und Ergebnis

2. **Etappe 2 – Massen-Speicherung robust machen (API)**
   - Batch-/Transaktionslogik implementieren
   - Fehler- und Konfliktbehandlung pro Item

3. **Etappe 3 – Desktop-App Import-Flow (UC-3)**
   - Manueller Import inkl. Preview, Strategieauswahl und Commit
   - Ergebnisreport und UX-Verbesserungen

4. **Etappe 4 – SSMS-Extension-Integration (UC-1/UC-2)**
   - Anbindung an denselben API-Contract laut `Docs/ssms-extension-plan.md`
   - End-to-End Validierung der Massen-Speicherung

## Akzeptanzkriterien
- UC-1: SSMS-Extension kann Import-Preview und Commit über den gemeinsamen API-Contract ausführen.
- UC-2: Massen-Speicherung verarbeitet große Mengen stabil und liefert einen vollständigen Ergebnisreport.
- UC-3: Desktop-App bietet einen vollständigen manuellen Import-Flow mit Preview und Commit.
- Konfliktstrategien werden in Desktop-App und SSMS-Extension identisch umgesetzt.
- Audit-Log dokumentiert alle Commit-Vorgänge nachvollziehbar.
