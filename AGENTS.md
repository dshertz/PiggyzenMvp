# Repository Guidelines

## Project Structure & Module Organization
- `PiggyzenMvp.API/` — REST API (Controllers, Services, EF Core `Data/`, `Models/`, `DTOs/`, `Migrations/`).
- `PiggyzenMvp.Blazor/` — Blazor Server UI (`Components/`, `wwwroot/`, `appsettings*`).
- `PiggyzenMvp.Web/` — Razor Pages site (`Pages/`, `wwwroot/`).
- Solution file: `PiggyzenMvp.sln`.

## Build, Run, and Development Commands
- Restore/build: `dotnet restore` · `dotnet build PiggyzenMvp.sln`.
- Run API: `dotnet run --project PiggyzenMvp.API` → https://localhost:7023 (Swagger in Development).
- Run Blazor: `dotnet run --project PiggyzenMvp.Blazor` → https://localhost:7287.
- Run Razor Pages: `dotnet run --project PiggyzenMvp.Web` → https://localhost:7257.
- Database: SQLite (`piggyzen.db`). Apply migrations: `dotnet ef database update -p PiggyzenMvp.API -s PiggyzenMvp.API`.

## Coding Style & Naming Conventions
- Language: C# (net9.0). Indent 4 spaces; one class per file; keep files under the relevant project area (`Controllers/`, `Services/`, `DTOs/`, `Pages/`, `Components/`).
- Naming: PascalCase for types/methods; camelCase for locals/parameters; `_camelCase` for private fields; async methods end with `Async`.
- DTOs end with `Dto`/`Request`; EF entities singular (e.g., `Transaction`).
- Formatting: `dotnet format` before PRs. Prefer file-scoped namespaces.

## Testing Guidelines
- No test project yet. Recommended: `dotnet new xunit -n PiggyzenMvp.Tests` and reference projects under test.
- Test naming: `ClassNameTests.cs`; methods `MethodName_ShouldExpectedBehavior`.
- Run tests: `dotnet test`.

## Commit & Pull Request Guidelines
- Commit style (recommended): Conventional Commits.
  - Examples: `feat(api): add transaction categorization`, `fix(blazor): handle null ApiBaseUrl`.
- PRs must include: concise description, linked issue(s), screenshots/GIFs for UI changes (Blazor/Web), manual test steps, and any config notes (ports, connection string).

## Security & Configuration Tips
- Do not commit secrets. Use `appsettings.Development.json` locally.
- CORS/URLs: Blazor calls the API via `ApiBaseUrl` in `PiggyzenMvp.Blazor/appsettings.Development.json` (default `http://localhost:5142/`). Keep ports in sync with `Properties/launchSettings.json`.
- Migrations: create with `dotnet ef migrations add <Name> -p PiggyzenMvp.API -s PiggyzenMvp.API`.
- Swagger is Development-only; verify endpoints locally before deployment.
