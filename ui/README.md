# Compliance SPA

The Compliance UI is a client-rendered AskrJS single-page application built by
Vite+. ASP.NET Core serves the production bundle and remains the API and
authorization boundary.

## Structure

| Path                        | Responsibility                                    |
| --------------------------- | ------------------------------------------------- |
| `src/main.tsx`              | Browser bootstrap only                            |
| `src/pages/_routes.ts`      | Route registry, lazy boundaries, and route access |
| `src/pages/_layout.tsx`     | Product shell, navigation, theme, and overlays    |
| `src/pages/*.tsx`           | Thin route entry points                           |
| `src/features/<capability>` | API-to-UI vertical product slices and their tests |
| `src/components`            | Components shared by more than one capability     |
| `public/brand`              | Stable, unbundled brand assets                    |

## Working rules

Start each user story at its route page, then keep the API adapter, state, UI,
and tests together in its feature folder. Do not create a generic abstraction
until at least two real features need it. Runtime pages use real application
states; scenario fixtures and failure injection belong in tests, not the
production shell.

Client route guards improve navigation, but they never replace server-side
authorization. The SPA consumes versioned endpoints under `/api/v1` and does
not depend on whether the ASP.NET Core host runs standalone or in split API and
worker mode.

## Agent-ready boundary

Compliance should support WebMCP as a progressive enhancement once a product
story identifies a useful agent-assisted workflow. Do not register placeholder
tools or maintain separate agent-only business logic. A WebMCP tool belongs to
the feature that owns the visible workflow and invokes the same authorized API
operation, validation, and audit path.

Expose task-level user intent rather than DOM mechanics. Tool availability must
follow the active user, tenant, route, and permissions; consequential operations
must remain explicit and reviewable. Because WebMCP is still an evolving draft,
add its browser adapter or polyfill only with the first validated workflow.
