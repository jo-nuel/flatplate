# FlatPlate Progress

Milestone entries are added after each milestone passes its build and test checks.

## M0: Solution and application shell (done)

- What was done: Created the three-project .NET 9 solution, added the approved packages, added a five-tab WPF shell, and created teammate placeholders.
- Decisions: Allowed .NET 9 applications to roll forward to the installed .NET 10 runtime for local checks while remaining compatible with .NET 9 lab machines. Disabled online NuGet auditing so offline builds stay warning-free.
- Tests: `dotnet test` completed successfully; 0 tests exist at M0. `dotnet build` completed with 0 errors and 0 warnings. The application launch check passed.
- Known issues / next: The tabs are placeholders by design. M1 adds shared enums, models, the EF Core context, and interfaces.
- AI help used: Codex read the assignment specification, created the initial solution and WPF shell, added setup documentation, and ran the build, test, and launch checks. Jonathan requested milestone-sized commits and will handle all pushes.

## M1: Shared domain contracts (done)

- What was done: Added the shared enums, entity models, ingredient inheritance hierarchy, merger and budget result models, four Core interfaces, the `PlanChanged` event contract, and the EF Core SQLite context with table-per-hierarchy ingredient mapping.
- Decisions: Recipe ingredients and store prices use composite keys. A date and meal slot pair is unique. Conversion method signatures are stable, while their behaviour remains explicitly deferred to M3.
- Tests: `dotnet test` completed successfully; 0 tests exist at M1. `dotnet build` completed with 0 errors and 0 warnings. `dotnet format --verify-no-changes` passed.
- Known issues / next: Ingredient conversion methods intentionally throw until M3. M2 adds the generic repository, database price provider, and repository tests.
- AI help used: Codex translated the approved domain design into documented C# contracts, configured the EF Core model, cleaned the default WPF assembly formatting, and ran build, test, formatting, and diff checks. Jonathan should share the M1 contracts with the team.
