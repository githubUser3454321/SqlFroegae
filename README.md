# SqlFrögä (SqlFroega)

**SqlFrögä** ist eine WinUI-3-Desktopanwendung (.NET 8) zur zentralen Verwaltung von SQL-Skripten in SQL Server.
Der Fokus liegt auf **schneller Auffindbarkeit**, **kontrollierter Bearbeitung** und **kundenabhängiger SQL-Generierung**.

## Überblick

Das Projekt ist als mehrschichtige Lösung aufgebaut:

- `SqlFrögä/` – WinUI-Frontend, Navigation, Dependency Injection
- `SqlFroega.Application/` – Anwendungsmodelle und Abstraktionen
- `SqlFroega.Domain/` – Domain-Typen und Kernkonzepte
- `SqlFroega.Infrastructure/` – SQL-Server-Repositories, Parsing und Rendering
- `SqlFroega.Tests/` – Unit- und Integrationsnahe Tests

## Aktuelle Features

### 1) Skriptbibliothek & Suche

- Volltext-/Metadatensuche mit kombinierbaren Filtern:
  - Scope (Global / Customer / Module)
  - Hauptmodul, abhängige Module
  - Tags/Flags
  - referenzierte SQL-Objekte
  - Kundenkürzel
- Optionales Einbeziehen von gelöschten Einträgen und Historie in die Suche.
- Metadaten-Katalog für Module und Tags mit schneller Filterung im UI.

### 2) Skriptbearbeitung mit Schutzmechanismen

- Erstellen, Bearbeiten und Löschen von Skripten.
- Unterstützung für:
  - Hauptmodul + Related Modules
  - Tags/Flags
  - Kundenzuordnung pro Skript
  - Beschreibung/Metadaten
- Historie-Ansicht (temporal-basierte Versionen) inkl. Wiederherstellen eines historischen Inhalts in den Editor.
- Bearbeitungssperren (Edit Locks), damit Datensätze nicht parallel überschrieben werden.
- Edit-Awareness-Hinweis, wenn ein Skript seit dem letzten Öffnen von einer anderen Person geändert wurde.
- Änderungsgrund verpflichtend, sobald sich SQL-Inhalt gegenüber der geladenen Version ändert.

### 3) Kunden-Mapping & SQL-Rendering

- Verwaltung von Kunden-Mappings (`CustomerCode`, `DatabaseUser`, `ObjectPrefix`).
- Rendering eines kanonischen SQL-Skripts für einen konkreten Kunden.
- Copy-Workflow für:
  - Original-SQL
  - gerendertes Kunden-SQL
- Automatische Normalisierung beim Speichern (DB-User/Präfix), inkl. Schutz bei uneindeutigen Mapping-Paaren.

### 4) Benutzer- und Zugriffsverwaltung

- Login gegen `dbo.Users` (aktive Benutzer).
- Fallback-Bootstrap-Login `admin/admin`, solange noch keine Benutzer in `dbo.Users` existieren.
- „Angemeldet bleiben“ pro Gerät über `dbo.AuthenticatedDevices` (Windows-User + Computername).
- Admin-Bereiche für:
  - Benutzer anlegen/deaktivieren/reaktivieren
  - Module anlegen/umbenennen/löschen
  - Kunden-Mappings pflegen

## Technologie-Stack

- .NET 8
- WinUI 3
- CommunityToolkit.Mvvm
- Microsoft.Extensions.DependencyInjection
- SQL Server
- Microsoft.SqlServer.TransactSql.ScriptDom

## Einrichtung

### Voraussetzungen

- Windows 10/11
- .NET 8 SDK
- Erreichbare SQL-Server-Instanz

### Konfiguration

Die Datenbankkonfiguration wird aus einer INI-Datei gelesen (`[SqlServer]`-Sektion).
Ein Beispiel liegt im Repository: `config.example.ini`.

Unterstützte Start-Reihenfolge:

1. **Programmargumente** (erste passende INI wird genutzt):
   - `SqlFroega.exe --config "C:\Pfad\config.ini"`
   - `SqlFroega.exe -c "C:\Pfad\config.ini"`
   - oder ein direkt übergebenes `.ini`-Argument
2. **Automatischer Fallback** auf `%AppData%/SqlFroega/config.ini`
3. **Dateiauswahl-Dialog** beim Start, falls 1 und 2 keine gültige INI liefern

Wichtige Parameter in `[SqlServer]`:

- `ConnectionString` (Pflicht)
- `ScriptsTable`
- `CustomersTable`
- `ScriptObjectRefsTable`
- `ModulesTable`
- optional: `UseFullTextSearch`, `JoinCustomers`, `EnableSoftDelete`

Hinweis: Bei ungültiger oder fehlender INI zeigt die App eine Fehlermeldung und fordert erneut zur Dateiauswahl auf.

Zusätzliche API-Roadmap für eine zentral deployte API: `Docs/central-api-next-steps.md`.

FlowLauncher-Priorisierungsplan (v1 zuerst): `Docs/flowlauncher-extension-plan.md`.

Zusätzliche SQL-Hilfsskripte liegen in `Docs/`:
- `001_seed_schema.sql` erstellt alle von der App genutzten Tabellen.
- `002_seed_fill_data_optional_users.sql` füllt optionale Demo-/Seed-Daten (inkl. optionalem Benutzer-Block).

## Build & Test

```bash
dotnet build "SqlFrögä.slnx"
```

```bash
dotnet test "SqlFrögä.slnx"
```

## Anwendung starten

Die App wird über Visual Studio unter Windows gestartet (`SqlFrögä` als Startup-Projekt).

---

Bei Bedarf kann die README im nächsten Schritt um ein kurzes Architekturdiagramm sowie ein konkretes Datenbankschema-Beispiel erweitert werden.

## API-Dokumentation

Die ausführliche API-Dokumentation findest du hier: **[SqlFroega.Api README](SqlFroega.Api/README.md)**.

