# SqlFrögä – Aufgabenliste (Backlog)

Diese Liste fasst die offenen Punkte aus dem aktuellen Projektkontext zusammen und strukturiert sie als umsetzbare Tasks.

## P0 – Kritisch / zeitnah umsetzen

- [ ] **Deep-Link-Key beim App-Start validieren**
  - Beim Start sicherstellen, dass der benötigte Deep-Link-Key vorhanden/gesetzt ist.
  - Bei fehlendem Key klare Fehlermeldung + Fallback-Verhalten definieren.

- [ ] **Fehleranalyse: Skript-Erkennung in `WITH`-Kontext**
  - Prüfen, warum ein Skript/Objekt vom Typ `om_db.syn_*.sql` innerhalb eines `WITH`-Blocks nicht gefunden wird.
  - Parser-/Regex-/ScriptDom-Logik auf CTE- und `WITH`-Sonderfälle erweitern.
  - Reproduzierbaren Testfall anlegen.

- [ ] **Fehleranalyse: Objektreferenzen in konkreten Skripten**
  - Problemfälle bei Skripten mit `SELECT * FROM ...` analysieren (u. a. `om_db.syn_*.sql` und `om.dbo._ztMembership.sql`).
  - Sicherstellen, dass Referenzen korrekt erkannt und gespeichert werden.

## P1 – Hoher Nutzen (UX + Konsistenz)

- [ ] **Filter-Reihenfolge mit Edit-Ansicht synchronisieren**
  - Reihenfolge der Filter in Such-/Listenansicht identisch zur Edit-Maske machen.
  - Einheitliche Feldreihenfolge in UI-Komponenten dokumentieren.

- [ ] **Editor-Flow verbessern („Speichern nach unten“) + Enter-Logik**
  - Bearbeiten/Speichern-Flow so anpassen, dass der Fokus sinnvoll weitergeführt wird.
  - Enter-Taste kontextsensitiv behandeln (z. B. Auslösen vs. Zeilenumbruch).

- [ ] **Felder in Edit-Maske konsolidieren**
  - `[key]` in `[id]` umbenennen.
  - Edit-Layout für `id`, `Name`, `Scope` vereinheitlichen.

- [ ] **Erweiterte Suche entschlacken**
  - „Erweiterte Suche“ aus der Hauptfläche entfernen.
  - Stattdessen als Icon rechts neben dem Search-Feld platzieren.

## P2 – Mittel (Qualität / Erweiterbarkeit)

- [ ] **SQL-Formatierung verbessern**
  - „Schöne“ bzw. konsistente SQL-Formatierung beim Anzeigen/Speichern einführen.
  - Optional: Formatierungsprofil pro Team/Projekt konfigurierbar machen.

- [ ] **Script-/Data-Quality-Verwaltung ausbauen**
  - Strukturierte Verwaltung für Skript-Qualität und automatische Checks aufbauen.
  - Mindestregeln definieren (Namenskonventionen, Objektreferenzen, gefährliche Statements).

- [ ] **Internationalisierung prüfen (DE/EN)**
  - Klären, welche UI-Texte zweisprachig (Deutsch/Englisch) unterstützt werden sollen.
  - Grundlage für lokalisierbare Ressourcen schaffen.

## P3 – Optional / Strategisch

- [ ] **Import/Export großer Mengen vorbereiten**
  - Bulk-Import/Bulk-Export von Skripten konzipieren.
  - Schnittstelle zur geplanten SSMS-Extension berücksichtigen.

- [ ] **Produktentscheidung „T-SQL Script Library“**
  - Prüfen, ob der Bereich „T-SQL Script Library“ entfernt oder umbenannt werden soll.
  - Auswirkungen auf Navigation, Suchkonzept und Terminologie abstimmen.

---

## Vorschlag für nächste Iteration (Sprint-Fokus)

1. Deep-Link-Key-Validierung (P0)
2. Parser-Fix für `WITH`-Kontexte + problematische `SELECT * FROM`-Skripte (P0)
3. Filter-Reihenfolge + Edit-Feldkonsistenz (`key` → `id`) (P1)
4. Erweiterte Suche als Icon neben Search (P1)

## Definition of Done (für alle Tasks)

- Akzeptanzkriterien pro Task schriftlich erfasst.
- Reproduzierbare Tests für Bugfixes vorhanden.
- UI-Änderungen dokumentiert (vorher/nachher).
- Changelog-Eintrag bzw. Release-Note vorbereitet.
