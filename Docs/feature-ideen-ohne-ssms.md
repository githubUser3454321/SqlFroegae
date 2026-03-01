# Feature-Ideen (ohne SSMS-Fokus)

## 1) Smart Impact Preview ("Was ändert sich bei Kunde X?")

### Idee
Vor dem Kopieren/Exportieren zeigt die App eine kompakte Vorschau:
- referenzierte Objekte
- welche Objekte durch Mapping konkret umgeschrieben werden
- Diff zwischen kanonischem SQL und gerendertem Kunden-SQL

### Warum das stark ist
- Nutzt eure bestehende Stärke im AST-basierten Parsing/Normalisieren.
- Reduziert Fehler vor produktiven Runs (falscher `customerCode`, unerwartete Prefix-Auflösung).
- Passt perfekt zu FlowLauncher "Copy Rendered SQL" als schneller Guardrail.

### MVP-Scope
- Neuer API-Endpunkt: `POST /api/v1/render/{customerCode}/preview`
- Response enthält:
  - `renderedSql`
  - `rewrittenObjects[]` (alt/neu)
  - `warnings[]` (z. B. ambige Mapping-Situationen)
- FlowLauncher: zusätzlicher Kontextmenüpunkt "Copy Rendered SQL + Preview"

## 2) Script Usage Insights (Top-Skripte, Top-Module, Suchlücken)

### Idee
Ein kleines Analytics-Dashboard (WinUI + optional API-Endpunkt), das zeigt:
- meistgesuchte Skripte
- häufige Suchanfragen ohne Treffer
- meistgenutzte Kundenkürzel beim Rendern

### Nutzen
- Hilft beim Aufräumen der Bibliothek (Tags, Modulnamen, Synonyme).
- Macht den Mehrwert des Systems messbar (Zeitersparnis, Trefferquote).
- Liefert Input für Priorisierung neuer Mappings/Module.

## 3) "Verified Snippets" / Freigabe-Status

### Idee
Skripte erhalten einen optionalen Qualitätsstatus:
- `Draft`
- `Reviewed`
- `Verified for Customer X`

### Nutzen
- Teams sehen sofort, was für produktive Einsätze verlässlich ist.
- Guter Fit zu eurer Historie + Edit-Lock-Strategie.
- Später erweiterbar um Pflicht-Review bei sensiblen Modulen.

## Priorisierte Empfehlung
Wenn ihr **nur ein** neues Feature als "cool + nützlich" umsetzt:

1. **Smart Impact Preview** zuerst (höchster direkter Benutzerwert, geringe Konzeptlücke zur bestehenden Architektur).
2. Danach **Usage Insights** für datengetriebene Produktverbesserung.
3. Anschließend **Verified Snippets** für Governance/Reife.
