# Contributing

## Branch and release strategy

`develop` is the default integration branch. Create feature, fix, hotfix, and
chore branches from `develop`, then open pull requests back to `develop`. Do not
push directly to `develop` or `main`. Merge topic branches into `develop` with
a squash merge after the required checks pass.

Ordinary PRs deliver one functional slice or keep unfinished behavior behind a
default-off flag. Sprints do not need branches. A genuinely multi-story feature
may use a short-lived `feat/<feature>` branch only with fixed included stories,
acceptance and exit; its child PRs and final PR to `develop` each require review
and final-head checks. Keep WIP = 1 throughout.

`main` is the release branch. Promote only by opening a pull request from this
repository's `develop` branch to `main`. The promotion must pass the regular
validation, dependency review, and native AMD64 and ARM64 container checks. Use
a merge commit for the promotion so `main` retains the history of `develop`;
topic branches continue to use squash merges.

Pull requests into `develop` require `Validate` and `Dependency review`. The
native container matrix is skipped for develop-bound pull requests and pushes.
Pull requests into `main` also run `Promotion source`, which rejects a branch
other than this repository's `develop` branch, and run both native container
builds. Pushes to `main` and manual CI dispatch also run the native matrix.

## Change workflow

Use [Project 2](https://github.com/orgs/bdgrz/projects/2), the
[delivery cycle](docs/product/delivery-cycle.md) and
[sprint planning](docs/product/sprint-planning.md) to select work. Stories carry
user outcomes; epics retain broader acceptance and tasks carry bounded gaps.
Ready means known scope and accepted prerequisites; Sequenced waits for its
named predecessor. High priority or a sprint date does not bypass readiness.

Keep one clean checkout per repository, WIP = 1 across backend and clients, and
architect → coder → reviewer sequential. Preserve unrelated dirty work; do not
create worktrees or concurrent implementation lanes. Before In Progress, record
the observable gap, owner/repository, allowed change, exclusions, direct
dependencies, focused check and stopping point. Cap discovery at 20 minutes.
Another workflow/repository, unplanned contract change, unrelated refactor or
unknown scope requires a stop and re-scope.

Create a child task only for a confirmed gap with useful independent acceptance
and a focused PR boundary. Reuse working code and existing evidence. Backend
API behavior and client implementation use separate owning repositories and
PRs; clients consume contracts and do not redefine authorization or domain
policy. Backend dependencies identify the actual upstream capability rather
than a product parent waiting on client acceptance.

1. Select the next Ready task in Order, move it In Progress and create an issue/topic branch from `develop`. Keep code inside the owning repository and documented module boundaries.
2. For a behavior change, add and run a focused failing unit test, make the smallest correction that passes it, and run the owning repository's full unit suite plus applicable format/build checks. Use `Should<ExpectedBehavior>Given<Condition>` names. Evidence-only and documentation tasks do not manufacture RED; verify their actual evidence, links and consistency.
3. Open a focused PR after those checks pass. Review the immutable head adversarially for contract, authorization, isolation, replay and failure gaps. Address findings, rerun affected checks, and repeat review after changes until clear.
4. Require CI on the final PR head, squash merge, and read back the merged commit and each linked issue's acceptance. Record contract, security, persistence and operational effects; never commit credentials or weaken production authentication for tests.
5. Close only proved acceptance, leave explicit gaps on partial stories, update project fields and stop at the task boundary before selecting another task. Keep the handoff to outcome, commit, evidence, remaining acceptance and next action.

For backend behavior, the focused .NET test build runs Portia generation,
Cntryl.Conventions and analyzers with warnings as errors. Current acceptance
uses focused and full units; directly required configured integration checks
supplement them. Broad E2E/browser and native-client validation remain separately
scoped. Native publish checks trimming and architecture-specific runtime
behavior during release qualification.

```console
dotnet restore Compliance.slnx --locked-mode
dotnet format whitespace Compliance.slnx --verify-no-changes --no-restore
dotnet build Compliance.slnx --configuration Release --no-restore
dotnet test Compliance.slnx --configuration Release --no-restore --filter "Category!=BrokerIntegration&Category!=WebIntegration"
```

This repository's feature CI checks formatting, build and backend units. Native
AOT images are built by `containers.yml`; retain the release checks above.
Client unit/build checks run in their owning repositories. A default-off feature
flag preserves unfinished behavior without changing authority or proving the
complete user outcome.

Dependency lock files are part of the change. Keep the tree formatted and warning-free; CI treats analyzer warnings as errors.

Tests use `Should<Outcome>Given<State>` names, with `When<Action>` when it clarifies the case. Block-bodied tests mark their Arrange, Act, and Assert phases. The Cntryl.Conventions analyzers enforce these rules as build errors.

HTTP route parameter names, query parameter names, and JSON property names use `snake_case`. Keep the OpenAPI wire-name test current when adding operations.

## Code organization

- Organize product code by capability under `Features/<FeatureName>`.
- Match .NET namespaces to the folder structure.
- Declare one top-level type per C# file and name the file for that type.
- Keep project-wide composition, generated JSON contexts, and assembly markers at the project root.
- Put process-level API and worker concerns under `Hosting` rather than a product feature.
