# SqlFroega.Api

Zentrale ASP.NET Core API für SqlFrögä-Clients (SSMS-Extension, FlowLauncher-Plugin, weitere Integrationen).

## Ziel

`SqlFroega.Api` stellt die bestehenden Anwendungsfunktionen als HTTP-API bereit, damit Extensions **keine direkten DB-Credentials** mehr benötigen.

## Base URL & Versionierung

Alle Fachendpunkte laufen aktuell unter:

- `/api/v1/...`

Health:

- `GET /health/live`
- `GET /health/ready`

Swagger/OpenAPI ist in Development aktiv.

## Authentifizierung & Tokens

### Login

- `POST /api/v1/auth/login`
- Body:

```json
{
  "username": "admin",
  "password": "admin",
  "tenantContext": "acme"
}
```

- Ergebnis: Access Token (JWT) + Refresh Token.
- Wenn `tenantContext` gesetzt ist, wird dieser als Claim `tenant_context` in den Access Token übernommen.

### Refresh

- `POST /api/v1/auth/refresh`
- Body:

```json
{
  "refreshToken": "..."
}
```

- Rotiert den Refresh Token und liefert einen neuen Access Token.
- `tenant_context` wird aus dem gespeicherten Refresh Token übernommen.

### Logout

- `POST /api/v1/auth/logout`
- Body:

```json
{
  "refreshToken": "..."
}
```

- Revoke des Refresh Tokens.

## Refresh-Token-Speicher

Es gibt zwei Implementierungen:

1. **SQL persistenter Store** (`SqlRefreshTokenStore`)
   - Tabelle: `dbo.ApiRefreshTokens`
   - Auto-Create/Schema-Upgrade (inkl. `TenantContext`-Spalte)
   - Rotation in Transaktion
2. **In-Memory Store** (`InMemoryRefreshTokenStore`)
   - Fallback, wenn kein SQL-ConnectionString gesetzt ist.

Auswahl erfolgt zur Laufzeit über DI.

## Tenant / Customer Context

Schreibende Endpunkte (`POST /scripts`, `DELETE /scripts/{id}`, Lock acquire/release) verlangen Tenant-Kontext.

Akzeptierte Quellen:

1. Header `X-Tenant-Context`
2. JWT Claim `tenant_context`

Wenn beides fehlt, antwortet die API mit `ValidationProblem`.

## Hauptendpunkte (v1)

- `GET /api/v1/scripts`
- `GET /api/v1/scripts/{id}`
- `POST /api/v1/scripts`
- `DELETE /api/v1/scripts/{id}`
- `POST /api/v1/scripts/{id}/locks/acquire`
- `POST /api/v1/scripts/{id}/locks/release`
- `GET /api/v1/scripts/{id}/locks/awareness`
- `GET /api/v1/customers/mappings`
- `POST /api/v1/render/{customerCode}`
- `GET /api/v1/admin/users`


## Such-/Payload-Limits

- `GET /api/v1/scripts` nutzt `take`/`skip` Paging.
- Default: `take=200`, `skip=0`; serverseitig wird `take` auf **maximal 500** begrenzt.
- Große Trefferlisten werden daher in Seiten übertragen, nicht in einem einzelnen Response-Blob.
- SQL-Inhalte (`POST /scripts`, `POST /render/{customerCode}`) sind auf **200.000 Zeichen** begrenzt; größere Payloads liefern ein `ValidationProblem`.

## Sicherheit & Betrieb

- JWT Bearer Auth
- Scope-basierte Policies (`scripts.read`, `scripts.write`, `mappings.read`, `admin.users`)
- Rate Limiting (Fixed Window)
- Correlation/Audit Middleware (`X-Correlation-Id`)
- Einheitliche Fehlerantworten über ProblemDetails/ValidationProblem

## Beispiel-Header für geschützte Requests

```http
Authorization: Bearer <access-token>
X-Tenant-Context: acme
X-Correlation-Id: req-1234
```

## Lokales Starten

```bash
dotnet run --project SqlFroega.Api
```

## Konfiguration (appsettings)

Wichtige Bereiche:

- `SqlServer:ConnectionString`
- `Jwt:Issuer`
- `Jwt:Audience`
- `Jwt:SigningKey`
- `Jwt:AccessTokenMinutes`
- `Jwt:RefreshTokenHours`

### Pflicht: SQL ConnectionString

Die API benötigt immer `SqlServer:ConnectionString`. Ohne diesen Wert beendet sie sich direkt beim Start mit einer klaren Fehlermeldung.

Mögliche Konfigurationen:

- `SqlFroega.Api/appsettings.Development.json`
- Umgebungsvariable `SqlServer__ConnectionString`

Beispiel (PowerShell):

```powershell
$env:SqlServer__ConnectionString = "Server=localhost;Database=SqlFroega;Trusted_Connection=True;TrustServerCertificate=True"
dotnet run --project SqlFroega.Api
```
