# Überarbeiteter PO-Plan: Script-Organisation in SqlFrögä

## Executive Summary
Die bisherige Planung ist technisch sinnvoll, aber als reines Ordner-MVP zu eng. Die empfohlene Ziel-Lösung ist ein **hybrides Navigationsmodell**:
- starke Suche bleibt zentral,
- ergänzend **Views + Collections**,
- optionaler Ordnerbaum via Toggle **`Display Folder Structure`**.

Damit werden klassische „Explorer“-Nutzer unterstützt, ohne moderne Workflows (Filter, Favoriten, schnelle Wiederfindung) zu blockieren.

---

## 1) Produktentscheidung (PO)

### Empfehlung
**Ja, den Toggle `Display Folder Structure` einführen** – aber nicht als Hauptstrategie, sondern als optionalen Darstellungsmodus innerhalb einer hybriden Informationsarchitektur.

### Warum
- Reine Baumstrukturen skalieren bei vielen Skripten schlecht.
- Nutzer denken oft in Aufgaben/Use-Cases statt in starren Hierarchien.
- Suche und gespeicherte Filter liefern in der Praxis schnellere Wiederfindung.

---

## 2) Zielbild UX

### Linke Navigation (einheitlich, klar priorisiert)
1. **Views**
   - All Scripts
   - Favorites
   - Recent
   - Uncategorized
2. **Collections**
   - manuell gepflegte Gruppen (fachlich orientiert)
3. **Folder Structure** *(optional)*
   - nur sichtbar, wenn Toggle aktiv

### Toggle-Verhalten
- UI-Label: **`Display Folder Structure`**
- Position: oben in der Sidebar
- Speicherung: pro User (Preference)
- Default: **Aus**, um neue Nutzer nicht in eine komplexe Baumlogik zu zwingen

### Script-Formular
- Feld `Collections` (Multi-Select)
- Optional `Primary location` für kompatible Baumdarstellung

---

## 3) Scope: MVP vs. vNext

### MVP (P0)
- Virtuelle Ordnerstruktur mit `script.folder_id` (single assignment)
- „Uncategorized/Ohne Ordner“ sichtbar
- `Display Folder Structure` Toggle (Feature Flag)
- Suche optional mit Folder-Filter

### vNext (P1/P2)
- Favorites + Recent + Saved Views
- `collection` + `script_collection` (multi assignment)
- Primary Location für eindeutige Standardanzeige

### Nicht-Ziele (vorerst)
- Dateisystem-Sync
- Rechte-/Freigabemodell pro Ordner/Collection
- AI-Autokategorisierung im MVP

---

## 4) Technische Leitplanken

### Datenmodell (inkrementell)
**Phase A (kompatibel zum MVP):**
- `script_folder` (id, name, parent_id, sort_order, created_at, updated_at)
- `script.folder_id` (nullable)

**Phase B (hybrid):**
- `collection` (id, name, parent_id nullable, owner_scope, sort_order, timestamps)
- `script_collection` (script_id, collection_id, is_primary)
- optional `saved_view` (id, name, filter_json, owner_user_id)

### Validierungen
- keine Zyklen in parent-child Beziehungen
- eindeutiger Name innerhalb gleicher parent_id
- definierte Löschstrategie (nur leer löschen oder Inhalte nach Uncategorized verschieben)

### API-Evolution
**MVP-kompatibel:**
- `GET /api/folders/tree`
- `POST/PATCH/DELETE /api/folders`
- `GET /api/scripts?folderId=...`

**vNext:**
- `GET /api/navigation` (Views + Collections + optional Folder Tree)
- `GET /api/scripts?collectionId=...&viewId=...&q=...`
- `PATCH /api/scripts/{id}` mit `collectionIds[]` (+ optional `primaryCollectionId`)
- `POST/PATCH /api/views`

---

## 5) Migration & Rollout
1. **DB-Migration A** (`script_folder`, `script.folder_id`)
2. Folder-Read APIs + UI hinter Feature Flag
3. Schreiboperationen für Ordner stabilisieren (inkl. Zykluschecks)
4. Toggle `Display Folder Structure` ausrollen
5. **DB-Migration B** (`collection`, `script_collection`) vorbereiten und schrittweise aktivieren
6. Telemetrie prüfen, danach Single-Folder-Limits gezielt abbauen

---

## 6) Abnahmekriterien
- Ordner anlegen/umbenennen/verschieben/löschen inkl. Fehlermeldungen
- Zyklusverhinderung beim Verschieben
- Skriptzuordnung zu Folder (MVP) und Entfernen möglich
- „Uncategorized“ ist sichtbar und benutzbar
- Toggle blendet Folder Structure zuverlässig ein/aus
- Suche liefert erwartete Treffer global sowie gefiltert

---

## 7) KPI-Set zur Erfolgsmessung
- Median „Time to find script“ sinkt
- Anteil uncategorized Skripte sinkt über Zeit
- Nutzung von Views/Favorites steigt
- Weniger reine Umorganisationsaktionen (Rename/Move) pro aktivem User

---

## 8) Priorisierte Roadmap
- **P0:** Folder MVP + Toggle + Uncategorized
- **P1:** Favorites, Recent, Saved Views
- **P2:** Multi-Collection + Primary Location
- **P3:** Drag&Drop, Bulk-Organize, AI-Assist
