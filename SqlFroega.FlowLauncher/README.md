# SqlFroega.FlowLauncher

FlowLauncher-Plugin (C#) für die bestehende `SqlFroega.Api`.

## v1 Features

- Skript-Suche via `GET /api/v1/scripts`
- `Copy SQL` via `GET /api/v1/scripts/{id}`
- `Copy Rendered SQL` via `POST /api/v1/render/{customerCode}`
- Login + Token-Refresh (`/auth/login`, `/auth/refresh`)
- Header-Support für `X-Tenant-Context` und `X-Correlation-Id`
- Kurzzeit-Cache für Suchergebnisse

## Plugin-Settings

- `ApiBaseUrl`
- `Username`
- `Password`
- `DefaultTenantContext` (optional)
- `DefaultCustomerCode` (optional)

## Build

```bash
dotnet build SqlFroega.FlowLauncher/SqlFroega.FlowLauncher.csproj
```

## Referenz

Das Grundgerüst orientiert sich am offiziellen C#-Sample:
`https://github.com/Flow-Launcher/plugin-samples/tree/master/HelloWorldCSharp`
