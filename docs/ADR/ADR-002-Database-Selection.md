# ADR-002: SQL Server (relational) for persistence

- **Status:** Accepted
- **Date:** 2026-11-10

## Context
The data is relational and the rules are relational: one catalogue item per estimate (R7),
exact decimal money (R4), effective-dated rates (R2), an append-only audit (R13), idempotent
approval under concurrency (R12), and estimates of 50 000 lines that must be summed and paged
quickly (R19).

## Decision
Microsoft SQL Server via EF Core (code-first migrations).

Rules pushed into the database as enforcement, not just code:
- Unique `(EstimateId, CatalogueItemId)` — R7 cannot be violated even by a race.
- Unique `(CatalogueItemId, EffectiveFrom)` — rate history is unambiguous.
- Filtered unique index on `EstimateHistory (EstimateId) WHERE Action = 'Approved'` — at most
  one approval record per estimate (R12).
- `rowversion` on `Estimate` — optimistic concurrency between line edits and transitions.
- `decimal(18,2)` / `decimal(18,3)` / `decimal(5,2)` — precision fixed in the schema.
- Covering index on lines for `SUM` and paging (R19).

## Alternatives rejected
- **PostgreSQL.** Technically as good (partial indexes, `numeric`, `xmin` for concurrency) and
  cheaper to host. Rejected only on fit: the team and my day-to-day stack are on Azure SQL /
  SQL Server, and the brief's starter compose ships both. Close call.
- **Document store (Cosmos DB / MongoDB).** An estimate-as-document breaks at 50 000 lines
  (document size limits, rewrite-the-whole-document updates), and cross-document uniqueness
  and transactions for the audit are harder.

## Consequences
- Requires SQL Server locally (Docker compose or LocalDB on Windows).
- Constraint violations surface as `DbUpdateException`s that the API translates to `409`.
- Integration tests against real SQL Server are needed to prove the constraints (deferred;
  see DESIGN §5).

## What would change my mind
Hosting on a Postgres-first platform or licence cost pressure (→ PostgreSQL, a provider swap
in Infrastructure only); a need to store free-form, schema-varying line attributes at scale
(→ JSON columns first, document store only if that fails).
