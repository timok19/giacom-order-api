# Solution notes

Solution to the Giacom Order API tech test described in [README.md](README.md). Giacom's original commits are kept in
the history; each later commit is one change made for this submission.

## Running

```bash
docker compose down --volumes   # mysql-init.sql changed, so the database must be re-seeded
docker compose up --build
```

The API starts once MySQL has finished seeding, which takes under a minute.

- Swagger UI: http://localhost:8000/swagger (OpenAPI document at `/openapi/v1.json`)
- Health check: http://localhost:8000/health
- Sample requests: [src/Giacom.Orders.WebApi/Giacom.Orders.WebApi.http](src/Giacom.Orders.WebApi/Giacom.Orders.WebApi.http)
- Tests: `dotnet test Giacom.Orders.sln`

Building outside Docker needs the .NET 10 SDK, not .NET 8 as the README states. The solution file is at the repository
root rather than in `/src`; production code is under `src/` and tests are under `tests/`.

## Endpoints

| Task | Endpoint | Success | Errors |
|---|---|---|---|
| 1. Orders by status | `GET /orders/status/{status}?page=1&pageSize=50` | 200, one page of orders | 400 unknown status or invalid paging |
| 2. Update status | `PATCH /orders/{orderId}/status`, body `{"status": "InProgress"}` | 204 | 400 unknown status, 404 unknown order, 409 same status or concurrent update |
| 3. Create order | `POST /orders`, body `{"resellerId", "customerId", "items": [{"productId", "quantity"}]}` | 201 with `Location` | 400 listing every validation error |
| 4. Profit by month | `GET /orders/profit/monthly` | 200, `[{"year", "month", "profit"}]` | |

`GET /orders` is paged the same way as task 1. Lists return `{"items", "page", "pageSize", "totalCount"}`, newest first,
50 per page by default and at most 100. Product ids for creating an order appear in the items of `GET /orders/{orderId}`.
Errors are RFC 7807 problem details.

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
Seeded orders have no completion time, so the seed script uses the creation date for those already completed.

A new `Version` column is an optimistic concurrency token. When two requests update the same order at once, one
succeeds and the other receives 409 instead of overwriting the first change.

Dates are stored in UTC, truncated to whole seconds to match MySQL `datetime`, and returned with an offset. Order
creation, status changes and conflicts are logged.

## Architecture

A single deployable Web API with one MySQL database, layered along Clean Architecture lines. Dependencies point
inwards:

```
Giacom.Orders.WebApi -> Giacom.Orders.Infrastructure -> Giacom.Orders.Application -> Giacom.Orders.Domain
```

| Project | Responsibility |
|---|---|
| `src/Giacom.Orders.Domain` | `Order` aggregate (status changes, version, completion date) and reference data entities. No dependencies. |
| `src/Giacom.Orders.Application` | Use cases (`IOrderService`), the `IOrderRepository` port, request and response records, validation and limits. |
| `src/Giacom.Orders.Infrastructure` | EF Core mapping onto the existing MySQL schema, and the repository. Ids stay `binary(16)` in .NET `Guid` byte order, so existing ids are unchanged. |
| `src/Giacom.Orders.WebApi` | Controllers, mapping of exceptions to problem details, health check, OpenAPI and Swagger UI. |
| `tests/Giacom.Orders.Domain.Tests` | Order rules, without a database. |
| `tests/Giacom.Orders.Application.Tests` | Use cases against the real repository on in-memory SQLite. |
| `tests/Giacom.Orders.WebApi.Tests` | The HTTP pipeline (routes, status codes, headers, problem details) through `WebApplicationFactory`. |

`mysql-init.sql` still owns the schema; there are no EF migrations.

## Changes beyond the brief

.NET 8 reaches end of support on 10 November 2026, and the original packages had known vulnerabilities (one critical
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
