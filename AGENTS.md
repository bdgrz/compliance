# Repository Guidelines

## Project Structure & Module Organization

`Compliance.slnx` contains three application projects: `src/Compliance.Common` holds shared contracts, `src/Compliance.Core` owns domain behavior and Portia/Fitz integration, and `src/Compliance.App` hosts the API, worker, and bundled SPA. Dependencies point inward: App → Core → Common. Put product code under `Features/<FeatureName>` and keep namespaces aligned with folders. The SPA lives in `src/Compliance.App/ClientApp`; its `src/pages` are thin routes and `src/features/<capability>` owns feature UI and tests. .NET tests live in `test/Compliance.Tests`; product and architecture decisions live in `docs/`. Brand assets are in `assets/` and `src/Compliance.App/ClientApp/public/brand/`.

## Build, Test, and Development Commands

Use the SDK selected by `global.json`, Node 24/npm 12, and Docker Compose. Copy `.env.example` to `.env` and configure package-registry credentials before restoring.

- `npm ci` and `dotnet restore Compliance.slnx --locked-mode`: install locked dependencies.
- `npm run client:check`: type-check, lint, test, and build the SPA.
- `dotnet build Compliance.slnx -c Release --no-restore`: compile with analyzers and warnings as errors.
- `./scripts/check-backend.sh focused 'FullyQualifiedName~ShouldReject...'`: run a focused .NET test with the AOT analyzer.
- `./scripts/check-backend.sh full`: run the local client, formatting, build, ordinary-test, and broker-test gates.
- `docker compose up --build`: start the standalone app and its dependencies at `http://127.0.0.1:8080`.

## Coding Style & Naming Conventions

Follow `.editorconfig`: UTF-8, LF, final newline, four-space C# indentation, and two-space JSON/YAML/XML and client indentation. Use file-scoped C# namespaces, one top-level type per matching file, `PascalCase` types and members, `I` interfaces, and `_camelCase` private fields. HTTP route/query names and JSON properties use `snake_case`. Check C# formatting with `dotnet format Compliance.slnx --verify-no-changes --no-restore`.

## Testing Guidelines

.NET tests use xUnit; SPA tests use Vitest. For behavior changes, first add a focused failing test. Name .NET tests `Should<ExpectedBehavior>Given<Condition>` (optionally prefix the method); mark Arrange, Act, and Assert in block-bodied tests. Run `Category=BrokerIntegration` tests for persistence, projection, reactor, or split-host changes; those tests manage an isolated Compose stack.

## Commits & Pull Requests

Recent commits use `feat(scope): summary (#issue)` or `docs(scope): summary (#issue)`. Keep each branch reviewable and link its issue. Complete the PR template’s summary, validation, and contract/operations sections, including authentication, wire, persistence, and deployment effects where relevant. Run applicable local gates and require CI on the final PR head, including native AMD64/ARM64 AOT jobs, before merge. See `CONTRIBUTING.md` for the full delivery workflow and `SECURITY.md` for vulnerability reporting.
