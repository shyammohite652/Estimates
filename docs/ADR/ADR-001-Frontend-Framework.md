# ADR-001: Angular (standalone) for the frontend

- **Status:** Accepted
- **Date:** 2026-11-10

## Context
The UI is a forms-and-tables line-of-business app: a user picker, a project list and an
estimate screen with a paged grid, inline edit forms and role-dependent actions. It needs
typed HTTP calls, cross-cutting HTTP handling (JWT, errors), route guards and validated
forms. The time budget is three hours, so batteries-included matters more than flexibility.

## Decision
Angular 22 with standalone components, signals for local state, Reactive Forms,
`HttpClient` with functional interceptors, functional route guards and Angular Material for
the table, paginator, dialogs and snackbars.

## Alternatives rejected
- **React (+ Vite, React Query, React Hook Form, MUI).** Equally capable, but every one of
  routing, forms, HTTP interception and DI is a library choice to make and wire. For a
  three-hour slice that is time spent on plumbing rather than domain.
- **Blazor.** Keeps one language end to end, but the brief explicitly offers React/Angular
  and Blazor WASM's payload and ecosystem for data grids are weaker.

## Consequences
- Interceptors give a single place for JWT attachment and ProblemDetails → message mapping,
  which is what the brief means by "cross-cutting concerns handled once" on the frontend.
- Material's `mat-paginator` maps directly onto the server-paged lines endpoint.
- Heavier framework and steeper learning curve than React for newcomers.

## What would change my mind
A team already standardised on React, a need for server-side rendering/streaming of very
large read-only views (React Server Components), or a design system that only ships React
components.
