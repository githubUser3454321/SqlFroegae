# SqlFrögä – Aufgabenliste (Backlog)

Diese Liste fasst die offenen Punkte aus dem aktuellen Projektkontext zusammen und strukturiert sie als umsetzbare Tasks.

## P0 – Kritisch / zeitnah umsetzen

- [ ] **Deep-Link-Key beim App-Start validieren**
  - Beim Start sicherstellen, dass der benötigte Deep-Link-Key funktion auf Windows vorhanden/gesetzt ist.
  - Bei(das heisst das windows erkennt wenn ich im browser den Deeplink eingebe das er direkt die applikation öffnet)
  - Funktioniert aktuell nicht, wenn die exe direkt gestartet wird ohne den installer, kein beim start der applikation gemacht werden.

- [ ] **Fehleranalyse: Skript-Erkennung in `WITH`-Kontext**
  - Prüfen, warum ein Skript/Objekt vom Typ `om_db.syn_MyTable` innerhalb eines `WITH`-Blocks nicht gefunden wird.
  (Obwohl die entsprechenden settings gesetzt sind, und wenn `om_db.syn_MyTable` in einem From oder join vorkommt, dann wird er ersetzt aber nicht innerhalb eines with blockes, prüfen ob es noch andere fälle gibt. Schreibe dazu ausreichihce tests.
  - Parser-/Regex-/ScriptDom-Logik auf CTE- und `WITH`-Sonderfälle erweitern.
  - Reproduzierbaren Testfall anlegen.

- [ ] **Fehleranalyse: Objektreferenzen in konkreten Skripten**
  - Problemfälle bei Skripten mit `SELECT * FROM ` + ()  (u. a. `om_db.syn_*.sql` und `om.dbo._ztMembership.sql`) analysieren.
  -> wenn `om_db.syn_*.sql` und `om_db._ztMembershipSettings` vorkommt, kann man davon ausgehen, das das skript, bzw die gewählten tabellen ebenfalls zum gespeicherten prefix von `om_db.syn_` gehört. wenn allerdings einmal `om_db.syn_` und einmal `om.syn_` vorkommt, soll ein fehler passieren, anonnsten soll die logik gleich bleiben.
  - Sicherstellen, dass Referenzen korrekt erkannt und gespeichert werden.

## P1 – Hoher Nutzen (UX + Konsistenz)

- [ ] **Editor-Flow verbessern („Speichern nach unten“) + Enter-Logik**
  - Bearbeiten und Speichern button unten rechts platzieren, so das sie rechts neben der Fehlerinformations anzeige ist.
  - Im login screen soll die Enter-Taste (wenn ich auf dem passwort feld bin) ein login trigger, solange passwort und nutzername einen wert hat.

- [ ] **Felder in Edit-Maske konsolidieren**
  - `[key]` in `[id]` umbenennen.
  - Edit-Layout Anpassen, anstelle von   `Name`, `id`(aktuell key),`Scope` soll neu das layout so sein(nach der neuen gegeben rheinfolge): 
  `id`(Länge verkleinern), `Name`(Länge vergrössern), `Scope` vereinheitlichen.

- [ ] **Erweiterte Suche entschlacken** [ERST NACH DER UMSETZUNG VON **Felder in Edit-Maske konsolidieren**]
  - „Erweiterte Suche“ aus der Hauptfläche entfernen.
  - Stattdessen als Icon rechts neben dem Search-Feld platzieren, das icon ersetzt das komplette pltz nutzende „Erweiterte Suche“, mit klick auf das icon erscheint das gleiche drop down, in dem die „Erweiterte Suche“ ersichtlich ist.
  
- [ ] **Filter-Reihenfolge mit Edit-Ansicht synchronisieren**
  - Reihenfolge der Filter in Such-/Listenansicht identisch zur Edit-Maske machen.
  - Einheitliche Feldreihenfolge in UI-Komponenten dokumentieren.
  
  
- [ ] **Default maske nach dem erfolgreichen login**
  - ist: Neues Skrpit erfassen.
  - Soll: Neu leere Maske, mit einem hinweise das zuerst ein Item ausgewählt werden muss
  - zusatz feature: auf jeder skript edit maske (Neu / Bearbeiten) soll ein abbrechen button hinzukommen ( unten rechts in der nähe oder neben Bearbeiten und Speichern)
  -> der abbrechen button zeigt anschliessend die neue maske an das zuerst ein item ausgewählt werden muss.

## P2 – Mittel (Qualität / Erweiterbarkeit)

- [ ] **SQL-Formatierung verbessern**
  - Evaluiere ein Tool, das die SQL-Formatierung verschönern kann. 
  - Feature request eines users: neben Save button -> Format button ( soll ebenfalls bei "Copy Rendered" eingebaut werden)
  - „Schöne“ bzw. konsistente SQL-Formatierung beim Anzeigen/Speichern einführen.(aktuell ist diese etwas schwebs...)

- [ ] **Script-/Ordern Verwaltung ausbauen**
  - Möglichkeit ein Skript in eine art ORder hinzuzufügen (um mehrere Skripts zu bündeln)
  - Nichts programmieren, nur umsetzung planen wie dies gemacht werden könnte
  - ggf. Virutelles Datei verzeichniss... ( suche ggf anpassen auch api muss hierzu funktioniren)

- [ ] **Internationalisierung prüfen (DE/EN)**
  - Alle UI Texte fixiert auf Deutsch stellen (CH-DE, nicht DE)

## P3 – Optional / Strategisch

- [ ] **Import/Export grosser Mengen vorbereiten**
  - Bulk-Import/Bulk-Export von Skripten konzipieren.
  - Schnittstelle zur geplanten SSMS-Extension berücksichtigen.
  - Nichts programmieren einfach mal nur planen unter /docs Folder

- [ ] **Produktentscheidung „T-SQL Script Library“**
  -„T-SQL Script Library“ gleich unter dem Titel "SqlFrögä" entfernen.

---


## Definition of Done (für alle Tasks)

- Akzeptanzkriterien pro Task schriftlich erfasst.
- Reproduzierbare Tests für Bugfixes vorhanden.
- UI-Änderungen dokumentiert (vorher/nachher).
- Changelog-Eintrag bzw. Release-Note vorbereitet.
