# Nächste Schritte: Zentrale API für SqlFrögä-Extensions

Dieses Dokument beschreibt ein pragmatisches Vorgehen für **eine einzige zentral deployte API** (ohne API-Deployment bei Kunden), die von SSMS- und FlowLauncher-Extensions genutzt wird.

## Zielbild

- Eine zentrale `SqlFroega.Api` (ASP.NET Core) nutzt die bestehenden Application-/Infrastructure-Schichten.
- Extensions (SSMS, FlowLauncher) sind dünne Clients gegen HTTP-Endpunkte.
- Kein direkter DB-Zugriff aus Extensions; DB-Zugriff bleibt nur im API-Backend.

## 1) Funktionsschnitt aus bestehender App übernehmen

Die bestehenden Repositories sind bereits ein gutes API-Backbone. Erste Endpunkte sollten diese Use-Cases abdecken:

- Auth/Login (`IUserRepository`)
- Script-Suche und Script-Details (`IScriptRepository`)
- Script speichern/löschen und Locks (`IScriptRepository`)
- Kunden-Mapping + Render-Funktionen (`ICustomerMappingRepository`, Render-Service)

### Minimaler Endpoint-Scope (v1)

- `POST /api/v1/auth/login`
- `GET /api/v1/scripts?query=&scope=&module=&take=&skip=`
- `GET /api/v1/scripts/{id}`
- `POST /api/v1/scripts` (create/update)
- `DELETE /api/v1/scripts/{id}`
- `POST /api/v1/scripts/{id}/locks/acquire`
- `POST /api/v1/scripts/{id}/locks/release`
- `GET /api/v1/customers/mappings`
- `POST /api/v1/render/{customerCode}`

## 2) Auth-Strategie für zentrale API

Da die API zentral läuft und von mehreren Tools genutzt wird:

- Kurzlebige **JWT Access Tokens** (z. B. 15 Minuten)
- **Refresh Tokens** (z. B. 8–24h, rotierend)
- Rollen/Scopes:
  - `scripts.read`
  - `scripts.write`
  - `mappings.read`
  - `admin.users`
- Audit-Log pro Request: User, Scope, Endpoint, Timestamp, CorrelationId

## 3) Mandanten-/Kontextmodell für Dienstleisterbetrieb

Da ihr als Dienstleister in mehreren Firmenkontexten arbeitet:

- API-seitig ein explizites `TenantContext`/`CustomerContext` einführen.
- Jede schreibende Operation mit Kontext kennzeichnen (Header oder Claim).
- Serverseitige Validierung, dass Benutzer nur erlaubte Kontexte nutzt.
- Ergebnis: klare Trennung und nachvollziehbare Änderungen.

## 4) Stabilität & Sicherheit vor breitem Rollout

Vor SSMS-/FlowLauncher-Integration:

- Rate Limiting und Request-Größenlimits
- Zentrale strukturierte Logs + Tracing (CorrelationId)
- Health-Check Endpunkte (`/health/live`, `/health/ready`)
- Versionierung (`/api/v1/...`) von Anfang an
- Einheitliches Error-Format (ProblemDetails)

## 5) SSMS- und FlowLauncher als dünne Adapter

### SSMS-Extension

- Fokus: Suche, Detailansicht, Render-Preview, Copy-to-Clipboard
- Nur lesende Features in erster Ausbaustufe
- Schreibende Funktionen erst nach erfolgreicher Lock-/Conflict-Strategie

### FlowLauncher-Extension

- Fokus: ultraschnelle Suche + Aktionen
- Debounced Querying + lokaler Kurzzeitcache (z. B. 30–120 Sekunden)
- Klare Tastatur-Shortcuts für „SQL kopieren“ / „Gerendertes SQL kopieren“

## 6) Migrationspfad ohne Big Bang

1. API v1 mit read-only Endpunkten bereitstellen.
2. Bestehende WinUI-App optional auf API umstellen (Feature Flag), parallel zur DB-Anbindung.
3. SSMS/FlowLauncher read-only anbinden.
4. Schreib-Endpunkte mit Locking + Conflict-Handling freischalten.
5. Alte Direktzugriffe schrittweise reduzieren.

## 7) Konkrete 2-Wochen-Planung (Vorschlag)

### Woche 1

- `SqlFroega.Api` Projekt aufsetzen
- DI-Wiring aus WinUI übernehmen
- Auth-Flow + 3 read-only Endpunkte implementieren
- OpenAPI/Swagger veröffentlichen

### Woche 2

- Lock-Endpunkte + Save/Delete-Endpunkte
- Auditing + strukturierte Logs + Healthchecks
- Minimaler SSMS-Prototyp gegen API
- Last-/Smoke-Tests für Such-Endpunkte

## 8) Definition of Done (v1)

- API ist versioniert, dokumentiert und überwacht
- Extensions laufen ohne direkte DB-Credentials
- Read-Use-Cases in SSMS/FlowLauncher produktiv nutzbar
- Schreib-Use-Cases haben Locking, Audit und nachvollziehbare Fehlerantworten
