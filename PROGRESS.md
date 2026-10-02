# FlatPlate Progress

Milestone entries are added after each milestone passes its build and test checks.

## M0: Solution and application shell (done)

- What was done: Created the three-project .NET 9 solution, added the approved packages, added a five-tab WPF shell, and created teammate placeholders.
- Decisions: Allowed .NET 9 applications to roll forward to the installed .NET 10 runtime for local checks while remaining compatible with .NET 9 lab machines. Disabled online NuGet auditing so offline builds stay warning-free.
- Tests: `dotnet test` completed successfully; 0 tests exist at M0. `dotnet build` completed with 0 errors and 0 warnings. The application launch check passed.
- Known issues / next: The tabs are placeholders by design. M1 adds shared enums, models, the EF Core context, and interfaces.
- AI help used: Codex read the assignment specification, created the initial solution and WPF shell, added setup documentation, and ran the build, test, and launch checks. Jonathan requested milestone-sized commits and will handle all pushes.
