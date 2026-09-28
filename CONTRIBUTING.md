# Contributing

## Branch and release strategy

`develop` is the default integration branch. Create feature, fix, hotfix, and
chore branches from `develop`, then open pull requests back to `develop`. Do not
push directly to `develop` or `main`. Merge topic branches into `develop` with
a squash merge after the required checks pass.

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

Use the [delivery cycle](docs/product/delivery-cycle.md) and the GitHub Project
delivery queue to select the next capability. Milestones describe product
outcomes; the queue describes whether an issue can be worked now. Keep one
capability bundle active at a time. An issue marked P0 is not automatically
ready when an upstream implementation or external decision remains open.

Create one outcome issue for a new validated product need. Add a child only when
it has independent acceptance, a useful review boundary, and a reason to close
separately. Existing backend and frontend children remain valid acceptance
records; a reviewable capability PR may satisfy several of them. Do not create
one PR per technical layer or per test. Do not create delivery children for an
unvalidated P2 hypothesis. A product parent closes only after its integrated
API-to-UI outcome passes. A backend dependency points to the upstream backend
capability, not an open product parent waiting for UI. A frontend issue depends
on the contracts it consumes and the accepted accessibility/browser baseline.

Close a backend child only after linking its merged PR, focused and full
applicable test results, broker and split-host evidence, and exact-head CI
checks. A previously merged PR can satisfy part of a child's criteria, but
record the remaining gaps explicitly. Close a frontend child only after its
accessible browser workflow, API integration, required UI states, focused
browser tests, and applicable repository checks are evidenced by a merged PR.
The frontend consumes backend contracts; it does not redefine authorization or
domain policy.

1. Take the lowest Run order item marked Ready, move its capability bundle to Active, and open a branch for that reviewable outcome. Bundle dependent child issues when they share contracts, canonical records, or acceptance tests. Keep domain code within the ownership boundaries documented in `README.md`.
2. For behavioral changes, first add a focused test that demonstrates the failure, then make the smallest correction that turns it green.
   Name tests `Should<ExpectedBehavior>Given<Condition>` (or `<Method>Should<ExpectedBehavior>Given<Condition>` when the method name adds clarity).
3. During development, run the focused test through the framework. Its build runs Portia generation, Cntryl.Conventions, and the .NET AOT analyzer with warnings-as-errors, without publishing a native image. Native publish still checks trimming and architecture-specific runtime behavior:

   ```console
   dotnet restore Compliance.slnx --locked-mode # once per dependency change or fresh checkout
   ./scripts/check-backend.sh focused 'FullyQualifiedName~ShouldRejectChangedCreateGivenExistingProgramAndPreserveReplay'
   ```

   Add a broker-focused filter when changing persistence, projections, reactors, or split-host behavior. Those tests start their own isolated Compose stack. Keep working on the same branch and commit related slices together. Do not push each red/green correction.

4. Run the full local gate once when the bundle is ready for review:

   ```console
   ./scripts/check-backend.sh full
   ```

5. Push the reviewed bundle and run hosted CI on its final head. Native AOT builds on both architectures remain required before merge, but start alongside Validate so they do not extend the critical path by their full duration. Repeat the local focused test for a correction; rerun the full gate and exact-head CI only after the final correction.
6. Explain contract, security, AOT, and operational effects in the pull request. Do not commit credentials or weaken production authentication to simplify a test.
7. After merge, read back each covered issue against its own acceptance evidence. Close satisfied children, leave explicit gaps on partial children, update the parent only when the integrated outcome is proven, and refresh project queue and Run order before selecting another capability.

Dependency lock files are part of the change. Keep the tree formatted and warning-free; CI treats analyzer warnings as errors.

Tests use `Should<Outcome>Given<State>` names, with `When<Action>` when it clarifies the case. Block-bodied tests mark their Arrange, Act, and Assert phases. The Cntryl.Conventions analyzers enforce these rules as build errors.

HTTP route parameter names, query parameter names, and JSON property names use `snake_case`. Keep the OpenAPI wire-name test current when adding operations.

## Code organization

- Organize product code by capability under `Features/<FeatureName>`.
- Match .NET namespaces to the folder structure.
- Declare one top-level type per C# file and name the file for that type.
- Keep project-wide composition, generated JSON contexts, and assembly markers at the project root.
- Put process-level API and worker concerns under `Hosting` rather than a product feature.
