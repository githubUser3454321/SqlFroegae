# Nächste Schritte: Zentrale API für SqlFrögä-Extensions

Dieses Dokument beschreibt ein pragmatisches Vorgehen für **eine einzige zentral deployte API** (ohne API-Deployment bei Kunden), die von SSMS- und FlowLauncher-Extensions genutzt wird.

## Umsetzungsstand (aktualisiert)

- **Status gesamt:** **Partialy Done** (v1-Endpoints inkl. Admin-User-Endpoint, JWT + Refresh-Flow inkl. TenantContext-Weitergabe, Rate Limiting, Correlation/Audit-Logging, SQL-persistenter Refresh-Token-Store (mit In-Memory-Fallback) sowie Tenant/CustomerContext via Header oder Claim sind umgesetzt; Client-Adapter und vollständige Produktivhärtung sind weiter offen).

## Zielbild

- Eine zentrale `SqlFroega.Api` (ASP.NET Core) nutzt die bestehenden Application-/Infrastructure-Schichten. **DONE**
- Extensions (SSMS, FlowLauncher) sind dünne Clients gegen HTTP-Endpunkte. **Partialy Done**
- Kein direkter DB-Zugriff aus Extensions; DB-Zugriff bleibt nur im API-Backend. **Partialy Done**

## 1) Funktionsschnitt aus bestehender App übernehmen

Die bestehenden Repositories sind bereits ein gutes API-Backbone. Erste Endpunkte sollten diese Use-Cases abdecken:

- Auth/Login (`IUserRepository`) **DONE**
- Script-Suche und Script-Details (`IScriptRepository`) **DONE**
- Script speichern/löschen und Locks (`IScriptRepository`) **DONE**
- Kunden-Mapping + Render-Funktionen (`ICustomerMappingRepository`, Render-Service) **DONE**

### Minimaler Endpoint-Scope (v1)

- `POST /api/v1/auth/login` **DONE**
- `POST /api/v1/auth/refresh` **DONE**
- `POST /api/v1/auth/logout` **DONE**
- `GET /api/v1/scripts?query=&scope=&module=&take=&skip=` **DONE**
- `GET /api/v1/scripts/{id}` **DONE**
- `POST /api/v1/scripts` (create/update) **DONE**
- `DELETE /api/v1/scripts/{id}` **DONE**
- `POST /api/v1/scripts/{id}/locks/acquire` **DONE**
- `POST /api/v1/scripts/{id}/locks/release` **DONE**
- `GET /api/v1/scripts/{id}/locks/awareness` **DONE**
- `GET /api/v1/customers/mappings` **DONE**
- `POST /api/v1/render/{customerCode}` **DONE**

## 2) Auth-Strategie für zentrale API

Da die API zentral läuft und von mehreren Tools genutzt wird:

- Kurzlebige **JWT Access Tokens** (z. B. 15 Minuten) **DONE**
- **Refresh Tokens** (z. B. 8–24h, rotierend) **DONE** (SQL-persistenter Store implementiert, inkl. Rotation/Revoke und In-Memory-Fallback ohne DB)
- Rollen/Scopes:
  - `scripts.read` **DONE**
  - `scripts.write` **DONE**
  - `mappings.read` **DONE**
  - `admin.users` **DONE** (Claim + geschützter Endpoint `GET /api/v1/admin/users` vorhanden)
- Audit-Log pro Request: User, Scope, Endpoint, Timestamp, CorrelationId **DONE**

## 3) Mandanten-/Kontextmodell für Dienstleisterbetrieb

Da ihr als Dienstleister in mehreren Firmenkontexten arbeitet:

- API-seitig ein optional `TenantContext`/`CustomerContext` einführen. **DONE** (Header `X-Tenant-Context` oder JWT-Claim `tenant_context`)
- Jede schreibende Operation mit Kontext kennzeichnen (Header oder Claim). **DONE**

## 4) Stabilität & Sicherheit vor breitem Rollout

Vor SSMS-/FlowLauncher-Integration:

- Rate Limiting und Request-Größenlimits (Viel spielraum) **DONE**
- Zentrale strukturierte Logs + Tracing (CorrelationId) **DONE**
- Health-Check Endpunkte (`/health/live`, `/health/ready`) **DONE**
- Versionierung (`/api/v1/...`) von Anfang an (zu begin mal nur mit v1 arbeiten, es wird bekannt gegeben ab wann wir auf v2 umstellen) **DONE**
- Einheitliches Error-Format (ProblemDetails) **DONE** (globale Exception-Umsetzung auf ProblemDetails + ValidationProblem in Endpunkten)

## 5) SSMS- und FlowLauncher als dünne Adapter

### SSMS-Extension

- Fokus: Suche, Detailansicht, Render-Preview, Copy-to-Clipboard **NOT DONE**
- Nur lesende Features in erster Ausbaustufe **NOT DONE**
- Schreibende Funktionen erst nach erfolgreicher Lock-/Conflict-Strategie **Partialy Done** (Lock-Endpoints + 409-Conflict bei Lock-Kollision serverseitig vorhanden, clientseitige UX dazu noch offen)

### FlowLauncher-Extension

- Fokus: ultraschnelle Suche + Aktionen **NOT DONE**
- Debounced Querying + lokaler Kurzzeitcache (z. B. 30–120 Sekunden) **NOT DONE**
- nach auswahl des resultat klare unterteilung zwischen für „SQL kopieren“ / „Gerendertes SQL kopieren“ (dann kundenkürzel angeben) **NOT DONE**

## 6) Migrationspfad ohne Big Bang

1. API v1 mit read-only Endpunkten bereitstellen. **DONE**
2. SSMS/FlowLauncher read-only anbinden. **NOT DONE**
3. Schreib-Endpunkte mit Locking + Conflict-Handling freischalten. **DONE**

## 7) Konkrete 2-Wochen-Planung (Vorschlag)

### Woche 1

- `SqlFroega.Api` Projekt aufsetzen **DONE**
- DI-Wiring aus WinUI übernehmen **DONE**
- Auth-Flow + 3 read-only Endpunkte implementieren **DONE**
- OpenAPI/Swagger veröffentlichen **DONE**

### Woche 2

- Lock-Endpunkte + Save/Delete-Endpunkte **DONE**
- Auditing + strukturierte Logs + Healthchecks **DONE**
- Minimaler SSMS-Prototyp gegen API **NOT DONE**
- Last-/Smoke-Tests für Such-Endpunkte **NOT DONE**

## 8) Definition of Done (v1)

- API ist versioniert, dokumentiert und überwacht **DONE**
- Extensions laufen ohne direkte DB-Credentials **NOT DONE**
- Read-Use-Cases in SSMS/FlowLauncher produktiv nutzbar **NOT DONE**
- Schreib-Use-Cases haben Locking, Audit und nachvollziehbare Fehlerantworten **DONE** (Write-Validierung + 409-Conflict für Lock-Kollisionen + Audit vorhanden)
