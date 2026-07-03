# Context

Ubiquitous language for the Taco Bell menu API demo.

## Glossary

**Menu Item** — a single purchasable Taco Bell offering (item, combo, or party pack) with a name, calorie count, price, protein grams, and a breakfast flag. Combos and party packs are not modeled separately; everything is a flat Menu Item.

**Breakfast Item** — a Menu Item flagged as belonging to the breakfast menu. The flag was inferred from item names during data extraction, not sourced data.

**Seed Data** — the fixed set of 83 Menu Items extracted from the Taco Bell PDF. It is the canonical starting state of both the application database and every test database.

**Envelope** — the paged response shape `{ items, page, pageSize, totalCount, totalPages }`. Only paged requests return an Envelope; unpaged requests return a bare array.

**Token Endpoint** — the anonymous endpoint that implements a self-contained client-credentials flow: validates a hardcoded client id/secret and issues a locally signed JWT. A stand-in for Okta (planned on a separate branch).

**Auth Bypass** — the test-only fake authentication handler that unconditionally authenticates requests via an AuthenticationTicket, so endpoint tests need no token. Never present in the real application.

**Runner** — the separate Spectre.Console CLI app that executes the test suite by shelling out to `dotnet test` and rendering parsed TRX results. It is strictly a test runner: not an API client, not a DB tool.
