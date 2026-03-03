# Evaluierung: Einsatz von `bruce-dunwiddie/tsql-parser` statt `Microsoft.SqlServer.TransactSql.ScriptDom`

## Kurzfazit
Ein **vollständiger Austausch** des aktuellen Parsers wäre im aktuellen Stand **eher aufwendig (hoch)**.
Der Hauptgrund ist, dass SqlFrögä nicht nur tokenisiert/parst, sondern heute stark auf **AST-Besucher (Visitor)**, **sicherheitsrelevante Strukturprüfungen** und insbesondere **SQL-Formatierung per Script Generator** aufsetzt.

## Ausgangslage im aktuellen Code
Der aktuelle Parser (`TSql160Parser` aus ScriptDom) wird in zentralen Parsing-Pfaden verwendet:

- SQL-Normalisierung und Rendering: `SqlCustomerRenderService`
  - Parsing mit `TSql160Parser`
  - Sicherheitsprüfung via `TSqlFragmentVisitor` (z. B. `USE` verbieten, 3-teilige Namen verbieten)
  - Formatierung via `Sql160ScriptGenerator`.
- Objekt- und Referenzanalyse: `SqlObjectReferenceExtractor`
  - Parsing mit `TSql160Parser`
  - umfassende Visitor-Logik für Tabellen, Views, Prozeduren, Funktionen, CTEs, APPLY/PIVOT/OpenJson etc.
- Umfangreiche Tests, die das heutige Verhalten absichern (Normalisierung, Formatierung, Fehlerdiagnostik, Extraktion).

## Was `tsql-parser` (bruce-dunwiddie) gut kann
Laut Projektbeschreibung bietet die Library:

- Vollständigen Tokenizer für T-SQL (Streaming-fähig).
- Parser-Unterstützung insbesondere für `SELECT`, `INSERT`, `UPDATE`, `DELETE`, `MERGE`.
- Gute Eignung für Token-/Statement-orientierte Aufgaben (z. B. Dependency Parsing, Find/Replace, Kommentar-Parsing).

Das ist für bestimmte Teilaufgaben attraktiv, z. B. wenn man nur robuste Tokenisierung oder einfache Statement-Klassifikation benötigt.

## Vorteile eines Wechsels
1. **Leichtgewichtiger Ansatz für Token-Workflows**  
   Für reine Token- oder Statement-Analysen kann die Library schlank und gut kontrollierbar sein.

2. **Streaming-Tokenizer**  
   Potenziell nützlich für sehr große Skripte oder inkrementelle Analysen.

3. **Apache-2.0-Lizenz**  
   Unproblematisch für kommerzielle Nutzung.

4. **Gute Basis für spezialisierte eigene Parserlogik**  
   Wenn ihr bewusst eine eigene Domänen-Logik über Tokens bauen wollt, kann das passen.

## Nachteile / Risiken im Kontext dieser Codebasis
1. **Kein Drop-in-Ersatz für ScriptDom-AST**  
   Eure aktuelle Logik basiert intensiv auf ScriptDom-Typen (`TSqlFragment`, `TSqlFragmentVisitor`, konkrete Knotenklassen). Diese Architektur müsste in großen Teilen neu gebaut werden.

2. **Formatierung ist kritisch betroffen**  
   Aktuell wird via `Sql160ScriptGenerator` standardisiert formatiert (Uppercase-Keywords, Join/Clause-Newlines etc.). Ein äquivalenter Formatter steht in `tsql-parser` nicht in derselben Form bereit. Ihr müsstet eigenen Formatter bauen oder ein weiteres Tool kombinieren.

3. **Sicherheits- und Regelprüfungen müssten neu implementiert werden**  
   Prüfungen wie `USE`-Verbot und Verbot datenbankqualifizierter Objektpfade sind heute AST-basiert und relativ robust. Tokenbasierte Reimplementierung ist fehleranfälliger und aufwendig.

4. **Objektreferenz-Extraktion würde deutlich komplexer**  
   Die bestehende Visitor-Logik behandelt CTEs, Aliase, Derived Tables, APPLY/PIVOT/OPENJSON etc. Das auf Tokenbasis in gleicher Qualität nachzubauen ist substanzieller Aufwand.

5. **Verhaltensrisiko bei T-SQL-Dialekt/Versionen**  
   Ihr nutzt bewusst `TSql160Parser` (SQL Server 2022-Dialekt). Ein Wechsel kann Dialekt- und Kompatibilitätsabweichungen erzeugen, die sich erst in Randfällen zeigen.

6. **Migrationskosten + Testaufwand hoch**  
   Selbst bei guter Testabdeckung wäre die Migration risikoreich, weil Parser-Fehler sich oft erst bei realen Kunden-Skripten zeigen.

## Aufwandsschätzung

### 1) Vollständiger Ersatz (heutige Funktionalität beibehalten)
**Aufwand: hoch (mehrere Wochen, je nach Teamgröße 3–8+ Wochen).**

Enthält typischerweise:
- Neues internes AST-/Modellkonzept oder umfangreiche Token-Regel-Engine.
- Nachbau von Rewrite-Logik (Schema/Präfix), Safety-Checks, Diagnoseaufbereitung.
- Ersatz für Formatierung (eigener Formatter oder Dritttool-Integration).
- Erweiterte Regressionstests mit realen Kundenskripten.

### 2) Hybrid-Ansatz (empfohlen, falls Interesse besteht)
**Aufwand: niedrig bis mittel (1–3 Wochen für Pilot).**

- ScriptDom bleibt für Rendering/Formatierung/Safety-kritische Pfade.
- `tsql-parser` wird nur für zusätzliche Analyse- oder Token-Use-Cases getestet.
- Risiko deutlich geringer, da Kernverhalten unverändert bleibt.

## Empfehlung
Für diese Codebasis ist ein kompletter Wechsel derzeit **nicht empfehlenswert**, wenn Stabilität und Funktionsparität Priorität haben.

Sinnvoller ist ein **Hybrid-/Pilot-Ansatz**:
1. Einen klar abgegrenzten Use Case wählen (z. B. reine Metadaten-/Tokenanalyse).
2. Parallel auf einem Feature-Flag implementieren.
3. Gegen bestehende Tests + echte SQL-Samples benchmarken.
4. Erst danach über breitere Adoption entscheiden.

## Konkrete Entscheidungsfragen für ein PoC
- Reicht euch token-/statement-basierte Analyse ohne vollständiges AST-Rewriting?
- Wie wichtig ist die bestehende, reproduzierbare Formatierung via Script Generator?
- Welche SQL-Server-Dialekte (2016/2019/2022) müssen zuverlässig unterstützt werden?
- Wie viele echte Kundenskripte könnt ihr als Regression-Suite bereitstellen?

---
Stand: Erste Machbarkeitsbewertung auf Basis der aktuellen SqlFrögä-Architektur und der öffentlich dokumentierten Features von `tsql-parser`.
