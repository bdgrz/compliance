# Contributing

## Change workflow

1. Open a focused branch and keep domain code within the ownership boundaries documented in `README.md`.
2. For behavioral changes, first add a focused test that demonstrates the failure, then make the smallest correction that turns it green.
3. Run the local gates before opening a pull request:

   ```console
   npm ci
   npm run client:check
   dotnet restore Compliance.slnx --locked-mode
   dotnet format Compliance.slnx --verify-no-changes --no-restore
   dotnet build Compliance.slnx --configuration Release --no-restore
   dotnet test Compliance.slnx --configuration Release --no-build --no-restore --filter "Category!=BrokerIntegration"
   ```

4. Run the broker integration test when changing hosting, Portia, Fitz, persistence, scheduling, or worker behavior.
5. Explain contract, security, AOT, and operational effects in the pull request. Do not commit credentials or weaken production authentication to simplify a test.

Dependency lock files are part of the change. Keep the tree formatted and warning-free; CI treats analyzer warnings as errors.

## Code organization

- Organize product code by capability under `Features/<FeatureName>`.
- Match .NET namespaces to the folder structure.
- Declare one top-level type per C# file and name the file for that type.
- Keep project-wide composition, generated JSON contexts, and assembly markers at the project root.
- Put process-level API and worker concerns under `Hosting` rather than a product feature.
