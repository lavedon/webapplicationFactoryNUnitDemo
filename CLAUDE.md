The goal of this project is to create a demo of running a .NET 10 Web API in headless mode with WebApplicationFactory<Program> (modern minimal-hosting style — NOT the legacy Startup class), using the `public partial class Program { }` trick at the bottom of Program.cs, and 1. have basic client credential authentication via a local token-issuing endpoint: `POST /token` validates a hardcoded client id/secret and signs its own JWTs (self-contained client-credentials flow, easy to demo with Invoke-RestMethod). A `GET /whoami` endpoint echoes the authenticated principal's claims. Okta integration will happen later on a separate branch. In tests, authentication is bypassed with a fake AuthenticationHandler that returns a successful AuthenticationTicket, registered via ConfigureTestServices.

The tests will be run with NUnit.  The goal is to show full API testing at the endpoint level, as much as possible.  What one would do with Kiota or Postman newman, but using NUnit and WebapplicationFactory headless mode.

# Data

1. We will use sqlite3 with Dapper (NOT EF Core). The sqlite package for .NET 10 should already be installed.

2. The source data was `docs/Taco bell - Google Sheets.pdf`; it has already been extracted to a checked-in seed script at `data/seed.sql`. The PDF is documentation only — no runtime PDF parsing.

3. Database lifecycle: NO in-memory SQLite — we demonstrate full real-database access as a stepping stone toward Testcontainers later. The app creates and seeds `tacobell.db` from `data/seed.sql` on startup if missing. Tests create a fresh temp-file SQLite DB per test, seeded from the same `seed.sql`, with WebApplicationFactory overriding the connection string; temp files are deleted on teardown.

4. Schema (table `MenuItems`): `Id INTEGER PK AUTOINCREMENT`, `Name TEXT`, `Calories INTEGER`, `Price REAL`, `ProteinGrams INTEGER`, `IsBreakfast INTEGER (0/1)`. Derived columns from the sheet (calories/$, price+tax, protein/$) are intentionally dropped. Breakfast flags were guessed from item names (breakfast items, Bell Breakfast Box, hash brown, Cinnabon Delights).

# Endpoints

1. The API has full CRUD: `GET /api/items` (unpaged, bare array; optional `page`/`pageSize` query params switch it to paged mode returning an envelope `{ items, page, pageSize, totalCount, totalPages }`), `GET /api/items/{id}`, `POST /api/items` (create), `PUT /api/items/{id}` (update), `DELETE /api/items/{id}`.

2. Auth coverage: every endpoint requires a valid JWT except `POST /token` (anonymous). `GET /whoami` echoes the caller's claims. Tests bypass auth with the fake AuthenticationHandler; at least one test uses a factory WITHOUT the fake handler and asserts 401.

3. The application is a normal Web API with controllers. AOT is explicitly NOT a goal.

4. Error/validation contract — standard ProblemDetails everywhere: unknown id on GET/PUT/DELETE → 404 ProblemDetails (including DELETE of a missing id); invalid POST/PUT body → 400 validation ProblemDetails via DataAnnotations on request DTOs (`Name` required/non-empty; `Calories`, `Price`, `ProteinGrams` >= 0); bad paging params (`page < 1`, `pageSize < 1` or `> 100`) → 400. Success codes: POST → 201 with Location header, PUT → 204, DELETE → 204.

4. The test project needs the `Microsoft.AspNetCore.Mvc.Testing` NuGet package (it provides `WebApplicationFactory<T>`) and a `<ProjectReference>` to the API project — neither is wired up yet.

5. Two separate projects: (a) a plain NUnit test project runnable with `dotnet test`, and (b) a separate Spectre.Console/Spectre.Console.Cli CLI app ("runner") that shells out to `dotnet test` and parses the TRX results for pretty output. The CLI is a pretty test runner ONLY — no API-client or DB-seeding features.

6. CLI commands: `runner run` (whole suite, Spectre table/tree of pass/fail/duration + failure details); `runner run --filter <expr>` / `-f` (pass-through to `dotnet test --filter`); `runner interactive` or bare `runner` (interactive mode: enumerate every individual test via `dotnet test --list-tests`, let the user pick with a Spectre SelectionPrompt, then run and show results).

7. Global switches on all commands: `--plain` reverts to plain Console.WriteLine instead of Spectre.Console whenever possible; `--hotpink` renders ALL output exclusively in hot pink ANSI color (inside team joke). `--plain` and `--hotpink` are mutually exclusive; if both are passed, error out.
