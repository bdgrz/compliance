# Compliance

Compliance is a Native AOT ASP.NET Core application built on Portia, with an AskrJS single-page application. This repository intentionally contains foundation code only; compliance domain behavior will be added behind the established boundaries.

## Repository layout

| Path | Responsibility |
| --- | --- |
| `src/Compliance.Common` | Contracts and primitives shared across process boundaries |
| `src/Compliance.Core` | Application composition and Portia/Fitz infrastructure |
| `src/Compliance.App` | ASP.NET Core API, worker host, and bundled SPA |
| `test/Compliance.Tests` | Unit, HTTP, broker, and architecture-level tests |

Dependencies point inward: App references Core and Common; Core references Common; Common does not reference either host layer. All code uses the `Bdgrz.Compliance` root namespace and `Bdgrz.Compliance.*` assembly names.

The SPA follows a thin-page, vertical-feature layout documented in
[`src/Compliance.App/ClientApp/README.md`](src/Compliance.App/ClientApp/README.md).

Product discovery and delivery are governed by the
[product brief](docs/product/product-brief.md),
[SOC 2 product gap analysis](docs/product/gap-analysis.md),
[shared domain model](docs/product/domain-model.md),
[user-story backlog](docs/product/backlog.md), and
[triage](docs/product/triage.md). The domain model and the domain slice and
implementation subtasks in every story are part of that story's delivery
contract.

## Prerequisites

- .NET SDK 10.0.400 (pinned by `global.json`)
- Node.js 24 and npm 12
- Docker with Compose
- A GitHub token that can read the Cntryl package registry

Copy `.env.example` to `.env`, set `GITHUB_ACTOR` and `GITHUB_TOKEN`, then install and validate the repository:

```console
npm ci
dotnet restore Compliance.slnx --locked-mode
npm run client:check
dotnet build Compliance.slnx --configuration Release --no-restore
dotnet test Compliance.slnx --configuration Release --no-build --no-restore --filter "Category!=BrokerIntegration"
```

## Running locally

Compose starts the standalone application, Fitz, and the Sqrzl S3 emulator:

```console
docker compose up --build
```

The app is available at `http://127.0.0.1:8080`. Split API and worker processes from the same image when process-level scaling is useful:

```console
docker compose --profile split up --build api worker
```

To run the app directly while its dependencies remain in Compose:

```console
docker compose up --detach storage broker
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Compliance.App
```

`COMPLIANCE_HOST_MODE` accepts `standalone` (the default), `api`, or `worker`. Local Compose enables email-based developer authentication with `BDGRZ_DEVELOPER_AUTH=true`. The application rejects developer authentication in every other environment.

Any signed-in user with a verified email address may create an organization. Suspension, reactivation, and platform tenant listings still require an operator. Configure production operators with `PlatformOperators:UserIds:0`, `PlatformOperators:UserIds:1`, and so on, using Bdgrz platform user UUIDs. Local developer identities are treated as operators only while developer authentication is enabled; this supports local bootstrap and integration tests. In-product operator grants and revocations are still pending under R1-15 backend #152.

Production organization registration requires a legal name and the creator's verified email address in `first_administrator_email`. The tenant registration event identifies that creator as its first Org Admin with `client_personnel` affiliation; the worker materializes membership and team grants from the same event. Until those projections catch up, access fails closed. Historical operator-provisioned tenants still replay their invited first-administrator path, and local developer registration without an invitation retains its creator bootstrap. Firm staff invitations create an explicit `firm_staff` membership but team grants alone confer no business access, including historical grants. Operators can inspect members, suspend or reactivate an active organization, and change its slug. The previous slug resolves only for existing members and is never assigned to another organization.

## Authentication

Badgers delegates authentication to an external OpenID Connect provider such as Auth0 or Microsoft Entra ID. It does not host passwords or a client secret. Production fails at startup unless these settings are supplied:

| Setting | Purpose |
| --- | --- |
| `Compliance__Authentication__Authority` | HTTPS issuer/authority URL |
| `Compliance__Authentication__Audience` | Audience expected in API access tokens |
| `Compliance__Authentication__ClientId` | Public browser application client ID |
| `Compliance__Authentication__Scopes` | Space-delimited OIDC and API scopes; must include `openid` |
| `Compliance__Authentication__AuthorizationAudience` | Optional Auth0-style `audience` authorization parameter |
| `BDGRZ_SESSION_SIGNING_KEY` | At least 32 bytes used to sign the HttpOnly Badgers session JWT |

For Entra, put the delegated API scope (for example, `api://.../Compliance.Read`) in `Scopes`. For Auth0, set the API identifier in both the server `Audience` and, when needed, `AuthorizationAudience`.

The SPA uses Authorization Code with PKCE. It stores the access token in session storage, never persists a refresh token, validates callback state/nonce and the ID token through `@askrjs/auth`, and sends the access token to the OIDC identity-registration command. A successful registration establishes the signed Badgers session cookie. Client route guards are navigation ergonomics; ASP.NET Core remains the authorization boundary.

## HTTP boundaries and operations

- Application APIs live under `/api/v1`.
- Unknown `/api/*` routes return an API error and never fall through to the SPA.
- OpenAPI 3.1 is exposed at `/openapi/v1.json` and `/openapi/v1.yml`.
- `/health/live` reports that the process can answer HTTP.
- `/health/ready` and the compatibility alias `/healthz` become healthy after hosted startup, including the initial Fitz connection and worker startup.
- API errors use RFC Problem Details and include a `trace_id`.
- Authenticated users can reserve, list, inspect, and verify their own email addresses under
  `/api/v1/users/{user_id}/email-addresses`. Challenge issuance and completion use Portia commands.
  Email ownership and verification are HTTP-only; they are not MCP tools.

Email delivery currently uses `MockEmailChallengeDelivery`. It captures the latest challenge
in process for automated tests and sends no external message. AUTH-02a backend #360 owns
production challenge delivery and durable retry before verified self-service creation can be
used by real users.
Invitations likewise use `MockTenantInvitationDelivery`, which captures tokens only in the process
that sent them. Invitation acceptance and email verification are human HTTP flows and have no MCP tools;
operator invitation management, organization queries, lifecycle, and slug operations use both HTTP and MCP.

ASP.NET Core's optimized static-asset endpoints serve the Vite output with build-time metadata and compression. A small pre-routing rewrite supplies `index.html` for client-owned, extensionless paths while reserving `/api`, `/auth`, `/health`, and `/openapi` for the server.

## Tests and containers

The real-broker tests start and tear down their own isolated Fitz and Sqrzl Compose stack:

```console
dotnet test Compliance.slnx --configuration Release --filter "Category=BrokerIntegration"
```

CI runs formatting, TypeScript, lint, browser-auth unit tests, the .NET suite, and the real-broker test. In parallel, it builds and executes the Native AOT image on native AMD64 and ARM64 GitHub runners—without emulation—and exercises standalone, API, and worker modes. All jobs must pass on the final pull-request head before merge; use the focused local loop in [CONTRIBUTING.md](CONTRIBUTING.md) during development.

The publish workflow runs only after CI succeeds (or by explicit manual dispatch), rebuilds the validated commit on native runners, pushes architecture digests, and assembles a multi-platform manifest. Images include BuildKit provenance and SBOM attestations. `sha-<commit>` and `latest` tags are emitted; a SemVer tag is created only when it does not already exist.

## Versioning and observability

GitVersion derives repository and container versions from `GitVersion.yml`. Package versioning can evolve independently; .NET assembly identity remains the stable `1.0.0.0` declared at the repository root.

Runtime observability stays BCL-first: ASP.NET Core structured logs, request activities, health checks, and Problem Details trace identifiers are available without binding the application layers to a telemetry vendor. OpenTelemetry export belongs in an optional Portia telemetry adapter when deployment requirements call for it.

See [CONTRIBUTING.md](CONTRIBUTING.md) for the change workflow and [SECURITY.md](SECURITY.md) for private vulnerability reporting.
