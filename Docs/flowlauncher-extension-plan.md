# FlowLauncher-Extension: Umsetzungsplan (Priorität vor SSMS)

Dieser Plan priorisiert eine **schnell nutzbare FlowLauncher-Extension** auf Basis der bereits stehenden API.

## 1) Zielbild für v1 (FlowLauncher zuerst)

Die Extension soll in v1 drei Dinge extrem zuverlässig können:

1. **Skript suchen** (ultraschnell, keyboard-first)
2. **Original-SQL kopieren**
3. **Gerendertes SQL kopieren** (nach Auswahl eines `customerCode`)

Nicht-Ziele für v1:

- Skript bearbeiten/speichern/löschen
- Locking-UX in FlowLauncher
- Admin-/Benutzerverwaltung

## 2) API-Abhängigkeiten (bereits vorhanden)

- `POST /api/v1/auth/login`
- `POST /api/v1/auth/refresh`
- `GET /api/v1/scripts`
- `GET /api/v1/scripts/{id}`
- `POST /api/v1/render/{customerCode}`
- `GET /api/v1/customers/mappings`

Damit ist eine read-only FlowLauncher-Erweiterung inkl. Rendering-Action umsetzbar.

## 3) Konkrete Implementierungsreihenfolge

### Schritt 1: Plugin-Grundgerüst

- Neues FlowLauncher-Plugin-Projekt anlegen (`SqlFroega.FlowLauncher`).
- Settings modellieren:
  - `ApiBaseUrl`
  - `Username`
  - `Password` (oder später Token-first)
  - `DefaultTenantContext` (optional)
  - `DefaultCustomerCode` (optional)

### Schritt 2: API-Client + Token-Handling

- `HttpClient` + API-Client-Klassen kapseln.
- Login beim ersten Request (oder Plugin-Start) durchführen.
- Access-Token im Speicher halten.
- Bei `401` einmalig Refresh versuchen, dann Request wiederholen.
- Header standardisieren:
  - `Authorization: Bearer ...`
  - `X-Tenant-Context` (falls gesetzt)
  - `X-Correlation-Id` je Query neu erzeugen.

### Schritt 3: Query-UX für Suche

- Debounce (ca. 200–300 ms).
- Kurzzeitcache für Suchresultate (30–120 Sekunden).
- Ergebnisliste mit klaren Actions:
  - `Copy SQL`
  - `Copy Rendered SQL`
  - optional `Open Details` (später)

### Schritt 4: Copy-Workflows

- `Copy SQL`:
  1. Details laden (`GET /scripts/{id}`),
  2. SQL in Clipboard.
- `Copy Rendered SQL`:
  1. Customer wählen (Default oder Action-Argument),
  2. `POST /render/{customerCode}`,
  3. Ergebnis in Clipboard.

### Schritt 5: Fehlerbild & Fallbacks

- Netzwerk-/API-Fehler als kurze, benutzbare FlowLauncher-Messages.
- Bei Auth-Fehlern aktive Re-Login-Hinweise.
- Bei fehlendem `customerCode` direkte Auswahlhilfe anbieten.

## 4) Akzeptanzkriterien für „funktionsfähige FlowLauncher-Extension“

- Suche liefert Ergebnisse in < 500 ms (bei warmem Cache typischerweise deutlich schneller).
- `Copy SQL` funktioniert für mindestens 10 repräsentative Skripte.
- `Copy Rendered SQL` funktioniert für mindestens 3 Kundenkürzel.
- Token-Refresh läuft transparent ohne manuelles Eingreifen.
- Fehler sind nutzerverständlich und blockieren den Workflow nicht dauerhaft.

## 5) Empfohlene Reihenfolge danach (SSMS nachgelagert)

1. Stabilisierung + Telemetrie für FlowLauncher (Timeouts, Error-Rates, Cache-Hit-Rate).
2. Erst danach SSMS read-only Adapter starten.
3. Schreibfeatures getrennt einführen (Locking/Conflict UX).
