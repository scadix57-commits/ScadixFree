# Debugger integration checks

Run on Windows with .NET 10, an interactive desktop, and a local Samsung netcoredbg executable:

```powershell
./tests/DebuggerIntegration/run.ps1 -DebuggerPath C:/tools/netcoredbg/netcoredbg.exe
```

If local PowerShell execution policy blocks scripts, run it with a process-scoped policy:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/DebuggerIntegration/run.ps1 -DebuggerPath C:/tools/netcoredbg/netcoredbg.exe
```

The default debugger path is `artifacts/debugger/netcoredbg/netcoredbg.exe`.
The script builds a small target with portable symbols and runs the actual Designer
window, keyboard handlers, editor, and debugger service. It opens temporary test
windows and closes them after the checks. Tests must run serially on a desktop.

Coverage includes stepping into/out of a method, evaluating its argument, adding
and removing breakpoints during a session, clearing the paused state and disabling
Step while running, repeated start/stop, target process cleanup, and
Continue → Pause → Continue. Pause must produce a real adapter stop event.
An additional deterministic message-order test verifies that a late step response
cannot overwrite a newer stopped event.

Call Stack and Locals checks inspect rendered rows with real debugger data,
verify frame and variable changes after stepping, open both panels in the Designer
dock, and verify that the rows are cleared while the target runs.

Logs and rendered screenshots are written to `artifacts/DebuggerDeep/`.
The script exits with an error if any assertion fails. It does not download a
debugger or require an external test framework.
