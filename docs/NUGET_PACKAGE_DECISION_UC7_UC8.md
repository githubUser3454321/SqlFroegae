# NuGet-Entscheidung für UC7/UC8 (SqlFrögä)

## Kurzfazit
Für eure Anforderungen ist **`Microsoft.SqlServer.TransactSql.ScriptDom`** das beste Paket als primäre Basis.
- ist istalliert.
Ihr nutzt ScriptDom bereits in `SqlCustomerRenderService` für Parse-Validierung vor dem Speichern; das passt exakt zur Anforderung „syntaktische Ersetzung“ bei `DatabaseUser` und `ObjectPrefix`. Für UC7 (Referenzen beim Speichern erzeugen) reicht ScriptDom ebenfalls sehr gut, wenn die Referenzbildung aus dem SQL-Text erfolgen soll (Tabellen/Views/Funktionen, die im Skript explizit auftauchen).

## Begründung anhand eurer Use Cases

### UC8 – DatabaseUser/ObjectPrefix syntaktisch ersetzen
- ScriptDom liefert robustes Parsing + AST und verhindert „blindes Regex“.
- Dadurch kann die Ersetzung auf **Objektknoten** (z. B. `SchemaObjectName`) statt auf reinen Stringmustern angewendet werden.
- Das verbessert die bestehende Logik in `SqlCustomerRenderService` deutlich (derzeit Parse + Regex), insbesondere bei Brackets, Casing, Whitespace, Multipart Names.

### UC7 – Referenzen beim Speichern erzeugen
- Mit ScriptDom können beim Speichern alle relevanten Referenzknoten traversiert und in einer Referenz-Tabelle persistiert werden.
- Für ein Script-Repository ist das in der Regel ausreichend und deploybar.
- Einschränkungen (Dynamic SQL, echte Name-Resolution über Kontext) sind bekannt und für MVP/UC7 meist akzeptabel.

## Warum nicht primär SqlParser oder DacFx?

### `Microsoft.SqlServer.Management.SqlParser`
- Mehr „Binding“-Charakter, aber deutlich schwergewichtiger und häufig aufwendiger in Betrieb/Versionierung.
- Für euren Scope (syntaktische Ersetzung + Referenzindex aus Script-Text) entsteht wenig Mehrwert gegenüber ScriptDom.

### `Microsoft.SqlServer.DacFx`
- Stark für modellbasierte Szenarien (DACPAC, Schema-Vergleich, Deployment).
- Für euren Workflow würde DacFx zusätzlichen Modell-/Referenzkontext benötigen, bevor es bei Auflösung wirklich besser wird.
- Für UC7/UC8 als unmittelbare App-Funktion unnötig komplex.

## Empfehlung
1. **Bei ScriptDom bleiben** (primäres Paket).
2. Ersetzung in UC8 von Regex auf AST-basierte Rewriter-Logik umstellen.
3. UC7 als Save-Pipeline-Schritt implementieren: Parse → Referenzen extrahieren → persistieren.
4. Optional später: DacFx nur ergänzen, falls ihr bewusst in Richtung DACPAC/Modellanalyse erweitert.

## Konkreter technischer Zielzustand
- `NormalizeForStorageAsync` arbeitet AST-basiert für Identifier (`schema.object`), nicht regex-zentriert.
- Referenzextraktion läuft beim Speichern zentral in der Application/Infrastructure-Pipeline.
- Referenzdaten werden versioniert/erneuerbar gespeichert (Delete old refs for script version + insert new refs).
- Parsing-Fehler blockieren Speichern wie heute.

