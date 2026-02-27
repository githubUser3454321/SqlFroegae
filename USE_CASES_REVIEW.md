# Überarbeitete Use-Case-Liste (Stand: aktueller Code)

## 1) Fachliche Use Cases (MVP + Core)

### UC1 – Script suchen
**Akteur:** Entwickler  
**Ziel:** Ein Script schnell über Textsuche und Filter finden.

**Ablauf:**
1. Suchbegriff eingeben (Name/Beschreibung/Content).
2. Optional Filter setzen (Scope, CustomerId, Modul, Tags).
3. Suche starten.
4. Trefferliste prüfen.

**Ergebnis:**
- Liste von `ScriptListItem` inkl. Lösch-Status (bei Temporal-Suche).

**Status:** ✅ **Vollständig umgesetzt**

---

### UC2 – Script anzeigen (Preview)
**Akteur:** Entwickler  
**Ziel:** Script-Inhalt und Metadaten lesen sowie SQL kopieren.

**Ablauf:**
1. Script in der Trefferliste auswählen.
2. Detailansicht mit Metadaten und SQL-Content wird geladen.
3. SQL per „Copy“ in die Zwischenablage kopieren.

**Status:** ✅ **Vollständig umgesetzt**

---

### UC3 – Script bearbeiten
**Akteur:** Entwickler  
**Ziel:** Bestehendes Script ändern und versioniert speichern.

**Ablauf:**
1. Script öffnen.
2. Felder (Name, Key, Scope, Modul, Tags, Content etc.) bearbeiten.
3. Speichern.

**Systemverhalten:**
- Update in zentraler SQL-Server-Tabelle.
- Bei aktivem Temporal Table entsteht automatisch eine neue Version.

**Status:** ✅ **Vollständig umgesetzt**

---

### UC4 – Neues Script erstellen
**Akteur:** Entwickler  
**Ziel:** Neues Script anlegen.

**Ablauf:**
1. „New“ klicken.
2. Pflichtfelder ausfüllen.
3. Speichern.

**Status:** ✅ **Vollständig umgesetzt**

---

### UC5 – Script löschen
**Akteur:** Entwickler  
**Ziel:** Script entfernen.

**Ablauf (Soll):**
1. „Delete“ klicken.
2. Löschung bestätigen.
3. Datensatz wird gelöscht.
4. Optional: Soft Delete statt Hard Delete.

**Ist:**
- Soft Delete ist jetzt als **optionaler Modus** implementiert (Feature-Flag `EnableSoftDelete`).
- Wenn die Tabelle eine `IsDeleted`-Spalte besitzt, setzt Delete auf `IsDeleted = 1`.
- Suche und Detail-Load blenden soft-gelöschte Datensätze standardmäßig aus; per Filter können sie eingeblendet werden.
- Fallback bleibt Hard Delete, falls Soft Delete deaktiviert ist oder die Spalte fehlt.

**Status:** ✅ **Vollständig umgesetzt (optional konfigurierbar)**

---

## 2) Erweiterte (geplante) Use Cases

### UC6 – Script-History anzeigen (Temporal)
**Ziel:** Versionen eines Scripts anzeigen und vergleichen.

**Ist:**
- Historie wird geladen und als Liste angezeigt.
- Einzelne Historienstände können geöffnet und gelesen werden.
- Historienstand kann direkt in den Editor zurückgeladen und anschließend gespeichert werden (Restore-Flow).

**Status:** ✅ **Vollständig umgesetzt**

---

### UC7 – Referenzsuche (Parsing)
**Ziel:** Abfragen wie „Welche Scripts referenzieren Tabelle X?“

**Ist:**
- Keine Parser-/Referenzsuch-Implementierung sichtbar.

**Status:** ⛔ **Nicht umgesetzt**

---

### UC8 – Kunden-Mapping / Rendering
**Ziel:** Script abhängig vom Kundenkontext angepasst rendern.

**Ist:**
- Abstraktionen/Domain-Typen sind angelegt.
- Keine konkrete Implementierung im Infrastructure/UI-Fluss.

**Status:** 🟡 **Teilweise umgesetzt (Vorbereitung vorhanden)**

---

### UC9 – Tagging / Metadatenpflege
**Ziel:** Tags, Modul und Scope pflegen.

**Ist:**
- Tags, Modul und Scope sind in Suche + Editiermaske integriert.
- Eigene Verwaltungsoberflächen (z. B. Tag-Katalog/Modul-Registry) fehlen.

**Status:** 🟡 **Teilweise umgesetzt**

---

### UC10 – Performance-Suche (Index / FTS)
**Ziel:** Sehr schnelle Volltextsuche.

**Ist:**
- Schalter für SQL Server Full-Text Search ist vorhanden.
- Optionales SQLite-FTS5-Konzept ist nicht umgesetzt.
- Tatsächliche Performance hängt von DB-Konfiguration (FTS-Indizes) ab.

**Status:** 🟡 **Teilweise umgesetzt**

---

## 3) Ergebnislisten nach deinem Wunsch

## Bereits **vollständig erledigt**
- UC1 – Script suchen
- UC2 – Script anzeigen (Preview)
- UC3 – Script bearbeiten
- UC4 – Neues Script erstellen
- UC5 – Script löschen (optional Soft Delete)
- UC6 – Script-History (inkl. Restore-Flow)

## Noch **teilweise oder ganz** umzusetzen
- UC7 – Referenzsuche (komplett offen)
- UC8 – Kunden-Mapping/Rendering (nur vorbereitet)
- UC9 – Tagging/Metadatenpflege (Grundlagen da, Management-Funktionen offen)
- UC10 – Performance-Suche (teilweise, abhängig von FTS-Ausbau)

---

## 4) Kurzempfehlung für die nächsten Schritte (priorisiert)
1. **UC7 starten:** ScriptDom-basierte Objekt-Referenzextraktion + Index-Tabelle aufbauen.
2. **UC8 konkretisieren:** Mapping-Pipeline (Rules laden → Rendern → Preview/Copy rendered).
3. **UC10 messbar machen:** FTS-Indexing + Benchmark-Szenarien definieren.
4. **UC9 erweitern:** Verwaltung für Tag-Katalog und Modul-Registry ergänzen.
