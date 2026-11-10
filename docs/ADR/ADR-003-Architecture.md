# ADR-003: Clean Architecture with a rich domain and no mediator

- **Status:** Accepted
- **Date:** 2026-11-10

## Context
Most of the value in this slice is business rules (pricing, lifecycle, authorisation). They
must be testable without a database or web host, and the interview will probe whether
patterns are used because they solve a problem here.

## Decision
- Four projects: **Domain → Application → Infrastructure → API**. Dependencies point inward.
- **Rich domain**: entities expose intention-revealing methods (`Submit`, `Approve`,
  `AddLine`) and validate in constructors/value objects; no public setters.
- **Application services, not MediatR**: one `EstimateService` per use-case area. A mediator
  adds indirection with no benefit at this size; pipeline behaviours are replaced by one
  exception handler and one access guard.
- **Ports in Application, adapters in Infrastructure**: `IEstimateRepository`,
  `IEstimateQueries`, `IUnitOfWork`, `ITokenIssuer`, `ICurrentUser`. Queries project
  directly to DTOs (read side) while commands go through the domain (write side) — a light
  CQRS split motivated by R19, not by fashion.
- **Validation** lives in domain value objects (the rules *are* domain rules); request DTOs
  only check shape. No FluentValidation — it would duplicate the domain checks.
- **Enforcement**: an architecture test asserts the dependency rules by reflecting over
  referenced assemblies. Chosen over NetArchTest to avoid a dependency for ~20 lines of code.
- **Auth (dev)**: JWT signed with a development-only symmetric key from configuration;
  token stored in `sessionStorage` by the SPA.

## Alternatives rejected
- **Vertical slices / minimal APIs in one project.** Faster to write, but the brief asks for
  boundary rules that can be enforced; one project makes that test meaningless.
- **Anaemic entities + service-layer rules.** Rules scatter across services; invariants such as
  "approved estimates cannot change" become conventions rather than guarantees.
- **Generic repository over EF Core.** Leaks `IQueryable` and adds nothing; repositories here
  are use-case shaped.

## Consequences
- More projects and mapping code than a single-project API.
- Domain tests run in milliseconds without infrastructure.
- `sessionStorage` tokens are readable by XSS; acceptable only because authentication is
  explicitly out of scope.

## What would change my mind
A much smaller scope (a CRUD admin tool) → single project. A need for cross-cutting
per-request behaviours on dozens of use cases (transactions, caching, retries) → a mediator
pipeline starts paying for itself.
