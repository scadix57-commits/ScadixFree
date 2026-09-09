# Build / Run integration checks

Requires the .NET 10 SDK. Run from the repository root:

```powershell
dotnet run --project tests/BuildRunIntegration/BuildRunIntegration.csproj
dotnet run --project tests/BuildRunUiIntegration/BuildRunUiIntegration.csproj
```

The first harness starts real dotnet processes in a temporary directory. It checks explicit targeting with spaces and multiple projects, rejection of concurrent operations, preparation cancellation, process-tree cancellation, recovery after cancellation, and nonzero exit handling.

The second harness requires an interactive Windows desktop and briefly opens a toolbar window. It checks command and button states, saving modified documents before building, aborting on a save error, and missing startup-project feedback. Native Save As dialog interaction is not automated; the service harness covers a cancelled preparation result.

Each harness returns a nonzero exit code on failure. Temporary target files are removed after execution.
