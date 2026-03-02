# Konzept: Script-/Ordner-Verwaltung in SqlFrögä

## Ziel
Skripte sollen in einer virtuellen Ordnerstruktur organisiert werden können, ohne dass physische Dateien im Dateisystem benötigt werden. Die bestehende Suche, Bearbeitung und API bleibt nutzbar und wird um Ordner-Kontext erweitert.

## Anforderungen (funktional)
- Benutzer können Ordner erstellen, umbenennen, verschieben und löschen.
- Ein Skript kann einem Ordner zugeordnet werden (MVP: genau ein Ordner pro Skript).
- Die linke Navigation zeigt eine Baumansicht (Ordner) plus Skriptliste des gewählten Knotens.
- Suche funktioniert weiterhin global, optional mit Ordnerfilter.
- Bestehende Skripte ohne Ordner bleiben sichtbar (z. B. unter „Ohne Ordner“).

## Nicht-Ziele (MVP)
- Keine Dateisystem-Synchronisierung.
- Keine Mehrfach-Zuordnung eines Skripts zu mehreren Ordnern.
- Keine Rechte-/Freigabelogik pro Ordner.

## Datenmodell (Vorschlag)

### Neue Tabelle `script_folder`
- `id` (uuid, PK)
- `name` (nvarchar, Pflicht)
- `parent_id` (uuid, nullable, FK auf `script_folder.id`)
- `sort_order` (int, default 0)
- `created_at`, `updated_at`

### Erweiterung Tabelle `script`
- `folder_id` (uuid, nullable, FK auf `script_folder.id`)

### Regeln
- `parent_id` darf keine Zyklen erzeugen (A -> B -> A verboten).
- Eindeutigkeit: `name` innerhalb desselben `parent_id`.
- Löschen eines Ordners nur, wenn leer, oder mit expliziter Option „Inhalt nach Ohne Ordner verschieben“.

## API-Design (Vorschlag)

### Endpunkte Ordner
- `GET /api/folders/tree` → gesamter Ordnerbaum.
- `POST /api/folders` → Ordner erstellen.
- `PATCH /api/folders/{id}` → Name/Parent/Sortierung ändern.
- `DELETE /api/folders/{id}` → Ordner löschen (mit Strategie-Flag für Inhalte).

### Skript-Endpunkte ergänzen
- `GET /api/scripts?folderId=...` (optional)
- `POST /api/scripts` + `PATCH /api/scripts/{id}` um Feld `folderId` erweitern.

## UI-Änderungen (MVP)
1. **Linke Spalte**: Umschaltbar zwischen „Alle Skripte“ und „Ordner“.
2. **Ordnerbaum**: Kontextmenü (Neu, Umbenennen, Löschen).
3. **Skriptmaske**: Feld „Ordner“ als Dropdown/Picker.
4. **Suche**: optionaler Filter „im aktuellen Ordner“.
5. **Leerer Zustand**: Wenn Ordner leer ist, Hinweis + CTA „Neues Skript erstellen“.

## Migration & Rollout
1. DB-Migration für `script_folder` + `script.folder_id`.
2. API read-only (Baum laden) einführen.
3. UI Baumansicht hinter Feature-Flag aktivieren.
4. Schreiboperationen (create/move/delete) aktivieren.
5. Flag entfernen, wenn Stabilität passt.

## Risiken & Gegenmassnahmen
- **Komplexität bei Verschieben/Sortierung** → Transaktion + serverseitige Validierung.
- **Zyklische Strukturen** → Zyklusprüfung in Service-Layer.
- **Suche wird langsamer** → Indexe auf `script.folder_id`, `script_folder.parent_id`, `script_folder.name`.

## Tests (Abnahmekriterien)
- Ordner anlegen/umbenennen/löschen inkl. Validierungsfehler.
- Skript einem Ordner zuordnen und wieder entfernen.
- Suche mit und ohne `folderId` liefert erwartete Treffer.
- Verschieben eines Ordners verhindert Zyklen.
- UI zeigt „Ohne Ordner“ für nicht zugeordnete Skripte.

## Offene Entscheidungen
- Soll ein Skript später in mehreren Ordnern erscheinen dürfen (Tags statt Ordner)?
- Braucht es pro Benutzer eigene Ordneransichten oder global gemeinsame Struktur?
- Ist Drag&Drop im MVP nötig oder Phase 2?
