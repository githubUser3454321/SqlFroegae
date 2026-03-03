# Evaluierung: PoorMansTSqlFormatter als Ersatz der aktuellen Umsetzung

## Kurzfazit
- **Für reines Pretty-Printing (`FormatSqlAsync`)**: relativ einfach integrierbar (**~0,5–1,5 PT**).
- **Als Ersatz der gesamten aktuellen SQL-Logik**: **nicht sinnvoll**; aktuelle Lösung nutzt AST-basiertes Rewriting + Validierung, was PoorMansTSqlFormatter so nicht abdeckt.

## Ist-Zustand im Projekt (heute)
- Formatierung läuft aktuell über `Microsoft.SqlServer.TransactSql.ScriptDom` (`TSql160Parser` + `Sql160ScriptGenerator`).
- Zusätzlich gibt es AST-basierte Sicherheits- und Rewrite-Logik (z. B. `USE`-Blocker, DB-Qualifier-Checks, Schema/Prefix-Rewrites).
- Formatierung ist nur **ein** Teil des Services; Kernfunktionen hängen stark am AST.

## Einbindungsaufwand PoorMansTSqlFormatter

### Technisch einfach
- NuGet-Referenz hinzufügen und in `FormatSqlAsync` umschalten/konfigurierbar machen.
- Optional als Feature-Flag (z. B. `appsettings`) einführen, damit Rückfall auf ScriptDom möglich bleibt.
- Testanpassungen für erwartete Format-Strings nötig (Formatter liefern oft unterschiedliche, aber valide Ergebnisse).

### Technisch aufwendig / riskant
- Als vollständiger Ersatz für `NormalizeForStorageAsync`/`RenderForCustomerAsync` ungeeignet:
  - PoorMansTSqlFormatter ist primär Formatter mit **coarse parsing**, kein robuster AST-Umschreiber.
  - bestehende Business-Regeln (Objekt-Mapping, Sicherheitsregeln) müssten weiterhin separat bleiben.

## Pros
- Gute Lesbarkeit für T-SQL-Batches, Prozedur-Skripte und typische Legacy-SQL-Snippets.
- Fehlertoleranter bei "unsauberem" SQL als strikt parsebasierte Ansätze.
- Viele Konfigurationsoptionen für Stil.
- Reif/lang verfügbar; schnell für Bulk-Formatierung.

## Cons
- **Lizenz: AGPL v3** (starker Copyleft-Effekt, für proprietäre/closed-source Distribution oft kritisch bis blocker).
- Nur "coarse" Parsing; nicht geeignet für sichere, semantische SQL-Transformationen.
- DDL/Edge-Cases laut Projekt selbst nur grob unterstützt.
- Kommentar-/Token-Reordering kann auftreten (nicht immer byte-stabil).
- Projekt wirkt historisch alt (Ursprung .NET 2.0), langfristige Wartbarkeit/Modernität prüfen.

## Einschätzung "statt aktueller Umsetzung"
- **Nein als 1:1-Ersatz** der heutigen Service-Logik.
- **Ja als optionaler Formatter-Adapter** nur für `FormatSqlAsync`, falls:
  - AGPL-Risiko akzeptiert ist,
  - Format-Differenzen fachlich ok sind,
  - bestehende AST-Rewrite-/Safety-Logik unangetastet bleibt.

## Bessere Alternativen?

### 1) Bei .NET + SQL Server Fokus (empfohlen)
- **ScriptDom beibehalten** (Status quo).
- Vorteile:
  - bereits integriert,
  - stabil für SQL Server Dialekt,
  - starker Fit zu eurer AST-basierten Rewrite-/Safety-Architektur.
- Verbesserung eher intern:
  - Formatter-Optionen feinjustieren,
  - eigene Post-Formatting-Regeln kapseln,
  - Snapshot-Tests für Formatierung ergänzen.

### 2) Open-Source Formatter ohne AGPL-Zwang (je nach Lizenzprüfung)
- **sqlfluff** (CLI, Python, T-SQL-Dialekt vorhanden):
  - + modernes Regelwerk/Linting,
  - – externer Runtime-Prozess, Integration komplexer, nicht "nur .NET".
- **sql-formatter (JS/Node)**:
  - + leicht konsumierbar in Toolchains,
  - – T-SQL-Abdeckung prüfen, zusätzlicher Node-Stack.

### 3) Hybrid-Ansatz
- AST-Funktionen (Safety + Rewrites) weiter über ScriptDom.
- Optionaler externer Formatter nur als "Kosmetik" (z. B. beim Copy/Export).
- So bleibt fachliche Korrektheit im Kern und Formatierung austauschbar.

## Empfehlung
- **Kurzfristig**: bei ScriptDom bleiben; kein vollständiger Wechsel auf PoorMansTSqlFormatter.
- **Wenn gewünschte Formatierungsqualität heute nicht reicht**:
  - zuerst bestehende ScriptDom-Optionen/Postprocessing ausbauen,
  - alternativ PoC mit 2 Formattern hinter Interface + Golden-Master-Vergleich auf realen Skripten.
- **License-Gate vor jeder PoorMansTSqlFormatter-Integration** zwingend durchführen.
