# Maple Remit

A quote and transfer API for the Platform Support Engineer assessment. Built with .NET 8 Minimal APIs, with in-memory storage by default and PostgreSQL support.

## Run locally

Requirements: .NET 8 SDK (`global.json` pins version 8.0.417) and Docker. Docker must be running because the PostgreSQL tests use Testcontainers.

```bash
dotnet test
dotnet run --project src/MapleRemit.Api
```

The API runs in memory at `http://localhost:5001`.

To run with PostgreSQL:

```bash
cp .env.example .env
# Set a local database password in .env
docker compose up --build
```

The API runs at `http://localhost:8080`.

See `src/MapleRemit.Api/MapleRemit.Api.http` for example requests and error cases.

### Configuration

- `ExchangeRate:ThbPerCad`: exchange rate, defaults to `24.50` and must be greater than zero.
- `Storage:Provider`: `InMemory` (default) or `Postgres`. PostgreSQL requires `ConnectionStrings:Postgres`.

## API

| Endpoint | Success | Notes |
|---|---|---|
| `POST /quotes` | 201 | Accepts `amountCad`. Quotes expire after 15 minutes. |
| `POST /transfers` | 201, 200 on replay | Accepts `quoteId` and `customerId`. Requires a UUID `Idempotency-Key` header. |
| `GET /transfers?customerId=&page=` | 200 | Newest first, 20 per page, starting at page 1. |
| `GET /health` | 200, 503 | Returns 503 if the database is unreachable. |

Errors use a consistent JSON format: `{ "code": "...", "message": "..." }`. The 17 error codes are defined in `src/MapleRemit.Api/Errors/ErrorCodes.cs`.

## Design decisions

- **Shared business rules.** Pricing, rounding, expiry and transfer limits are kept separate from storage. Both stores use the same rules.
- **Concurrency.** The in-memory store locks the check-and-write operation. PostgreSQL uses a transaction, a per-customer advisory lock for idempotency and the daily limit, and `SELECT ... FOR UPDATE` to prevent a quote from being used twice.
- **Database constraints.** Unique constraints on `quote_id` and `(customer_id, idempotency_key)` prevent duplicate records. The daily limit still depends on the advisory lock; bypassing a lock could allow the limit to be exceeded.
- **Tests.** The same store contract tests run against both stores, including concurrent quote reuse, daily-limit and idempotency requests. I removed the locks to verify that the concurrency tests catch the failures.
- **Errors and logging.** A single middleware returns consistent error responses without exposing exception details. Client errors are logged at Information, unhandled exceptions at Error, and failed database health checks at Warning.
- **Time and money.** `TimeProvider` makes time testable with `FakeTimeProvider`. Times are aligned to PostgreSQL's microsecond precision. Money uses `decimal`, with half-up rounding to cents.
- **Schema.** Tables are created at startup with `CREATE TABLE IF NOT EXISTS`. This keeps setup simple but does not handle changes to existing schemas.
- **Container and CI.** The multi-stage Docker build uses the .NET 8 Alpine runtime and a non-root user. CI scans for secrets, builds and tests the app, checks the container with a health request, and runs Trivy. Images are pushed to GHCR on `main`, tagged with the commit SHA.

## Business rule assumptions

- The fee is 1.5% of `amountCad`. The daily limit counts `amountCad` only, without fees.
- Amounts with more than two decimal places are rejected.
- The limit covers transfers created in the previous 24 hours. A transfer exactly 24 hours old no longer counts. Rejected requests do not count.
- Quotes are not tied to a customer. The first successful transfer uses the quote.
- Idempotency keys are UUIDs, case-insensitive and scoped to a customer. Replaying the same key and quote returns the original transfer, even after quote expiry. Reusing the key with a different quote returns `422 IDEMPOTENCY_KEY_REUSED`.
- `customerId` comes from the request body. Authentication is expected to be handled by the BFF.

## Questions for the Compliance Officer

1. Should the limit use a rolling 24-hour window or a calendar day? If a calendar day, which time zone?
2. Which transfer statuses should count toward the limit, and should refunds restore the available limit?
3. Should the limit apply per customer, or should linked accounts share a limit?
4. Should the $3,000 limit be a hard cap, or can exceptions be approved?

The current implementation uses a rolling 24-hour window, counts all created transfers, applies the limit per `customerId`, and rejects transfers that exceed it.

## With more time

- Add database migrations and cleanup for expired quotes, idempotency keys and in-memory data.
- Split `/health` into liveness and readiness, so a database outage does not restart healthy containers.
- Add an index on `transfers (customer_id, created_at)`.
- Add metrics and alerts for unusual error rates.
- Derive `customerId` from the authenticated identity instead of trusting the request body.
