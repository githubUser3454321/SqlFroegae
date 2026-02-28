# SqlFrögä (SqlFroega)

SqlFrögä is a Windows desktop tool (WinUI 3, .NET 8) for managing SQL scripts in a central SQL Server database.

It is built to help developers:
- find scripts quickly,
- view and edit script content and metadata,
- track historical versions (temporal tables),
- and render customer-specific SQL variants from canonical tenant-style scripts.

## What this project contains

The solution is split into layered projects:

- `SqlFrögä/` – WinUI desktop application (UI + dependency wiring)
- `SqlFroega.Application/` – application contracts and models
- `SqlFroega.Domain/` – core domain entities/enums
- `SqlFroega.Infrastructure/` – SQL Server persistence + SQL parsing/rendering logic
- `SqlFroega.Tests/` – automated tests

## Core capabilities

- Script search with filters (scope, customer, module, tags, referenced object)
- Script create, preview, update, delete (including optional soft-delete support)
- Script history retrieval for temporal data
- SQL object reference extraction and lookup support
- Customer mapping + rendered SQL copy flow for customer-specific contexts
- Metadata catalog (modules/tags)

## Login process

- User authentication checks `dbo.Users` first.
- If `dbo.Users` contains **no** entries, a fallback fake admin is accepted with credentials `admin` / `admin`.
- If at least one user exists in `dbo.Users`, only active DB users are accepted for username/password login.
- Optional **"Angemeldet bleiben"** uses `dbo.AuthenticatedDevices` with `(UserId, WindowsUserName, ComputerName)`.
- On app startup, a matching `(WindowsUserName, ComputerName)` entry logs the mapped active user in automatically (no password prompt).
  - Next login can succeed without password when username + current Windows user + current computer name match a remembered device.
  - If Windows account/device differs, normal password login is required again.
- `WindowsUserName` is resolved with fallback (`USERNAME`/`Environment.UserName`/`USER`/`LOGNAME`), so offline/local VM sign-ins are supported too.

## Technology stack

- .NET 8
- WinUI 3 desktop app
- CommunityToolkit.Mvvm
- Microsoft.Extensions.DependencyInjection / Hosting abstractions
- SQL Server backend
- Microsoft.SqlServer.TransactSql.ScriptDom for SQL parsing and AST-based transforms

## Getting started

### 1) Prerequisites

- Windows 10/11 with WinUI 3 support
- .NET 8 SDK
- SQL Server instance reachable from the app

### 2) Configure database connection

Connection options are currently wired in `SqlFrögä/App.xaml.cs` (see `SqlServerOptions` setup).
Adjust these values for your environment:

- `ConnectionString`
- `ScriptsTable`
- `CustomersTable`
- optional flags like `UseFullTextSearch`, `JoinCustomers`, and `EnableSoftDelete`

### 3) Build

```bash
dotnet build "SqlFrögä.slnx"
```

### 4) Run tests

```bash
dotnet test "SqlFrögä.slnx"
```

### 5) Run the app

Open the solution in Visual Studio (Windows) and run the `SqlFrögä` startup project.

## Notes

- The app expects a SQL schema that includes script/customer-related tables used by the repositories.
- Some advanced behavior (temporal history, full-text search, soft delete) depends on database-side configuration.

---

