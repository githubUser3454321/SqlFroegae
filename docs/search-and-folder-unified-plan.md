# Einheitlicher PO-Plan: Script-Organisation + Advanced Spotlight Suche

## 1) Zielbild (konsolidiert)
SqlFrögä erhält ein hybrides Navigations- und Suchmodell mit zwei klar getrennten Ebenen:

1. **Bestehende Library-Suche bleibt unverändert** (stabiler Standard-Workflow).
2. **Neue vollflächige Advanced-Suche** als zusätzliche Option unter dem Namen **Spotlight Query Studio**.

Zusätzlich wird die bestehende Strukturplanung (Views/Collections/optionale Folder-Tree-Anzeige) mit der neuen Spotlight-Suche einheitlich verzahnt.

---

## 2) Produktentscheidung (PO)

### Empfehlung
- Den Toggle **`Display Folder Structure`** einführen, aber nur als optionale Navigationsansicht.
- Die neue, mächtige Suchlogik ausschließlich im **Spotlight Query Studio** umsetzen.

### Warum
- Baumstrukturen helfen „Explorer“-Nutzern, skalieren allein aber schlecht.
- Filter + gespeicherte Ansichten sind für Wiederfindung effizienter.
- Trennung schützt die bestehende Suche vor Komplexitätszuwachs.

---

## 3) Scope und Abgrenzung

### Im Scope
- Hybrid-Navigation mit:
  - Views (All Scripts, Favorites, Recent, Uncategorized)
  - Collections
  - optionaler Folder Structure per Toggle
- Neue Spotlight-Ansicht als Fullscreen-Overlay/Dialog.
- Alle bestehenden Filteroptionen auch im Spotlight verfügbar.
- AND/OR-Regellogik in einem einheitlichen UI/UX.
- Speichern/Laden von Suchprofilen mit Flag:
  - **Für mich**
  - **Für alle**
- Admin-Verwaltung zum Löschen gespeicherter Filterprofile.
- Folder-/Tree-Struktur als **zusätzliche** Option innerhalb des Spotlights.

### Nicht im Scope
- Umbau der bestehenden Suchmaske in der Library.
- Dateisystem-Sync.
- Rechte-/Freigabemodell pro Ordner/Collection.
- AI-Autokategorisierung im MVP.

---

## 4) UX-Konzept (einheitlich)

## 4.1 Standard-Library (bleibt wie bisher)
- Bestehende Suche und Masken bleiben funktionsfähig und unverändert.
- Optionaler Toggle `Display Folder Structure` steuert die Baumdarstellung in der Navigation.

## 4.2 Spotlight Query Studio (neu)

### Einstieg
- Ein leicht erreichbarer Button („Erweiterte Suche“) öffnet Spotlight.

### Verhalten
1. Spotlight öffnet sich als Fullscreen-Overlay über der aktuellen Ansicht.
2. Die Hintergrundansicht bleibt geladen (kein Hard-Reset).
3. „Abbrechen“ schließt Spotlight und zeigt den vorherigen Zustand sofort wieder.

### Such-UI
- Regelbasierte Suche mit Gruppen:
  - „Regel hinzufügen“
  - „Gruppe hinzufügen“
- Verknüpfung via AND/OR auf Gruppen-/Regel-Ebene.
- Einheitliche Eingabekomponenten je Filtertyp (Dropdown, Token, Text, etc.).
- Startzustand: 1 AND-Gruppe mit 1 leerer Regel.

---

## 5) Funktionale Anforderungen

## FR-1: Vollbild-Advanced-Suche
- Öffnen per Button, Schließen per Abbrechen (ESC optional).

## FR-2: Vollständiger Filterumfang
Im Spotlight sind pro Regelblock alle heute verfügbaren Filter auswählbar:
- Scope (Global / Customer / Module)
- Hauptmodul
- abhängige Module
- Tags/Flags
- referenzierte SQL-Objekte
- Kundenkürzel
- optional: gelöschte Einträge / Historie (wenn im aktuellen Modell aktiv)

## FR-3: AND/OR-Logik
- Mehrere Regeln innerhalb von Gruppen.
- Mehrere Gruppen pro Suchkonfiguration.
- AND/OR-Verknüpfung muss für alle Filterarten nutzbar sein.

## FR-4: Suchprofile
- Profil speichern/laden.
- Sichtbarkeit pro Profil:
  - Für mich (privat)
  - Für alle (global)

## FR-5: Admin-Filterverwaltung
- Admin kann gespeicherte Profile einsehen und löschen.
- Mindestdaten: Name, Owner, Scope, letzter Änderungszeitpunkt.

## FR-6: Tree als Zusatzbaustein
- Folder-Tree gemäß Plan als optionale Zusatzfunktion im Spotlight.
- Kein Ersatz der Standardnavigation.

---

## 6) Technische Leitplanken

### Datenmodell (inkrementell)
**Phase A (MVP-kompatibel):**
- `script_folder` (id, name, parent_id, sort_order, created_at, updated_at)
- `script.folder_id` (nullable)

**Phase B (hybrid/vNext):**
- `collection` (id, name, parent_id nullable, owner_scope, sort_order, timestamps)
- `script_collection` (script_id, collection_id, is_primary)
- optional `saved_view`/`saved_filter` (id, name, filter_json, owner_user_id, visibility)

### Validierungen
- Keine Zyklen in parent-child-Beziehungen.
- Eindeutiger Name innerhalb gleicher `parent_id`.
- Definierte Löschstrategie für Ordner/Collections.
- Validierungszustände für unvollständige Spotlight-Regeln.

### API-Evolution
**MVP:**
- `GET /api/folders/tree`
- `POST/PATCH/DELETE /api/folders`
- `GET /api/scripts?folderId=...`

**vNext (Navigation + Spotlight):**
- `GET /api/navigation`
- `GET /api/scripts?collectionId=...&viewId=...&q=...`
- `PATCH /api/scripts/{id}` mit `collectionIds[]` (+ optional `primaryCollectionId`)
- `POST/PATCH /api/views`
- `GET/POST/PATCH/DELETE /api/search-profiles`

---

## 7) Migration & Rollout
1. DB-Migration A (`script_folder`, `script.folder_id`)
2. Folder-Read APIs + UI hinter Feature Flag
3. Ordner-Schreiboperationen stabilisieren (inkl. Zykluschecks)
4. Toggle `Display Folder Structure` ausrollen
5. Spotlight Phase 1: Fullscreen + bestehende Filter 1:1
6. Spotlight Phase 2: AND/OR-Regelgruppen + Validierung
7. Spotlight Phase 3: Suchprofile (Für mich/Für alle)
8. Spotlight Phase 4: Admin-Filterverwaltung (Löschen)
9. DB-Migration B (`collection`, `script_collection`) schrittweise aktivieren
10. Tree-Struktur als optionale Spotlight-Erweiterung finalisieren

---

## 8) Abnahmekriterien (DoD)
1. Spotlight öffnet per Button als Vollbild-Overlay.
2. Abbrechen stellt die vorherige Ansicht ohne Reload-Verlust wieder her.
3. Alle bestehenden Filter sind in jedem Regelblock verfügbar.
4. AND/OR mit Regeln + Gruppen funktioniert konsistent.
5. Suchprofile sind speicherbar mit „Für mich“/„Für alle“.
6. Admin kann Suchprofile löschen.
7. Toggle blendet Folder Structure zuverlässig ein/aus.
8. Folder-Tree ist im Spotlight als zusätzliche Option nutzbar.
9. Bestehende Suche bleibt funktional und strukturell unverändert.

---

## 9) KPI-Set
- Median „Time to find script“ sinkt.
- Anteil uncategorized Skripte sinkt.
- Nutzung von Views/Favorites/Suchprofilen steigt.
- Weniger reine Umorganisationsaktionen pro aktivem User.

---

## 10) Priorisierte Roadmap
- **P0:** Folder MVP + Toggle + Uncategorized
- **P1:** Spotlight Overlay + Filter parity
- **P2:** AND/OR-Regelgruppen + Suchprofile
- **P3:** Admin-Filterverwaltung + Tree im Spotlight
- **P4:** Multi-Collection + Primary Location + UX-Feinschliff

---

## 11) Umsetzungsstatus (aktueller Stand)

### 11.1 Funktionale Anforderungen (FR)
- FR-1: Vollbild-Advanced-Suche — **DONE** (Spotlight öffnet per eigenem Button als Vollbild-Overlay, Abbrechen möglich).
- FR-2: Vollständiger Filterumfang — **Partialy DONE** (Scope, Hauptmodul, abhängige Module, Tags/Flags, SQL-Objekte, Kundenkürzel, IncludeDeleted/Historie sowie folderId/collectionId sind backendseitig verfügbar; regelblockbasiertes Multi-Set und vollständige UI-Bindung noch offen).
- FR-3: AND/OR-Logik — **Partialy DONE** (Backend-Endpoint `/api/v1/scripts/spotlight-search` mit Gruppen-Verknüpfung per AND/OR umgesetzt; UI-Regelbuilder auf Frontend-Seite noch offen).
- FR-4: Suchprofile (Für mich/Für alle) — **Partialy DONE** (Backend-CRUD für Suchprofile inkl. Sichtbarkeit `private/global` umgesetzt; Frontend-Integration und Speichern/Laden im Studio noch offen).
- FR-5: Admin-Filterverwaltung — **DONE** (Admin-Liste und Löschpfad über `/api/v1/admin/search-profiles` und `/api/v1/search-profiles/{id}` backendseitig implementiert).
- FR-6: Tree als Zusatzbaustein im Spotlight — **Partialy DONE** (Folder-Tree und Collection-Daten sind backendseitig über `/api/v1/navigation` und `/api/v1/folders/tree` abrufbar; Spotlight-UI-Integration noch offen).

### 11.2 Migration & Rollout (Punkt für Punkt)
1. DB-Migration A (`script_folder`, `script.folder_id`) — **Partialy DONE** (Schema-Ensure vorhanden und versioniertes SQL-Skript `Docs/003_folder_collection_searchprofile_migration.sql` ergänzt; Ausführung im Zielsystem/Release-Pipeline noch offen).
2. Folder-Read APIs + UI hinter Feature Flag — **Partialy DONE** (`GET /api/v1/folders/tree` und `folderId`-Filter in Script-Suche backendseitig verfügbar; UI/Feature-Flag steht aus).
3. Ordner-Schreiboperationen stabilisieren (inkl. Zykluschecks) — **Partialy DONE** (Create/Update/Delete APIs vorhanden, inklusive Parent-Validierung, Duplicate-Check und Cycle-Check; erste Unit-Tests für zentrale Backend-Logik vorhanden, E2E-Härtung und UI-Flows fehlen).
4. Toggle `Display Folder Structure` ausrollen — **NOT DONE**.
5. Spotlight Phase 1: Fullscreen + bestehende Filter 1:1 — **Partialy DONE** (Fullscreen + Filter-Parität im Spotlight-Einstieg implementiert und in eigene Spotlight-View ausgelagert; vollständige Regelblock-Engine noch offen).
6. Spotlight Phase 2: AND/OR-Regelgruppen + Validierung — **Partialy DONE** (Backend-Gruppenlogik per AND/OR verfügbar; UI-Validierungszustände noch ausstehend).
7. Spotlight Phase 3: Suchprofile (Für mich/Für alle) — **Partialy DONE** (Backend-Endpunkte für Anlegen/Liste/Löschen sind vorhanden; UI-Workflows noch offen).
8. Spotlight Phase 4: Admin-Filterverwaltung (Löschen) — **DONE** (Admin kann Profile backendseitig einsehen/löschen).
9. DB-Migration B (`collection`, `script_collection`) schrittweise aktivieren — **Partialy DONE** (Schema-Ensure, CRUD-/Assignment-APIs und versioniertes SQL-Skript `Docs/003_folder_collection_searchprofile_migration.sql` vorhanden; produktive Aktivierung + UI-Anbindung noch ausstehend).
10. Tree-Struktur als optionale Spotlight-Erweiterung finalisieren — **Partialy DONE** (Backend-Navigation liefert Views/Folders/Collections gebündelt; finale Spotlight-Frontend-Integration noch ausstehend).

### 11.3 Abnahmekriterien (DoD)
1. Spotlight öffnet per Button als Vollbild-Overlay — **DONE**.
2. Abbrechen stellt die vorherige Ansicht ohne Reload-Verlust wieder her — **DONE**.
3. Alle bestehenden Filter sind in jedem Regelblock verfügbar — **Partialy DONE** (Filter sind im Spotlight vorhanden und suchbar; Regelblock-Mehrfachstruktur noch ausstehend).
4. AND/OR mit Regeln + Gruppen funktioniert konsistent — **Partialy DONE** (Konsistente Gruppenverknüpfung im Backend verfügbar; Rule-Builder-UX im Frontend noch nicht final).
5. Suchprofile sind speicherbar mit „Für mich“/„Für alle“ — **Partialy DONE** (Backend unterstützt private/global Profile; UI-Speicherfunktion steht noch aus).
6. Admin kann Suchprofile löschen — **DONE** (Backend-Endpunkte vorhanden und auf Admin-Berechtigung abgesichert).
7. Toggle blendet Folder Structure zuverlässig ein/aus — **NOT DONE**.
8. Folder-Tree ist im Spotlight als zusätzliche Option nutzbar — **Partialy DONE** (Backend liefert Tree-/Navigation-Daten; Frontend-Einbindung im Spotlight steht noch aus).
9. Bestehende Suche bleibt funktional und strukturell unverändert — **DONE**.
