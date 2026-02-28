# Follow-up Prompt für die nächste Codex-Instanz (Implementierung UC7/UC8)

Du arbeitest im Repo `SqlFroegae`.

## Ziel
Implementiere UC7 + UC8 technisch sauber auf Basis von **Microsoft.SqlServer.TransactSql.ScriptDom** (kein DacFx, kein SqlParser als Primärpfad).

## Kontext
- Aktuell wird in `SqlFroega.Infrastructure/Parsing/SqlCustomerRenderService.cs` SQL mit ScriptDom validiert und anschließend per Regex normalisiert.
- UC8 soll syntaktisch robust sein: Ersetzung von `DatabaseUser` + `ObjectPrefix` auf AST-Ebene.
- UC7 soll beim Speichern eines Scripts Referenzen erzeugen (Tabellen/Views etc.), analog „Referenzsuche“.

## Anforderungen

### 1) UC8: AST-basierte Normalisierung/Rendering
- Ersetze die Regex-zentrierte Logik in `SqlCustomerRenderService` durch AST-basierte Verarbeitung.
- Nutze ScriptDom Parser (`TSql160Parser`) + AST Traversal/Visitor.
- Zielverhalten beibehalten:
  - Beim Speichern ohne `CustomerId`: auf kanonisch `om.om_...` normalisieren.
  - Beim Speichern mit `CustomerId`: SQL unverändert speichern.
  - Beim Rendern für Kunden: von kanonisch auf Mapping des Zielkunden umsetzen.
- Sicherstellen, dass Bracketed Identifier, Case und Whitespace keine falschen Ersetzungen erzeugen.

### 2) UC7: Referenzextraktion beim Speichern
- Implementiere eine Referenzextraktion über ScriptDom AST.
- Extrahiere mindestens:
  - `schema.object` Referenzen aus DML/DDL-Knoten (SELECT/INSERT/UPDATE/DELETE/MERGE, CREATE/ALTER sofern sinnvoll).
  - Objektart (z. B. Table, View, Procedure, Function) soweit aus Knoten ableitbar; sonst `Unknown`.
- Persistiere Referenzen in SQL:
  - Neue Tabelle `dbo.ScriptObjectRefs` (oder konfigurierbar via Options), inkl. `ScriptId`, `ObjectName`, `ObjectType`, `CreatedAt`.
  - Beim Speichern eines Scripts: bestehende Referenzen zu `ScriptId` löschen und neu schreiben (idempotenter Rebuild).
- Integriere in bestehende Save-Pipeline im Repository/Application-Layer.

### 3) Query/Use in App vorbereiten
- Ergänze Repository-API für „find scripts by referenced object“ (Basis für UC7 UI später).
- UI muss noch nicht komplett ausgebaut sein, aber die Backend-Funktion soll testbar sein.

### 4) Tests
- Füge Unit-Tests hinzu für:
  - Parserfehler blockieren Save.
  - AST-Ersetzung korrekt bei:
    - `[om].[om_table]`
    - `om.om_table`
    - gemischte Schreibweise/Whitespace.
  - Referenzextraktion bei Joins, Aliases, CTEs.
  - Dynamic SQL wird nicht tief analysiert (nur dokumentiertes Verhalten).

### 5) Doku
- Aktualisiere `USE_CASES_REVIEW.md`:
  - UC7 Status auf „umgesetzt“ (wenn implementiert).
  - UC8 Hinweis auf AST-basierte Ersetzung statt Regex.
- Kurze technische Notiz ergänzen (z. B. in `docs/`) mit bekannten Grenzen (Dynamic SQL, keine vollständige Name-Resolution).

## Qualitätskriterien
- Keine try/catch um Imports.
- Saubere Layer-Trennung (Application-Abstraktionen vs Infrastructure-Implementierung).
- Bestehende Funktionalität darf nicht regressieren.
- Build + Tests müssen lokal laufen.

## Ausführung
1. Relevante AGENTS.md beachten.
2. Implementieren.
3. `dotnet build` + `dotnet test` ausführen.
4. Commit mit aussagekräftiger Message.
5. PR-Text mit Änderungen, Testbelegen und offenen Restpunkten.
