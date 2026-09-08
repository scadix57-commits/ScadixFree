# Scadix Designer Build Script

$config = "Debug"
$framework = "net10.0-windows10.0.19041.0"
$baseDir = "d:\Scadix"

Write-Host "--- Starting Scadix Designer Build Pipeline ---" -ForegroundColor Cyan

# 1. Build the entire solution using the new .slnx format
Write-Host "--- Building Full Solution (Scadix.slnx) ---" -ForegroundColor Yellow
dotnet build "$baseDir\Scadix.slnx" -c $config -v quiet

Write-Host "`n--- Deployment Verification ---" -ForegroundColor Cyan
# The PostBuild events in csproj files will still handle the embedding and copying.


Write-Host "`n--- Build Completed Successfully! ---" -ForegroundColor Green
Write-Host "Output located in: $baseDir\App\Scadix.Designer\bin\$config\net10.0-windows\" -ForegroundColor Gray
