# E2E Test Baselines

This directory contains baseline artifacts for E2E integration tests.

## Purpose

Baseline artifacts are used to:
1. Verify that test outputs remain consistent across code changes
2. Detect regressions in ESP generation
3. Provide reference outputs for debugging

## Structure

```
Baselines/
├── scenario-basic-mapping/
│   ├── metadata.json      # Test execution metadata
│   ├── diagnostics.json   # Diagnostic output
│   └── *.esp              # Generated ESP files
├── scenario-dlc-remap/
│   └── ...
└── summary.json           # Overall test summary
```

## Updating Baselines

To update baselines after intentional changes:

1. **Automatic**: Trigger the GitHub Actions workflow with `Update baseline artifacts` enabled
2. **Manual**: Run tests locally and copy artifacts to this directory

```powershell
# Run integration tests with artifact output
dotnet test tests/IntegrationTests/IntegrationTests.csproj `
  -c Release `
  --logger "trx;LogFileName=results.trx"

# Copy new baselines
Copy-Item -Path "test-artifacts/scenarios/*" -Destination "tests/IntegrationTests/Baselines/" -Recurse -Force
```

## CI/CD Integration

The E2E test workflow (`.github/workflows/e2e-tests.yml`) automatically:
- Runs integration tests on PR and push
- Uploads artifacts for comparison
- Compares against baselines on PRs
- Creates auto-update PRs when requested

## Ignored Fields

The following fields are ignored during baseline comparison:
- Timestamps
- FormID counters
- Machine-specific paths

See `ESPValidationProfile.IgnoreHeaderFields` for the complete list.
