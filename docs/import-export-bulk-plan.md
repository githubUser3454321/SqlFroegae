# Plan: Bulk-Import / Bulk-Export für SqlFrögä

## Ziel
Bulk-Import und Bulk-Export von Skripten ermöglichen, damit grosse Script-Bestände effizient zwischen Umgebungen übertragen werden können.

## Scope (MVP)
- **Bulk-Export** von ausgewählten Skripten (inkl. Metadaten und Referenzen) in eine transportable Datei.
- **Bulk-Import** derselben Datei in eine andere Umgebung mit Validierung und Vorschau.
- Unterstützung für spätere Integration in die geplante SSMS-Extension.

## Nicht-Ziele (MVP)
- Kein Live-Sync zwischen zwei Systemen.
- Keine automatischen Migrationsregeln für beliebige Fremdformate.

## Datenformat (Vorschlag)
- Container: `zip`
- Inhalt:
  - `manifest.json` (Version, Export-Zeitpunkt, Quelle, Schema-Version)
  - `scripts/<id>.sql` (Script-Inhalt)
  - `metadata.json` (Name, Scope, Module, Kundenkürzel, Tags, Referenzen)
  - `checksums.json` (SHA-256 je Datei)
- Vorteil: gut nachvollziehbar, diff-/review-freundlich und für SSMS-Extension einfach lesbar.

## API-Design (Vorschlag)
- `POST /api/bulk/export`
  - Input: Filter + Liste selektierter IDs
  - Output: Download (`application/zip`)
- `POST /api/bulk/import/preview`
  - Input: Upload-Datei
  - Output: Validierungsreport (neu, update, konflikt, fehler)
- `POST /api/bulk/import/commit`
  - Input: Upload-Datei + Konfliktstrategie
  - Output: Import-Resultat (Anzahl erstellt/aktualisiert/übersprungen)

## Konfliktstrategien
- `SkipExisting`: bestehende Einträge nie überschreiben.
- `UpdateIfNewer`: nur überschreiben, wenn Quelle neuer ist.
- `ForceUpdate`: immer überschreiben (nur Admin).

## Validierungen
- Pflichtfelder: `id`, `name`, `scope`, SQL-Inhalt.
- Referenzen müssen auflösbar sein oder als Warnung markiert werden.
- Schema-/Format-Version muss unterstützt sein.
- Checksum-Prüfung zur Erkennung manipulierter Dateien.

## UI-Flow (WinUI)
1. **Export-Dialog**
   - Filter/Selektion wählen
   - Zusammenfassung anzeigen
   - ZIP speichern
2. **Import-Dialog**
   - Datei wählen
   - Preview/Validierungsreport anzeigen
   - Konfliktstrategie wählen
   - Commit ausführen + Ergebnisreport

## SSMS-Extension-Kompatibilität
- Gleiches ZIP/Manifest-Format verwenden.
- Versionierte Contract-Dateien, damit Desktop-App und Extension unabhängig versioniert werden können.
- Import-Preview als wiederverwendbarer API-Endpunkt für beide Clients.

## Sicherheit & Betrieb
- Import/Export nur für berechtigte Rollen (Admin/Power User).
- Grössenlimit pro Upload (z. B. 200 MB im MVP) + Streaming statt Voll-Laden in RAM.
- Audit-Log: Wer hat wann was importiert/exportiert.

## Umsetzungsetappen
1. **Etappe 1 – Export MVP**
   - API + Manifest/ZIP-Writer
   - UI-Exportdialog
2. **Etappe 2 – Import Preview MVP**
   - Parser + Validator + Konfliktanalyse
   - UI-Preview
3. **Etappe 3 – Import Commit MVP**
   - Persistenz + Konfliktstrategie
   - Ergebnisreport + Audit-Log
4. **Etappe 4 – SSMS-Extension-Anbindung**
   - Wiederverwendung derselben Endpunkte/Contracts

## Akzeptanzkriterien
- Exportierte ZIP kann in einer zweiten Umgebung als Preview gelesen werden.
- Preview listet korrekt: neu / update / konflikt / fehler.
- Import-Commit respektiert die gewählte Konfliktstrategie.
- Export/Import-Events erscheinen im Audit-Log.
- Das Datenformat ist versioniert und dokumentiert.
