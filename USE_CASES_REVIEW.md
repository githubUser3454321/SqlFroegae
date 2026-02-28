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
- Beim Speichern werden Referenzen über ScriptDom-AST extrahiert und idempotent in `dbo.ScriptObjectRefs` neu aufgebaut.
- Backend-API `FindByReferencedObjectAsync` liefert Scripts für ein referenziertes Objekt (Basis für spätere UI-Suche).

**Status:** ✅ **Umgesetzt**

---

### UC8 – Kunden-Mapping / Rendering
**Ziel:** Script abhängig vom Kundenkontext angepasst rendern.

**Ist:**
- Kunden-Mapping ist jetzt konkret umgesetzt (`CustomerCode`, `DatabaseUser`, `ObjectPrefix`) inkl. persistenter SQL-Tabelle `dbo.CustomerMappings`.
- Beim Speichern ohne `CustomerId` wird SQL mit Microsoft ScriptDom geparst/validiert und AST-basiert auf kanonische Tenant-Notation (`om.om_...`) normalisiert (kein Regex-Primärpfad).
- Beim Speichern mit gesetzter `CustomerId` bleibt SQL unverändert (as-is).
- In der Script-Detailansicht gibt es neben „Copy“ ein Kundenkürzel-Feld (AutoSuggest) + „Copy Rendered“, um Platzhalter direkt in den Zielkunden-Kontext zu übersetzen.
- Eine einfache Mapping-Maske (anlegen/refresh) ist in der Detailansicht vorhanden.

**Status:** ✅ **MVP umgesetzt**

---

### UC9 – Tagging / Metadatenpflege
**Ziel:** Tags, Modul und Scope pflegen.

**Ist:**
- Tags, Modul und Scope sind in Suche + Editiermaske integriert.
- Es gibt jetzt einen **Tag- & Modul-Katalog** in der Suche, der vorhandene Werte aus der DB lädt und als klickbare Einträge anbietet.
- Module können direkt als Filter übernommen werden; Tags lassen sich per Klick zur Filterliste hinzufügen/entfernen.
- Katalog kann manuell aktualisiert werden (zusätzlich zum automatischen Refresh nach der Suche).

**Status:** ✅ **Vollständig umgesetzt (Katalog + Pflege im Such-/Filter-Flow)**

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
- UC10 – Performance-Suche (teilweise, abhängig von FTS-Ausbau)

---

## 4) Kurzempfehlung für die nächsten Schritte (priorisiert)
1. **UC7 starten:** ScriptDom-basierte Objekt-Referenzextraktion + Index-Tabelle aufbauen.
2. **UC10 messbar machen:** FTS-Indexing + Benchmark-Szenarien definieren.
3. **UC9 optional ausbauen:** Dedizierte Admin-Verwaltung für kontrollierte Tag-/Modul-Listen ergänzen (Governance, Freigaben, Cleanup).
