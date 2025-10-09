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

## Context7 Integration

Codex in this workspace is configured to use **Context7 MCP**, a high‑performance local mirror of Microsoft’s official documentation for:

- [.NET](https://learn.microsoft.com/en-us/dotnet)
- [ASP.NET Core](https://learn.microsoft.com/en-us/aspnet)

Context7 enables Codex agents to:

- Retrieve authoritative framework documentation without external web lookups
- Provide accurate API references, examples, and best‑practice explanations
- Respond quickly while staying aligned with Microsoft’s current guidance

---

### 📘 Coverage

| Area             | Update Frequency | Description                                                |
| ---------------- | ---------------- | ---------------------------------------------------------- |
| .NET             | Weekly           | Core runtime, configuration, dependency injection, EF Core |
| ASP.NET          | Quarterly        | Middleware, controllers, routing, hosting, authentication  |
| EF Core / Blazor | Included         | Common usage patterns and API samples                      |

---

### 🤖 Agent Behavior

- When a query contains `.NET`, `ASP.NET`, `Blazor`, `EF Core`, or `MediatR`, it is **automatically routed to Context7**.
- Agents (`dotnetExpert`, `devAgent`, `reviewAgent`, `docAgent`) prioritize Context7 for framework‑related topics.
- Codex combines Context7 results with project context for framework‑aware answers.

---

### ⚖️ When to Use (and Not Use) Context7

| Situation                                      | Use Context7? | Reason                                                   |
| ---------------------------------------------- | ------------- | -------------------------------------------------------- |
| Looking up Microsoft APIs or framework classes | ✅ Yes        | e.g. `HttpContext`, `AddDbContext`, `IServiceCollection` |
| Asking for .NET / ASP.NET Core best practices  | ✅ Yes        | Dependency injection, middleware, hosting setup          |
| Requesting official docs or examples           | ✅ Yes        | Pulls exact reference from Microsoft docs                |
| Debugging your own code or refactoring         | ❌ No         | Prefer `local_repo` for implementation details           |
| Working on domain models, DTOs, or handlers    | ❌ No         | These are project‑specific, not framework                |
| Handling runtime errors or exceptions          | ❌ No         | Context7 doesn’t reflect your local stack                |

---

### 💡 Examples

```text
> devAgent: "How do I register middleware in ASP.NET Core?"
→ Context7 returns the official example from learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.usemiddlewareextensions

> devAgent: "Why does CategorizationService throw null reference?"
→ Uses local_repo — Context7 is skipped.
```
