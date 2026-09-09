param([string]$DebuggerPath = "artifacts/debugger/netcoredbg/netcoredbg.exe")

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Push-Location $repoRoot
try {
    $debuggerExecutable = (Resolve-Path -LiteralPath $DebuggerPath).Path
    New-Item -ItemType Directory -Force artifacts/DebuggerDeep | Out-Null
    dotnet build tests/DebuggerIntegration/Target/DeepTarget.csproj --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw 'Debug target build failed.' }
    $testOutput = Join-Path $repoRoot 'artifacts/debugger-tests-bin/'
    dotnet run --project tests/DebuggerIntegration/DebuggerIntegration.csproj "-p:OutputPath=$testOutput" -- $debuggerExecutable *> artifacts/DebuggerDeep/results.log
    $testExitCode = $LASTEXITCODE
    Get-Content artifacts/DebuggerDeep/results.log | Select-String '^(PASS:|FAIL:|TOTAL FAILURES:)'
    if ($testExitCode -ne 0) { throw 'Debugger integration tests failed; see artifacts/DebuggerDeep/results.log.' }
}
finally {
    Pop-Location
}
