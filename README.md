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
docker compose up --detach sqrzl fitz
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Compliance.App
```

`COMPLIANCE_HOST_MODE` accepts `standalone` (the default), `api`, or `worker`. Development uses an explicit development-only authentication bypass. The application rejects that bypass in every other environment.

## Authentication

Compliance delegates identity to an external OpenID Connect provider such as Auth0 or Microsoft Entra ID. It does not host users, passwords, or a client secret. Production fails at startup unless these settings are supplied:

| Setting | Purpose |
| --- | --- |
| `Compliance__Authentication__Authority` | HTTPS issuer/authority URL |
| `Compliance__Authentication__Audience` | Audience expected in API access tokens |
| `Compliance__Authentication__ClientId` | Public browser application client ID |
| `Compliance__Authentication__Scopes` | Space-delimited OIDC and API scopes; must include `openid` |
| `Compliance__Authentication__AuthorizationAudience` | Optional Auth0-style `audience` authorization parameter |

For Entra, put the delegated API scope (for example, `api://.../Compliance.Read`) in `Scopes`. For Auth0, set the API identifier in both the server `Audience` and, when needed, `AuthorizationAudience`.

The SPA uses Authorization Code with PKCE. It stores the access token in session storage, never persists a refresh token, validates callback state/nonce and the ID token through `@askrjs/auth`, and sends the access token as a bearer credential. Client route guards are navigation ergonomics; ASP.NET Core remains the authorization boundary.

## HTTP boundaries and operations

- Application APIs live under `/api/v1`.
- Unknown `/api/*` routes return an API error and never fall through to the SPA.
- OpenAPI 3.1 is exposed at `/openapi/v1.json` and `/openapi/v1.yml`.
- `/health/live` reports that the process can answer HTTP.
- `/health/ready` and the compatibility alias `/healthz` become healthy after hosted startup, including the initial Fitz connection and worker startup.
- API errors use RFC Problem Details and include a `trace_id`.

ASP.NET Core's optimized static-asset endpoints serve the Vite output with build-time metadata and compression. A small pre-routing rewrite supplies `index.html` for client-owned, extensionless paths while reserving `/api`, `/auth`, `/health`, and `/openapi` for the server.

## Tests and containers

Run the real-broker test after starting Fitz and Sqrzl:

```console
docker compose up --detach sqrzl fitz
dotnet test Compliance.slnx --configuration Release --filter "Category=BrokerIntegration"
docker compose down --volumes
```

CI runs formatting, TypeScript, lint, browser-auth unit tests, the .NET suite, and the real-broker test. It then builds and executes the Native AOT image on native AMD64 and ARM64 GitHub runners—without emulation—and exercises standalone, API, and worker modes.

The publish workflow runs only after CI succeeds (or by explicit manual dispatch), rebuilds the validated commit on native runners, pushes architecture digests, and assembles a multi-platform manifest. Images include BuildKit provenance and SBOM attestations. `sha-<commit>` and `latest` tags are emitted; a SemVer tag is created only when it does not already exist.

## Versioning and observability

GitVersion derives repository and container versions from `GitVersion.yml`. Package versioning can evolve independently; .NET assembly identity remains the stable `1.0.0.0` declared at the repository root.

Runtime observability stays BCL-first: ASP.NET Core structured logs, request activities, health checks, and Problem Details trace identifiers are available without binding the application layers to a telemetry vendor. OpenTelemetry export belongs in an optional Portia telemetry adapter when deployment requirements call for it.

See [CONTRIBUTING.md](CONTRIBUTING.md) for the change workflow and [SECURITY.md](SECURITY.md) for private vulnerability reporting.
