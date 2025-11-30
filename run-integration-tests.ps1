# PowerShell script to run integration tests for Munition AutoPatcher
# This script provides options to run different test suites and collect artifacts

param(
    [Parameter(HelpMessage="Test suite to run: All, WeaponExtractor, ViewModelE2E")]
    [ValidateSet("All", "WeaponExtractor", "ViewModelE2E")]
    [string]$Suite = "All",
    
    [Parameter(HelpMessage="Output directory for test artifacts")]
    [string]$ArtifactPath = "",
    
    [Parameter(HelpMessage="Enable verbose output")]
    [switch]$Verbose,
    
    [Parameter(HelpMessage="Skip build step")]
    [switch]$NoBuild
)

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
Write-Host "Test Suite: $Suite" -ForegroundColor Cyan

# Set up artifact path
if ([string]::IsNullOrEmpty($ArtifactPath)) {
    $ArtifactPath = Join-Path $PSScriptRoot "test-artifacts"
}
Write-Host "Artifact Path: $ArtifactPath" -ForegroundColor Cyan
$env:MUNITION_TEST_ARTIFACT_PATH = $ArtifactPath

# Create artifact directory
if (-not (Test-Path $ArtifactPath)) {
    New-Item -ItemType Directory -Path $ArtifactPath -Force | Out-Null
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

# Build the solution (unless skipped)
if (-not $NoBuild) {
    Write-Host "Building solution..." -ForegroundColor Yellow
    dotnet build --no-restore
    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ Build failed" -ForegroundColor Red
        exit 1
    }
    Write-Host "✓ Build completed successfully" -ForegroundColor Green
    Write-Host ""
}

# Determine test filter
$testFilter = switch ($Suite) {
    "WeaponExtractor" { "--filter 'FullyQualifiedName~WeaponDataExtractor'" }
    "ViewModelE2E" { "--filter 'FullyQualifiedName~ViewModelE2ETests'" }
    default { "" }
}

# Determine verbosity
$verbosity = if ($Verbose) { "detailed" } else { "normal" }

# Run integration tests
Write-Host "Running Integration Tests ($Suite)..." -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Cyan

$testResultsPath = Join-Path $ArtifactPath "test-results"
if (-not (Test-Path $testResultsPath)) {
    New-Item -ItemType Directory -Path $testResultsPath -Force | Out-Null
}

# Build test command
$testArgs = @(
    "test",
    "tests/IntegrationTests",
    "--no-build",
    "--logger", "console;verbosity=$verbosity",
    "--logger", "trx;LogFileName=integration-tests.trx",
    "--results-directory", $testResultsPath
)

if (-not [string]::IsNullOrEmpty($testFilter)) {
    $testArgs += $testFilter
}

# Execute tests
& dotnet $testArgs

$testExitCode = $LASTEXITCODE

if ($testExitCode -eq 0) {
    Write-Host ""
    Write-Host "✓ All integration tests passed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Integration test implementation is working correctly." -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "✗ Some integration tests failed" -ForegroundColor Red
    Write-Host "Please check the test output above for details." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== Test Summary ===" -ForegroundColor Cyan
Write-Host "• Suite: $Suite" -ForegroundColor White
Write-Host "• Artifacts: $ArtifactPath" -ForegroundColor White
Write-Host "• Test Results: $testResultsPath" -ForegroundColor White
Write-Host ""

# List artifacts if ViewModelE2E
if ($Suite -eq "ViewModelE2E" -or $Suite -eq "All") {
    $scenarioArtifacts = Join-Path $ArtifactPath "scenarios"
    if (Test-Path $scenarioArtifacts) {
        Write-Host "=== Scenario Artifacts ===" -ForegroundColor Cyan
        Get-ChildItem -Path $scenarioArtifacts -Recurse | ForEach-Object {
            Write-Host "  $($_.FullName.Replace($ArtifactPath, '.'))" -ForegroundColor White
        }
        Write-Host ""
    }
}

Write-Host "For more details, see:" -ForegroundColor Yellow
Write-Host "• tests/IntegrationTests/README.md" -ForegroundColor White
Write-Host "• tests/IntegrationTests/Infrastructure/README.md (E2E harness docs)" -ForegroundColor White
Write-Host ""

exit $testExitCode