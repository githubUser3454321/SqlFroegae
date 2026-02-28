# UC7/UC8 technische Notizen (ScriptDom)

## Umgesetzter Ansatz
- SQL wird über `Microsoft.SqlServer.TransactSql.ScriptDom` geparst.
- UC8-Ersetzung (`DatabaseUser` + `ObjectPrefix`) erfolgt auf AST-Knoten (`SchemaObjectName`), nicht per Regex.
- UC7-Referenzen werden beim Speichern aus dem AST extrahiert und in `dbo.ScriptObjectRefs` neu aufgebaut (Delete + Insert).

## Bekannte Grenzen
- **Dynamic SQL** (z. B. `EXEC(@sql)`) wird nicht tief analysiert; String-Inhalte werden nicht weiter geparst.
- **Keine vollständige Name-Resolution** gegen live DB-Metadaten (Synonyme, temporäre Auflösung, Kontextdatenbanken etc.).
- Objektarten werden best-effort aus AST-Knoten bestimmt; wenn nicht ableitbar, bleibt `Unknown`.
