# FlatPlate

FlatPlate is a .NET 9 WPF application for planning seven days of meals within a grocery budget. It combines recipe ingredients into a shopping list, prices the basket by supermarket, and includes an account flow backed by an Azure Functions API.

## Projects

- `FlatPlate.App` — WPF desktop client, weekly planner, seed data, and local planner database.
- `FlatPlate.Core` — shared models, contracts, validation, repositories, and planner services.
- `FlatPlate.Server` — authentication domain, EF Core context, token security, and email delivery.
- `FlatPlate.Api` — HTTP-triggered Azure Functions authentication API.
- `FlatPlate.Tests` — NUnit unit and integration tests.

## Requirements

- Windows 11
- .NET 9 SDK
- Azure Functions Core Tools v4
- Azurite, used by the Functions host during local development

## Run locally

1. Copy `FlatPlate.Api/settings.example.json` into the same folder. Name the copy `local` followed by `.settings.json`, which is the private configuration filename expected by Azure Functions. Git ignores that local copy.
2. Start Azurite.
3. From the repository root, start the API:

   ```powershell
   func start --script-root FlatPlate.Api
   ```

4. In a second terminal, start the WPF app:

   ```powershell
   dotnet run --project FlatPlate.App
   ```

The app connects to `http://localhost:7071/api/` by default. To use a deployed API for the current terminal session, set `FLATPLATE_API_BASE_URL` to its HTTPS `/api/` address before starting the app.

The example configuration uses a local SQLite authentication database and deferred email delivery. Deferred delivery prevents local development from accidentally sending real messages.

## Build and test

```powershell
dotnet build FlatPlate.sln
dotnet test FlatPlate.sln
```

Do not commit private configuration, database files, connection strings, SMTP credentials, or signing keys.
