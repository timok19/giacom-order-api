# Solution notes

My solution to the Giacom Order API tech test ([README.md](README.md)). The git history keeps Giacom's original
commits; everything after them is mine, one commit per change.

## Running it

```bash
docker compose down --volumes   # the schema changed, so the database must be re-seeded
docker compose up --build
```

- Swagger UI: http://localhost:8000/swagger (OpenAPI document: http://localhost:8000/openapi/v1.json)
- Ready-made requests: [src/Giacom.Orders.WebApi/Giacom.Orders.WebApi.http](src/Giacom.Orders.WebApi/Giacom.Orders.WebApi.http)
- Tests: `dotnet test Giacom.Orders.sln`

Running locally needs the **.NET 10 SDK** (see "Upgrade to .NET 10" below): start the database with
`docker compose up db`, then run `Giacom.Orders.WebApi`.

The solution file `Giacom.Orders.sln` is at the repository root (the README's setup step still says `/src`).
Production code is under `src/` and tests under `tests/`; the solution shows them as `src` and `tests` folders.

## Endpoints

| Task | Endpoint | Success | Errors |
|---|---|---|---|
| 1. Orders by status | `GET /orders/status/{status}` | 200 | 400 unknown status |
| 2. Update status | `PATCH /orders/{orderId}/status` with `{"status": "InProgress"}` | 204 | 400 invalid status or transition, 404 unknown order, 409 concurrent change |
| 3. Create order | `POST /orders` with `{"resellerId", "customerId", "items": [{"productId", "quantity"}]}` | 201 + `Location` | 400 with every validation error |
| 4. Profit by month | `GET /orders/profit/monthly` | 200 `[{"year", "month", "profit"}]` | |

Errors are RFC 7807 problem details.

## Decisions

- **Status names** are matched ignoring case, spaces and separators: the README uses `InProgress`, while the
  database stores `In Progress`.
- **Status workflow** is forward-only: `Created -> In Progress -> Completed | Failed`, and Completed and Failed are
  final. Setting the current status again is rejected.
- **Create order**: the order starts in `Created` and each item's service is taken from its product. The request is
  validated as a whole: reseller and customer ids are set, there is at least one item, every product exists and every
  quantity is positive.
- **Concurrency**: the order table has a `Version` column used as an optimistic concurrency token. If two requests
  change the same order at once, one wins and the other gets 409 instead of overwriting it.
- **Profit date**: the order table has a `CompletedDate` column, set when an order becomes Completed. Profit is
  grouped by the UTC month of completion, not creation. Seeded completed orders use their creation date as the
  completion date, since that is the best data available.
- **Dates** are stored in UTC and returned with an offset (`DateTimeOffset`).

## Architecture

Clean Architecture; dependencies point inwards:

```
Giacom.Orders.WebApi ──> Giacom.Orders.Infrastructure ──> Giacom.Orders.Application ──> Giacom.Orders.Domain
        └─────────────────────────────────────────────────────────┘
```

| Project | Contains |
|---|---|
| `src/Giacom.Orders.Domain` | `Order` aggregate (`Create`, `ChangeStatus`, version, completion date), `OrderStatusWorkflow`, reference data entities. No dependencies. |
| `src/Giacom.Orders.Application` | `IOrderService` use cases, `IOrderRepository` port, record DTOs, `ValidationException`, `ConcurrencyConflictException`. |
| `src/Giacom.Orders.Infrastructure` | EF Core `OrdersDbContext` mapping onto the existing MySQL schema (`binary(16)` ids in .NET `Guid` byte order, so ids are unchanged) and the repository. |
| `src/Giacom.Orders.WebApi` | Controllers, `ApiExceptionHandler` (exceptions to problem details), OpenAPI and Swagger UI. |
| `tests/Giacom.Orders.Domain.Tests` | Workflow and invariants, no database. |
| `tests/Giacom.Orders.Application.Tests` | Use cases running against the real repository on in-memory SQLite. |

The schema stays owned by `mysql-init.sql` (no EF migrations), as in the original project.

## Upgrade to .NET 10

The project targeted .NET 8, which reaches end of support on 2026-11-10, and its packages had known
vulnerabilities (1 critical and several high, all transitive). It now targets .NET 10 LTS with current packages, and
`dotnet list package --vulnerable --include-transitive` reports none. The upgraded MySQL EF provider also translates
queries the old one could not, so no workarounds are needed.

## Docker fixes

- `mysql:5.7` has no arm64 image, so the database service is pinned to `linux/amd64` (emulated on Apple Silicon,
  native elsewhere).
- The API connects with `SslMode=Disabled`. In the container, TLS between .NET and this MySQL 5.7 image cannot be
  negotiated. The original project failed the same way. The likely cause: a native OpenSSL client only gets a DHE
  cipher suite from this server, while .NET on Linux offers only ECDHE suites. Traffic stays on the internal
  Docker network.

## Not done / next steps

- Enable TLS to the database by moving to MySQL 8.x, which also removes the amd64 pin.
- Swagger is exposed in every environment to make testing easy; a real deployment would restrict it.
- No authentication, and `GET /orders` has no pagination.
- Resellers and customers have no tables, so only non-empty ids are validated.
