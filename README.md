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

If you want, I can also add a dedicated **Database Setup** section with example SQL table definitions and seed data so first-time contributors can run the app faster.
