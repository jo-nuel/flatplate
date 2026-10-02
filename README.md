# FlatPlate

FlatPlate is a WPF desktop application for planning a week of meals within a grocery budget. This repository currently contains the M0 solution shell. The Weekly Planner and domain behaviour will be added in later milestones.

## Requirements

- Windows 11
- Visual Studio 2022 with the .NET 9 SDK or a later compatible SDK

## Build and run

1. Open `FlatPlate.sln` in Visual Studio 2022.
2. Set `FlatPlate.App` as the startup project.
3. Build the solution and run the application.

The application currently opens a five-tab shell. Each tab is intentionally a placeholder at M0.

## Tests

Run the tests from Visual Studio Test Explorer or from the repository root:

```powershell
dotnet test FlatPlate.sln
```
