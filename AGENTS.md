# Repository Guidelines

## Project Structure & Module Organization

This is the backend repository. `Compliance.slnx` contains the .NET projects: `src/Compliance.Common` holds backend contracts and primitives, `src/Compliance.Core` owns domain behavior and Portia/Fitz integration, and `src/Compliance.App` hosts the API and worker. Dependencies point inward: App → Core → Common. .NET tests live in `test/Compliance.Tests`, backend API contract tooling lives in `tools/`, and product and architecture decisions live in `docs/`.

The web, iOS, and Android clients are separate repositories. The tracked `ui/` directory is the web client awaiting extraction; do not add it to the .NET solution or backend build. New client implementation and client-specific tests belong in their owning repositories. The backend owns API behavior, authorization, and the OpenAPI contract. Client repositories consume a pinned contract or generated client; they must not depend on `Compliance.Common` or reproduce backend authorization rules.

## Build, Test, and Development Commands

Use the .NET SDK selected by `global.json` and Docker Compose. Copy `.env.example` to `.env` and configure package-registry credentials before restoring .NET packages. Node and npm checks belong to the separate client repositories.

- `dotnet restore Compliance.slnx --locked-mode`: restore .NET dependencies.
- `dotnet build Compliance.slnx -c Release --no-restore`: compile with analyzers and warnings as errors.
- `dotnet test Compliance.slnx -c Release --no-restore --filter "Category!=BrokerIntegration&Category!=WebIntegration"`: run the backend unit-test suite.
- `docker compose up --build`: start the standalone app and its dependencies at `http://127.0.0.1:8080`.

## Coding Style & Naming Conventions

Follow `.editorconfig`: UTF-8, LF, final newline, four-space C# indentation, and two-space JSON/YAML/XML indentation. Use file-scoped C# namespaces, one top-level type per matching file, `PascalCase` types and members, `I` interfaces, and `_camelCase` private fields. HTTP route/query names and JSON properties use `snake_case`. Check C# whitespace formatting with `dotnet format whitespace Compliance.slnx --verify-no-changes --no-restore`; the .NET build enforces configured style and analyzer diagnostics.

## Testing Guidelines

.NET tests use xUnit; client repositories use their own unit-test runners. For every backend or frontend behavior change, first add a focused failing unit test. Name .NET tests `Should<ExpectedBehavior>Given<Condition>` (optionally prefix the method); mark Arrange, Act, and Assert in block-bodied tests.

## Fast Development Loop

1. **Red:** in the backend or client repository, write a focused unit test that demonstrates the missing behavior and run that test alone.
2. **Green:** make the smallest change that passes the focused unit test. Run that repository's unit-test suite when the change is ready for review.
3. **PR:** open a focused PR after the unit-test suite passes. CI runs the repository's applicable format, build, and unit-test checks.
4. **Adversarial review:** review the exact PR head for contract, authorization, tenant isolation, replay, and failure-mode gaps. Treat review findings as actionable until resolved or explicitly documented.
5. **Refine and refactor:** address findings, simplify the implementation, and rerun the focused test plus the unit-test suite. Push the revised head and repeat review as needed.
6. **Squash merge:** merge after the final PR head has passed required CI and the adversarial review is clear.

For both backend and frontend features, current acceptance is proved with focused and full unit tests. Integration, broker, split-host, and end-to-end test execution is not an issue, PR, CI, or merge requirement. End-to-end validation is a later phase, after backend and client features work independently.

## JEV and Backlog Workflow

JEV is TypeSafe's Jev model, called through the TypeSafe System One API. It supplies semantic judgments for backlog triage; GitHub Issues and the `Compliance — SOC 2 product journey` project remain the source of truth for issue text, status, dependencies, and delivery progress. Use `scripts/backlog-jev.mjs` as the repository wrapper. It reads GitHub and calls JEV but does not change GitHub state; use the GitHub CLI to record issue comments, dependencies, project fields, and completion.

The API key is stored at `~/.config/typesafe/jev.key`. Load it only into the command environment, and never print, commit, or copy the key into a file:

```sh
TYPESAFE_API_KEY="$(tr -d '[:space:]' < "$HOME/.config/typesafe/jev.key")" node scripts/backlog-jev.mjs triage --milestone R1
TYPESAFE_API_KEY="$(tr -d '[:space:]' < "$HOME/.config/typesafe/jev.key")" node scripts/backlog-jev.mjs criteria 209 211
TYPESAFE_API_KEY="$(tr -d '[:space:]' < "$HOME/.config/typesafe/jev.key")" node scripts/backlog-jev.mjs preflight 437
TYPESAFE_API_KEY="$(tr -d '[:space:]' < "$HOME/.config/typesafe/jev.key")" node scripts/backlog-jev.mjs api-gap "<UI need> :: <current client workaround>"
```

Run `triage` to select and group work, `criteria` before closing an issue, and `preflight` on the proposed PR head. Treat model probabilities as review signals: verify them against issue text, recorded dependencies, decisions, code, unit tests, and CI. Record newly discovered blockers in GitHub before proceeding. Backend API gaps belong in this repository; client implementation belongs in the web or mobile repository that owns the experience. Link cross-repository issues and coordinate contract compatibility instead of combining code from multiple repositories into one PR. See `docs/product/delivery-cycle.md` for backlog queue rules.

## Commits & Pull Requests

Recent commits use `feat(scope): summary (#issue)` or `docs(scope): summary (#issue)`. Keep each branch reviewable and link its backend issue. A PR in this repository changes backend code or its API contract; link related client work rather than including client code. Complete the PR template’s summary, validation, and contract/operations sections, including authentication, wire compatibility, persistence, and deployment effects where relevant. Run the fast development loop above and require CI on the final PR head before squash merge. This repository's CI runs formatting, build, and backend unit tests. See `CONTRIBUTING.md` for branch and release rules and `SECURITY.md` for vulnerability reporting.
