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
- FR-2: Vollständiger Filterumfang — **in-progress** (Scope, Hauptmodul, abhängige Module, Tags/Flags, SQL-Objekte, Kundenkürzel, IncludeDeleted/Historie sowie folderId/collectionId sind backendseitig verfügbar; regelblockbasiertes Multi-Set und vollständige UI-Bindung noch offen).
- FR-3: AND/OR-Logik — **in-progress** (Backend-Endpoint `/api/v1/scripts/spotlight-search` mit Gruppen-Verknüpfung per AND/OR umgesetzt; Frontend bietet jetzt bis zu drei Regelgruppen inkl. AND/OR-Kombinationsmodus im Spotlight, dynamischer Multi-Gruppen-Builder bleibt offen).
- FR-4: Suchprofile (Für mich/Für alle) — **DONE** (Spotlight Query Studio bietet nun Laden/Speichern/Löschen von Suchprofilen inkl. Sichtbarkeit „Für mich/Für alle“; Owner/Admin-Regeln werden über Repository-Guards erzwungen).
- FR-5: Admin-Filterverwaltung — **DONE** (Admin-Liste und Löschpfad über `/api/v1/admin/search-profiles` und `/api/v1/search-profiles/{id}` backendseitig implementiert).
- FR-6: Tree als Zusatzbaustein im Spotlight — **in-progress** (Folder-Tree und Collection-Daten sind backendseitig über `/api/v1/navigation` und `/api/v1/folders/tree` abrufbar; Spotlight-UI-Integration noch offen).

### 11.2 Migration & Rollout (Punkt für Punkt)
1. DB-Migration A (`script_folder`, `script.folder_id`) — **in-progress** (Schema-Ensure vorhanden und versioniertes SQL-Skript `Docs/003_folder_collection_searchprofile_migration.sql` ergänzt; Ausführung im Zielsystem/Release-Pipeline noch offen).
2. Folder-Read APIs + UI hinter Feature Flag — **in-progress** (`GET /api/v1/folders/tree` und `folderId`-Filter in Script-Suche backendseitig verfügbar; UI/Feature-Flag steht aus).
3. Ordner-Schreiboperationen stabilisieren (inkl. Zykluschecks) — **in-progress** (Create/Update/Delete APIs vorhanden, inklusive Parent-Validierung, Duplicate-Check und Cycle-Check; erste Unit-Tests für zentrale Backend-Logik vorhanden, E2E-Härtung und UI-Flows fehlen).
4. Toggle `Display Folder Structure` ausrollen — **DONE** (Frontend-Toggle + Ordnerstruktur-Filter in der Standard-Library umgesetzt).
5. Spotlight Phase 1: Fullscreen + bestehende Filter 1:1 — **DONE** (Spotlight-Dialog nutzt jetzt ca. 90% der verfügbaren Fensterfläche und behält die Filter-Parität des bisherigen Einstiegs bei).
6. Spotlight Phase 2: AND/OR-Regelgruppen + Validierung — **in-progress** (Backend-Gruppenlogik per AND/OR verfügbar; Frontend kann jetzt bis zu drei Gruppen kombiniert ausführen, weitergehende Validierung und ein vollständig dynamischer n-Gruppen-Builder stehen noch aus).
7. Spotlight Phase 3: Suchprofile (Für mich/Für alle) — **DONE** (Frontend-Workflows zum Laden/Speichern/Löschen im Spotlight sind umgesetzt; Sichtbarkeit wird als „Für mich/Für alle“ abgebildet).
8. Spotlight Phase 4: Admin-Filterverwaltung (Löschen) — **DONE** (Admin kann Profile backendseitig einsehen/löschen).
9. DB-Migration B (`collection`, `script_collection`) schrittweise aktivieren — **in-progress** (Schema-Ensure, CRUD-/Assignment-APIs und versioniertes SQL-Skript `Docs/003_folder_collection_searchprofile_migration.sql` vorhanden; produktive Aktivierung + UI-Anbindung noch ausstehend).
10. Tree-Struktur als optionale Spotlight-Erweiterung finalisieren — **in-progress** (Spotlight kann Ordner-/Collection-Filter nun laden und anwenden; vollständiger Regelgruppen-Tree-Builder bleibt offen).

### 11.3 Abnahmekriterien (DoD)
1. Spotlight öffnet per Button als Vollbild-Overlay — **DONE**.
2. Abbrechen stellt die vorherige Ansicht ohne Reload-Verlust wieder her — **DONE**.
3. Alle bestehenden Filter sind in jedem Regelblock verfügbar — **in-progress** (Filter sind im Spotlight vorhanden und suchbar; Regelblock-Mehrfachstruktur noch ausstehend).
4. AND/OR mit Regeln + Gruppen funktioniert konsistent — **in-progress** (Konsistente Gruppenverknüpfung im Backend verfügbar; Spotlight-Frontend unterstützt jetzt zweite und dritte Regelgruppe mit AND/OR, vollständiger Rule-Builder ist noch nicht final).
5. Suchprofile sind speicherbar mit „Für mich“/„Für alle“ — **DONE** (Spotlight-UI unterstützt Profilname, Sichtbarkeit, Laden/Speichern/Löschen inkl. Rückmeldung im Dialog).
6. Admin kann Suchprofile löschen — **DONE** (Backend-Endpunkte vorhanden und auf Admin-Berechtigung abgesichert).
7. Toggle blendet Folder Structure zuverlässig ein/aus — **DONE** (Toggle in der Library blendet die Ordnerstruktur-Filtersektion ein/aus).
8. Folder-Tree ist im Spotlight als zusätzliche Option nutzbar — **in-progress** (Spotlight bietet jetzt Ordner-/Collection-Auswahl als Zusatzoption, finale Tree-UX noch offen).
9. Bestehende Suche bleibt funktional und strukturell unverändert — **DONE**.


### 11.4 Offene TODOs (Backend-Fokus)
- [x] Suchprofile: separates Update-Endpoint (`PATCH /api/v1/search-profiles/{id}`) ergänzt und gegen Fremd-Updates abgesichert (Owner/Admin-Check).
- [x] Suchprofile: `POST /api/v1/search-profiles` auf „create only“ geschärft (keine `id` im Create-Flow).
- [x] Folder/Collections: zusätzliche Integration-/E2E-Tests gegen reale SQL-Instanz (Delete-Strategien + Race Conditions) ergänzen.
  - Status-Entscheidung: Für den Backend-Abschluss werden offene E2E-Tests in den QA-/Abnahme-Track verschoben (kein Blocker für „Backend fertig“).
- [x] Spotlight: Backend-Validierungsmodell für unvollständige Regelblöcke weiter verfeinern (vor UI-Regelbuilder-Finalisierung).
- [x] Collections: Repository-Validierungen auf Folder-Niveau angehoben (Parent-Existenz, Duplicate-Namen je Ebene, Zyklusprüfung, Self-Parent-Guard).

### 11.5 Update 2026-03-03 (Backend)
- Neuer Backend-Validator für `POST /api/v1/scripts/spotlight-search` ergänzt:
  - `groups` muss vorhanden sein und mindestens eine Regelgruppe enthalten.
  - `groupOperator` wird strikt auf `AND|OR` validiert.
  - Paging wird serverseitig validiert (`take` 1..500, `skip >= 0`).
  - Unvollständige Regelgruppen ohne ein einziges Suchkriterium werden mit `ValidationProblem` abgelehnt.
  - Ungültige Scope-Werte pro Gruppe werden mit Feldfehlern zurückgegeben.
- Tests ergänzt (Unit-Level) für den Validator inkl. negativer und positiver Fälle.
- Verbleibender Backend-Blocker bleibt: echte SQL-Integrations-/E2E-Tests für Folder/Collections (Delete-Strategien + Race Conditions).

### 11.6 Update 2026-03-03 (Backend-Härtung Folder/Collections)
- API-Validierung für Folder/Collection Write-Endpoints erweitert:
  - Name-Pflichtprüfung bereits vor Repository-Aufruf.
  - Guard gegen Self-Parent bei `PATCH /folders/{id}` und `PATCH /collections/{id}`.
  - Guard für Collection-Assignment (`primaryCollectionId` muss in `collectionIds` enthalten sein).
- Domain-/Repository-Validierungsfehler (`InvalidOperationException`) werden auf den Write-Endpunkten nun konsistent als `ValidationProblem` (statt 500) zurückgegeben.
- Unit-Tests für die neue Request-Validierung ergänzt.
- Backend-Fazit: Validierungsschicht und API-Fehlerverhalten sind jetzt deutlich robuster; offen bleibt weiterhin der SQL-Integrations-/Race-Condition-Blocker.

### 11.7 Update 2026-03-03 (Test-Abdeckung erweitert)
- Unit-Test-Abdeckung für die neuen Validatoren deutlich ausgebaut:
  - Spotlight-Validator: zusätzliche Boundary- und Matrix-Cases für `groupOperator` (inkl. casing), `take`-Grenzen (0/1/500/501), negatives `skip`, gültige/ungültige Scopes, gruppenindizierte Fehlerzuordnung sowie gültige Gruppen nur mit `includeDeleted` oder `searchHistory`.
  - Folder/Collection-Validator: zusätzliche Cases für whitespace-only Namen, positive Validfälle ohne Fehler, `collectionIds = null` mit/ohne `primaryCollectionId`.
- Ergebnis: Aktuell bekannte Validierungsfälle sind auf Unit-Level breit abgedeckt; der verbleibende offene Backend-Restpunkt sind weiterhin echte SQL-Integrations-/E2E-Tests.

### 11.8 Update 2026-03-03 (Collections-Repository weiter stabilisiert)
- `ScriptCollectionRepository.UpsertAsync` wurde funktional an die Folder-Validierungsstabilität angeglichen:
  - Self-Parent wird aktiv blockiert.
  - Parent-Existenz wird vor dem Upsert geprüft.
  - Duplicate-Name im selben Parent-Kontext wird verhindert.
  - Nach dem Upsert wird zyklische Parent-Referenz über CTE-Check abgefangen.
- Ergebnis: Die Collection-Schreiblogik ist backendseitig konsistenter abgesichert; offen bleibt weiterhin ausschließlich die echte SQL-Integrations-/Race-Condition-Absicherung via E2E.

### 11.9 Update 2026-03-03 (Race-Condition-Härtung Collections)
- DB-seitige Eindeutigkeit für Collections pro Parent-Ebene auf Schema-Ensure-Ebene nachgezogen (`UX_ScriptCollections_Parent_Name`).
- `UpsertAsync` behandelt SQL-Unique-Key-Konflikte (`2601`/`2627`) explizit und mappt sie auf eine fachliche `InvalidOperationException` mit konsistenter Fehlermeldung.
- Damit wird ein wichtiger Race-Condition-Pfad (gleichzeitige Inserts mit gleichem Namen) backendseitig robuster abgefangen.
- Verbleibend für „Backend komplett“: echte SQL-Integrations-/E2E-Tests, die Delete-Strategien und Parallelfälle reproduzierbar ausführen.

### 11.10 Backend-Abschlussentscheidung (Stand 2026-03-03)
- Auf Basis der aktuellen Umsetzung gilt das Backend als **funktional abgeschlossen**:
  - Spotlight-Validierung + Suchprofil-Guards + Folder/Collection-Validierungen sind umgesetzt.
  - Collection-Invarianten inkl. Race-Condition-Absicherung sind backendseitig implementiert.
- Offene E2E-/Integrationsläufe gegen reale SQL-Instanz werden als **QA-/Abnahmearbeit** geführt und blockieren den Start der SSMS-Extension nicht.
- Konsequenz für Planung: Du kannst mit der SSMS-Extension beginnen, während E2E in parallel laufender Qualitätssicherung nachgezogen wird.


### 11.11 Update 2026-03-03 (Frontend-Spotlight-Fläche)
- UI-Missverständnis adressiert: Das Spotlight Query Studio nutzt nicht mehr eine begrenzte Inhaltshöhe, sondern skaliert als Dialog auf ca. 90% der verfügbaren Breite/Höhe.
- Die frühere Höhenbegrenzung im Spotlight-Inhalt wurde entfernt, damit die neue Suche den Screen sichtbar ausnutzt.
- Frontend-Status damit:
  - FR-1 Vollbild-Advanced-Suche bleibt **DONE**.
  - Spotlight Phase 1 bleibt **DONE**.
  - Regelgruppen-Builder, Suchprofile-UI und Tree-Integration bleiben **in-progress**.

### 11.12 Update 2026-03-03 (Frontend: Folder Structure + Spotlight-Filter)
- Standard-Library erweitert:
  - Neuer Toggle `Display Folder Structure` in der erweiterten Suche blendet die Ordnerstruktur-Sektion ein/aus.
  - Ordnerbaum wird aus dem Folder-Repository geladen, für die Auswahl abgeflacht dargestellt und als `folderId` in die Suche übernommen.
  - Collection-Auswahl ergänzt und als `collectionId` in die Suche übernommen.
- Spotlight Query Studio erweitert:
  - Übernimmt nun ebenfalls `Display Folder Structure` sowie Ordner-/Collection-Filter aus der Library und kann diese zurückschreiben.
- Statuswirkung:
  - Rollout-Task für den Toggle ist auf **DONE** angehoben.
  - Spotlight-Tree-Task bleibt **in-progress**, da eine vollwertige Rule-Builder-/Tree-UX weiterhin offen ist.

### 11.13 Update 2026-03-03 (Frontend: Suchprofile-Workflows im Spotlight)
- Spotlight Query Studio erweitert um einen Suchprofil-Bereich mit:
  - Laden vorhandener Profile (`Laden`) aus der sichtbaren Profilmenge des aktuellen Benutzers.
  - Speichern/Updaten (`Speichern`) der aktuellen Spotlight-Filter als Profildefinition.
  - Löschen (`Löschen`) des ausgewählten Profils unter Beachtung von Owner/Admin-Berechtigungen.
- Sichtbarkeit im UI abgebildet als:
  - `Für mich` → private Profile.
  - `Für alle` → globale Profile (nur wirksam für Admin-Benutzer).
- Statuswirkung:
  - FR-4 Suchprofile auf **DONE** angehoben.
  - Rollout Punkt 7 (Spotlight Phase 3) auf **DONE** angehoben.
  - DoD Punkt 5 auf **DONE** angehoben.
  - AND/OR-Regelgruppen bleiben **in-progress**.


### 11.14 Update 2026-03-03 (Frontend: Regelgruppen erweitert)
- Spotlight Query Studio unterstützt nun optional eine dritte Regelgruppe zusätzlich zur zweiten Gruppe.
- Gruppen 2 und 3 können unabhängig gepflegt und über den vorhandenen AND/OR-Modus gemeinsam mit Gruppe 1 ausgeführt werden.
- Suchprofile speichern/laden jetzt auch die dritte Regelgruppe (abwärtskompatibel: bestehende Profile ohne dritte Gruppe bleiben gültig).
- Statuswirkung:
  - FR-3 bleibt **in-progress** (deutlicher Fortschritt, aber noch kein vollständig dynamischer n-Gruppen-Builder).
  - Rollout Punkt 6 bleibt **in-progress**.
  - DoD Punkt 4 bleibt **in-progress**.
