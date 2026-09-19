# Contributing

## Change workflow

For each validated product story or existing delivery slice entering delivery,
track backend and frontend work in separate child issues under the product
parent. Give both children the parent's milestone and record applicable
dependencies explicitly. Backend children omit the UI-only M0-D24 blocker;
frontend children depend on M0-D24 and the corresponding backend child. Keep
the product parent open until both children and their integrated acceptance
criteria are complete. Do not schedule delivery children for unvalidated P2
hypotheses.
For an upstream feature or enabler, link a backend child to its backend child;
link a frontend child to an upstream frontend child when that browser workflow
is required. Do not let a still-open product parent make backend work wait for
UI completion.

Close a backend child only after linking its merged PR, focused and full
applicable test results, broker and split-host evidence, and exact-head CI
checks. A previously merged PR can satisfy part of a child's criteria, but
record the remaining gaps explicitly. Close a frontend child only after its
accessible browser workflow, API integration, required UI states, focused
browser tests, and applicable repository checks are evidenced by a merged PR.
The frontend consumes backend contracts; it does not redefine authorization or
domain policy.

1. Open a branch for one reviewable backend capability. Bundle dependent child issues when they share contracts, canonical records, or acceptance tests. Keep domain code within the ownership boundaries documented in `README.md`.
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

Dependency lock files are part of the change. Keep the tree formatted and warning-free; CI treats analyzer warnings as errors.

Tests use `Should<Outcome>Given<State>` names, with `When<Action>` when it clarifies the case. Block-bodied tests mark their Arrange, Act, and Assert phases. The Cntryl.Conventions analyzers enforce these rules as build errors.

HTTP route parameter names, query parameter names, and JSON property names use `snake_case`. Keep the OpenAPI wire-name test current when adding operations.

## Code organization

- Organize product code by capability under `Features/<FeatureName>`.
- Match .NET namespaces to the folder structure.
- Declare one top-level type per C# file and name the file for that type.
- Keep project-wide composition, generated JSON contexts, and assembly markers at the project root.
- Put process-level API and worker concerns under `Hosting` rather than a product feature.
