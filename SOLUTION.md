# Solution notes

Solution to the Giacom Order API tech test described in [README.md](README.md). Giacom's original commits are kept in
the history; each later change is a separate branch merged into `main`.

## Time spent

About 2 hours went on the brief itself: tasks 1 to 4 and the two Docker fixes needed to run them. About 2 more hours
went on optional work done afterwards: Clean Architecture, the .NET 10 upgrade, Swagger, concurrency and completion
date handling, paging, logging, the extra tests and the Alpine image. The core tasks arrived in the
`feature/order-endpoints` and `fix/docker-compose-apple-silicon-and-tls` merges; later merges changed their behaviour
(status rule, paging, completion date). Most XML doc comments were written with the optional work; the core-task
commit has only a few, on the validation exception, the error filter and helper methods.

## Running

```bash
docker compose down --volumes   # mysql-init.sql changed, so the database must be re-seeded
docker compose up --build
```

The API starts once MySQL has finished seeding, which takes under a minute.

- Swagger UI: http://localhost:8000/swagger (OpenAPI document at `/openapi/v1.json`)
- Health check: http://localhost:8000/health
- Sample requests: [src/Giacom.Orders.WebApi/Giacom.Orders.WebApi.http](src/Giacom.Orders.WebApi/Giacom.Orders.WebApi.http)
- Tests: `dotnet test Giacom.Orders.slnx`

Building outside Docker needs the .NET 10 SDK. The solution file `Giacom.Orders.slnx` is at the repository root;
production code is under `src/` and tests are under `tests/`.

## Endpoints

| Task | Endpoint | Success | Errors |
|---|---|---|---|
| 1. Orders by status | `GET /orders/status/{status}?page=1&pageSize=50` | 200, one page of orders | 400 unknown status or invalid paging |
| 2. Update status | `PATCH /orders/{orderId}/status`, body `{"status": "InProgress"}` | 204 | 400 unknown status, 404 unknown order, 409 same status or concurrent update |
| 3. Create order | `POST /orders`, body `{"resellerId", "customerId", "items": [{"productId", "quantity"}]}` | 201 with `Location` | 400 listing every validation error |
| 4. Profit by month | `GET /orders/profit/monthly` | 200, `[{"year", "month", "profit"}]` | |

`GET /orders` is paged the same way as task 1, so it now returns `{"items", "page", "pageSize", "totalCount"}` instead
of the original plain array. Lists are newest first, 50 per page by default and at most 100. Product ids for new
orders appear in the items of `GET /orders/{orderId}`. Errors are RFC 7807 problem details.

## Assumptions and decisions

The README uses `InProgress` while the database stores `In Progress`, so status names match regardless of case,
spaces and separators.

An order can move to any other status. Setting the status it already has returns 409, as does a change that races
with another request.

A new order starts as Created. Each item takes its service from its product, so the request only carries the product
and quantity. The request is validated as a whole: reseller and customer ids must be set, there must be 1 to 100
items, every product must exist and every quantity must be between 1 and 10,000.

Profit is price minus cost, summed over the items of completed orders and grouped by the UTC month in which each order
was completed. A new nullable `CompletedDate` column records that moment; it is cleared if the order leaves Completed.
Seeded orders have no completion time, so the seed script uses the creation date for those already completed. Months
without completed orders are not listed.

A new `Version` column is an optimistic concurrency token. When two requests update the same order at once, one
succeeds and the other receives 409 instead of overwriting the first change.

Dates are stored in UTC, truncated to whole seconds to match MySQL `datetime`, and returned with an offset. Order
creation, status changes and conflicts are logged.

## Architecture

One Web API and one MySQL database, layered along Clean Architecture lines; dependencies point inwards:

```
Giacom.Orders.WebApi -> Giacom.Orders.Application, Giacom.Orders.Infrastructure
Giacom.Orders.Infrastructure -> Giacom.Orders.Application -> Giacom.Orders.Domain
```

| Project | Responsibility |
|---|---|
| `src/Giacom.Orders.Domain` | `Order` aggregate (status, version, completion date) and reference data. No dependencies. |
| `src/Giacom.Orders.Application` | Use cases, the repository port, request and response records, validation and limits. |
| `src/Giacom.Orders.Infrastructure` | EF Core mapping onto the existing schema (ids stay `binary(16)` in .NET `Guid` byte order) and the repository. |
| `src/Giacom.Orders.WebApi` | Controllers, problem details, health check, OpenAPI and Swagger UI. |
| `tests/Giacom.Orders.Domain.Tests` | Order rules, no database. |
| `tests/Giacom.Orders.Application.Tests` | Use cases against the real repository on in-memory SQLite. |
| `tests/Giacom.Orders.WebApi.Tests` | Routes, status codes, headers and problem details through `WebApplicationFactory`. |

`mysql-init.sql` still owns the schema; there are no EF migrations.

## Upgrade and Docker

Support for .NET 8 ended on 10 November 2026, and the original packages had known vulnerabilities (one critical
and several high, all transitive). The solution now targets .NET 10 LTS with current packages, and
`dotnet list package --vulnerable --include-transitive` reports none. Nullable warnings fail the build.

The API image is based on `aspnet:10.0-alpine`, runs as a non-root user and is 209 MB (394 MB on Debian). It waits
for a MySQL health check, so the first request after `docker compose up` succeeds.

Two Docker fixes were needed for `docker compose up` to work. `mysql:5.7` has no arm64 image, so the database is
pinned to `linux/amd64` (emulated on Apple Silicon). The API connects with `SslMode=Disabled` because the MySQL driver
cannot negotiate TLS with this MySQL 5.7 image inside the container; the original code failed the same way, and the
traffic stays on the internal Docker network.

## Known limitations

- The database connection is not encrypted. Moving to MySQL 8 would allow TLS and remove the amd64 pin.
- Automated tests run on SQLite; the MySQL queries were checked by running the API against the Docker database.
- Totals and profit use current product prices, because the schema does not store the price at the time of the order.
- Swagger is exposed in every environment to make the API easy to try; a production deployment would restrict it.
- There is no authentication, and resellers and customers have no tables, so only non-empty ids are validated.
