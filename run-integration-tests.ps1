# PowerShell script to run integration tests for WeaponDataExtractor
# This script provides an easy way to run the new integration tests and verify the implementation

Write-Host "=== Munition AutoPatcher Integration Tests ===" -ForegroundColor Green
Write-Host ""

# Check if .NET SDK is available
try {
    $dotnetVersion = dotnet --version
    Write-Host "✓ .NET SDK Version: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "✗ .NET SDK not found. Please install .NET 8.0 SDK or later." -ForegroundColor Red
    exit 1
}

Write-Host ""

# Restore packages
Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Package restore failed" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Packages restored successfully" -ForegroundColor Green
Write-Host ""

# Build the solution
Write-Host "Building solution..." -ForegroundColor Yellow
dotnet build --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Build failed" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Build completed successfully" -ForegroundColor Green
Write-Host ""

# Run integration tests
Write-Host "Running Integration Tests..." -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Cyan

# Run all integration tests with detailed output
dotnet test tests/IntegrationTests --no-build --logger "console;verbosity=normal"

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✓ All integration tests passed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Integration test implementation is working correctly." -ForegroundColor Green
    Write-Host "WeaponDataExtractor can successfully process virtual Mutagen environments." -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "✗ Some integration tests failed" -ForegroundColor Red
    Write-Host "Please check the test output above for details." -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "=== Test Summary ===" -ForegroundColor Cyan
Write-Host "• TestEnvironmentBuilder: Creates virtual Mutagen environments" -ForegroundColor White
Write-Host "• WeaponDataExtractor: Processes weapon-ammo relationships" -ForegroundColor White
Write-Host "• Integration Tests: Validate end-to-end functionality" -ForegroundColor White
Write-Host "• Virtual Environments: No game files required" -ForegroundColor White
Write-Host ""
Write-Host "For more details, see:" -ForegroundColor Yellow
Write-Host "• tests/IntegrationTests/README.md" -ForegroundColor White
Write-Host "• INTEGRATION_TESTS_IMPLEMENTATION_SUMMARY.md" -ForegroundColor White