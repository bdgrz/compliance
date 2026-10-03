# Repository Guidelines

## Project Structure & Module Organization

`Compliance.slnx` contains the .NET projects: `src/Compliance.Common` holds shared contracts, `src/Compliance.Core` owns domain behavior and Portia/Fitz integration, and `src/Compliance.App` hosts the API and worker. Dependencies point inward: App → Core → Common. The separate SPA lives in `ui/`; its `src/pages` are thin routes and `src/features/<capability>` owns feature UI and tests. .NET tests live in `test/Compliance.Tests`; product and architecture decisions live in `docs/`. Brand assets are in `assets/` and `ui/public/brand/`.

## Build, Test, and Development Commands

Use the SDK selected by `global.json`, Node 24/npm 12 for the separate UI, and Docker Compose. Copy `.env.example` to `.env` and configure package-registry credentials before restoring .NET packages.

- `dotnet restore Compliance.slnx --locked-mode`: restore .NET dependencies.
- `dotnet build Compliance.slnx -c Release --no-restore`: compile with analyzers and warnings as errors.
- `dotnet test Compliance.slnx -c Release --no-restore`: run .NET tests.
- `npm ci && npm run client:check`: independently type-check, lint, test, and build the SPA; this is outside the .NET build and test flow.
- `docker compose up --build`: start the standalone app and its dependencies at `http://127.0.0.1:8080`.

## Coding Style & Naming Conventions

Follow `.editorconfig`: UTF-8, LF, final newline, four-space C# indentation, and two-space JSON/YAML/XML and client indentation. Use file-scoped C# namespaces, one top-level type per matching file, `PascalCase` types and members, `I` interfaces, and `_camelCase` private fields. HTTP route/query names and JSON properties use `snake_case`. Check C# formatting with `dotnet format Compliance.slnx --verify-no-changes --no-restore`.

## Testing Guidelines

.NET tests use xUnit; SPA tests use Vitest. For behavior changes, first add a focused failing test. Name .NET tests `Should<ExpectedBehavior>Given<Condition>` (optionally prefix the method); mark Arrange, Act, and Assert in block-bodied tests. Run `Category=BrokerIntegration` tests for persistence, projection, reactor, or split-host changes; those tests manage an isolated Compose stack.

## JEV and Backlog Workflow

JEV is TypeSafe's Jev model, called through the TypeSafe System One API. It supplies semantic judgments for backlog triage; GitHub Issues and the `Compliance — SOC 2 product journey` project remain the source of truth for issue text, status, dependencies, and delivery progress. Use `scripts/backlog-jev.mjs` as the repository wrapper. It reads GitHub and calls JEV but does not change GitHub state; use the GitHub CLI to record issue comments, dependencies, project fields, and completion.

The API key is stored at `~/.config/typesafe/jev.key`. Load it only into the command environment, and never print, commit, or copy the key into a file:

```sh
TYPESAFE_API_KEY="$(tr -d '[:space:]' < "$HOME/.config/typesafe/jev.key")" node scripts/backlog-jev.mjs triage --milestone R1
TYPESAFE_API_KEY="$(tr -d '[:space:]' < "$HOME/.config/typesafe/jev.key")" node scripts/backlog-jev.mjs criteria 209 211
TYPESAFE_API_KEY="$(tr -d '[:space:]' < "$HOME/.config/typesafe/jev.key")" node scripts/backlog-jev.mjs preflight 437
TYPESAFE_API_KEY="$(tr -d '[:space:]' < "$HOME/.config/typesafe/jev.key")" node scripts/backlog-jev.mjs api-gap "<UI need> :: <current client workaround>"
```

Run `triage` to select and group work, `criteria` before closing an issue, and `preflight` on the proposed PR head. Treat model probabilities as review signals: verify them against issue text, recorded dependencies, decisions, code, tests, and CI. Record newly discovered blockers in GitHub before proceeding. See `docs/product/delivery-cycle.md` for queue rules and the complete delivery loop.

## Commits & Pull Requests

Recent commits use `feat(scope): summary (#issue)` or `docs(scope): summary (#issue)`. Keep each branch reviewable and link its issue. Complete the PR template’s summary, validation, and contract/operations sections, including authentication, wire, persistence, and deployment effects where relevant. Run applicable local gates and require CI on the final PR head before merge. CI is one five-minute job (format, build, unit tests); the broker suite is a local gate (`./scripts/check-backend.sh full`). See `CONTRIBUTING.md` for the full delivery workflow and `SECURITY.md` for vulnerability reporting.
