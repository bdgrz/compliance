# Compliance

Compliance is a Native AOT ASP.NET Core backend built on Portia, with a separate AskrJS single-page application. This repository intentionally contains foundation code only; compliance domain behavior will be added behind the established boundaries.

## Repository layout

| Path | Responsibility |
| --- | --- |
| `src/Compliance.Common` | Contracts and primitives shared across process boundaries |
| `src/Compliance.Core` | Application composition and Portia/Fitz infrastructure |
| `src/Compliance.App` | ASP.NET Core API and worker host |
| `ui` | Independent AskrJS single-page application |
| `test/Compliance.Tests` | Backend tests; the fast gate runs unit tests only |

Dependencies point inward: App references Core and Common; Core references Common; Common does not reference either host layer. All code uses the `Bdgrz.Compliance` root namespace and `Bdgrz.Compliance.*` assembly names.

The SPA follows a thin-page, vertical-feature layout documented in
[`ui/README.md`](ui/README.md). It is outside the .NET solution, project graph,
and build/test flow.

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

Copy `.env.example` to `.env` and set `GITHUB_ACTOR` and `GITHUB_TOKEN` for .NET package restore. Run the backend checks independently:

```console
dotnet restore Compliance.slnx --locked-mode
dotnet build Compliance.slnx --configuration Release --no-restore
dotnet test Compliance.slnx --configuration Release --no-build --no-restore --filter "Category!=BrokerIntegration&Category!=WebIntegration"
```

Run the UI unit tests separately from the .NET build and test flow:

```console
npm ci
npm test
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

Any signed-in user with a verified email address may create an organization. Suspension, reactivation, and platform tenant listings require a current operator. Configure `PlatformOperators:UserIds:0`, `PlatformOperators:UserIds:1`, and so on with the initial Bdgrz platform user UUIDs. The configuration seeds an empty roster once; subsequent HTTP or MCP grants and revocations are authoritative, and restart never restores a revoked operator. An operator cannot revoke the last operator or grant an unknown platform user UUID. Local developer identities are treated as operators only while developer authentication is enabled.

Production organization registration requires a legal name. The server checks the creator's verified email directory; callers omit `first_administrator_email`, which is retained only for local developer invitation compatibility. The tenant registration event identifies that creator as its first Org Admin with `client_personnel` affiliation. The worker materializes membership and administrator grants; the tenant remains provisioning until those grants and its slug are ready. Historical registrations replay with their original activation rule. Firm staff invitations create an explicit `firm_staff` membership but team grants alone confer no business access, including historical grants. Operators can inspect members, suspend or reactivate an active organization, and change its slug. The previous slug resolves only for existing members and is never assigned to another organization.

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
- Unknown `/api/*` routes return an API error. The backend does not serve SPA assets or client-side routes.
- OpenAPI 3.1 is exposed at `/openapi/v1.json` and `/openapi/v1.yml`. API-user operations declare the existing `bdgrz_session` cookie (`BdgrzSessionCookie`) or bearer JWT (`BdgrzBearer`) as alternative authentication mechanisms. Request authorizers still verify actor identity, tenant membership, and operation permissions. Anonymous routes have no global API-user security requirement.
- `/health/live` reports that the process can answer HTTP.
- `/health/ready` and the compatibility alias `/healthz` become healthy after hosted startup, including the initial Fitz connection and worker startup.
- API errors use RFC Problem Details and include a `trace_id`.
- Authenticated users can reserve, list, inspect, and verify their own email addresses under
  `/api/v1/users/{user_id}/email-addresses`. Challenge issuance and completion use Portia commands.
  `GET /api/v1/users/{user_id}/email-addresses/{email_address}/challenges/status` reports
  `not_issued`, `pending`, `failed`, `delivered`, `expired`, or `verified` to the owner.
  Email ownership and verification are HTTP-only; they are not MCP tools.

Development and tests default to `MockEmailChallengeDelivery`. Other environments require
`Compliance:EmailDelivery:Mode=smtp`, a STARTTLS SMTP host/port/sender, and an active 32-byte
base64 token key under `Compliance:EmailDelivery:TokenKeys:<key-id>`. Set the same key ring and
active key ID on API and worker hosts. The worker derives the token from the persisted challenge
ID and key, sends it, and records delivery status. Neither events nor responses contain the
plaintext token. Keep a previous key configured until all challenges and invitations issued
with it expire (up to 7 days). A missing key or SMTP failure records a terminal failed attempt;
the owner must reissue the challenge after the cause is fixed.
The SMTP `Message-ID` is stable per challenge. Delivery is at least once: a crash after SMTP
acceptance but before the sent event can produce another email with the same valid token.
Reissuing replaces the challenge and invalidates its previous token. Keep SMTP credentials and
token keys in deployment secrets. The `.env.example` remains suitable for local mock delivery.
Invitations use the same configured SMTP relay and key ring outside development. The API commits
only a token hash, attempt ID, and key ID; the worker derives the seven-day invitation token,
sends it with a stable `Message-ID`, and records the outcome. A worker restart retries an
unacknowledged send with the same token and message ID. SMTP delivery is at least once; the relay
may still deliver a duplicate after a crash. A recorded key or SMTP failure requires an
administrator to reissue; the tenant worker continues with later invitations. Reissue
invalidates the previous token. Historical hash-only invitations cannot be delivered by the
worker and must be reissued. Development and
tests default to `MockTenantInvitationDelivery` in the worker that sent the invitation. Invitation
acceptance and email verification are human HTTP flows and have no MCP tools; operator invitation
management, organization queries, lifecycle, and slug operations use both HTTP and MCP.

### Weekly work digest delivery

Weekly digests use the same `Compliance:EmailDelivery:Mode=smtp` relay, sender, and optional
paired SMTP username/password as email verification and tenant invitations. Development defaults
to an in-process mock transport; explicitly setting `Mode=smtp` also selects SMTP there. In
non-Development deployments, configure SMTP and the digest link settings on each API and worker
host; startup validates these settings for both roles.

The digest link settings are:

| Setting | Requirement |
| --- | --- |
| `Compliance__WorkDigest__ApplicationOrigin` | Canonical HTTPS origin supplied by deployment, with no path, query, fragment, or user information |
| `Compliance__WorkDigest__ClientWorkItemRouteTemplate` | Client-owned root-relative route template containing `{tenant_id}`, `{tenant_slug}`, `{program_id}`, and `{work_item_id}` |

The client deployment must supply its actual route template and origin. The backend does not ship
or assume a user-facing route. Do not use a queue `ActionPath` or an API URL as the email link; the
client route should open the work item and obtain current authorized data from
`GET /api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}`. Opening a link grants
no access by itself.

`COMPLIANCE_HOST_MODE=standalone` (the default) runs the API and digest worker together. Use
`COMPLIANCE_HOST_MODE=api` for an API-only process and `COMPLIANCE_HOST_MODE=worker` for a
dedicated worker. The digest sweep runs only in standalone and worker modes. It polls every
60 seconds by default; retry defaults are three attempts, a five-minute retry delay, a six-day
retry window, and a three-minute attempt lease. These values can be tuned with the corresponding
`Compliance__WorkDigest__*` settings shown in `.env.example`. In Development, an absent origin or
route leaves the digest worker inactive; other environments require both.

Platform operators supporting IT operations can inspect a single tenant member and week through
the read-only endpoint:
`GET /api/v1/platform/tenants/{tenant_id}/members/{member_id}/work-digest-dispatches/{week_of}`
(`week_of` is `YYYY-MM-DD`). The same read-only request is exposed to authorized platform
operators through MCP as `GetWorkDigestDispatchStatus`. Tenant administrator or member privileges
alone do not grant this cross-tenant operational view. The response contains delivery state and
attempt metadata, but no recipient address, message body, SMTP/provider response, or source work
content.
An `unknown` status means the relay outcome may be ambiguous and is not automatically resent;
an interrupted attempt can also be unresolved. Definite transient SMTP rejection is retried
within the configured attempt and time limits. SMTP acceptance and ledger persistence cannot
guarantee exactly-once physical delivery.

## Tests and containers

Feature development uses TDD with unit tests in the owning backend or client
repository. The backend unit-test command is:

```console
dotnet test Compliance.slnx --configuration Release --no-build --no-restore --filter "Category!=BrokerIntegration&Category!=WebIntegration"
```

Client repositories run their own unit-test suites. This repository's pull
request CI runs formatting, build, and backend unit tests. After successful CI
on `main`, `containers.yml` builds and publishes Native AOT images on native
AMD64 and ARM64 runners; it does not run as a feature pull-request check.
End-to-end validation is planned for a later phase after features work
independently. See [CONTRIBUTING.md](CONTRIBUTING.md) for the current delivery
loop.

The publish workflow runs only after CI succeeds (or by explicit manual dispatch), rebuilds the validated commit on native runners, pushes architecture digests, and assembles a multi-platform manifest. Images include BuildKit provenance and SBOM attestations. `sha-<commit>` and `latest` tags are emitted; a SemVer tag is created only when it does not already exist.

## Versioning and observability

GitVersion derives repository and container versions from `GitVersion.yml`. Package versioning can evolve independently; .NET assembly identity remains the stable `1.0.0.0` declared at the repository root.

Runtime observability stays BCL-first: ASP.NET Core structured logs, request activities, health checks, and Problem Details trace identifiers are available without binding the application layers to a telemetry vendor. OpenTelemetry export belongs in an optional Portia telemetry adapter when deployment requirements call for it.

See [CONTRIBUTING.md](CONTRIBUTING.md) for the change workflow and [SECURITY.md](SECURITY.md) for private vulnerability reporting.
