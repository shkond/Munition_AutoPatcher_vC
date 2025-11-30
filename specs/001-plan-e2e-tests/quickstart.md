# Quickstart — ViewModel E2E Harness

## Prerequisites
- Windows 10/11 dev box (same as CI) with .NET 8 SDK installed.
- Fallout 4 masters available to Mutagen via `TestEnvironmentBuilder` (the builder mocks them, no local install required).
- Git branch with latest E2E implementation checked out and dependencies restored (`dotnet restore`).

## Local Workflow

### 1. Run all integration tests

```powershell
# PowerShell (recommended)
./run-integration-tests.ps1

# Or directly with dotnet
dotnet test tests/IntegrationTests/IntegrationTests.csproj -v minimal
```

### 2. Run only ViewModelE2E tests

```powershell
# Using the script
./run-integration-tests.ps1 -Suite ViewModelE2E

# Or with filter
dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter "FullyQualifiedName~ViewModelE2ETests" -v minimal
```

### 3. Run with artifact collection

```powershell
# Specify custom artifact output path
./run-integration-tests.ps1 -Suite ViewModelE2E -ArtifactPath ./my-artifacts -Verbose
```

### 4. Adding a new scenario

Create a JSON manifest in `tests/IntegrationTests/Scenarios/`:

```json
{
  "id": "scenario-my-test",
  "displayName": "My Test Scenario",
  "description": "Tests a specific weapon-ammo mapping case",
  "pluginSeeds": [
    {
      "name": "MyTestMod.esp",
      "builderActionName": "CreateBasicWeaponAmmoScenario",
      "ownsEnvironment": true
    }
  ],
  "expectedEspName": "MyTestMod_Munition.esp",
  "validationProfile": {
    "profileId": "basic-validation",
    "ignoreHeaderFields": ["timestamp", "nextFormId"],
    "structuralExpectations": {
      "weaponCount": "atleast:1"
    }
  }
}
```

Register any new builder actions in `TestDataFactoryScenarioExtensions.cs`:

```csharp
catalog.RegisterBuilderAction("MyCustomAction", builder => {
    builder.WithWeapon("MyWeapon", weap => { /* ... */ });
});
```

### 5. Inspect artifacts

- **Generated ESPs**: `%TEMP%/MunitionAutoPatcher_E2E_Tests/<run>/Output` or custom `ArtifactPath`
- **Validation logs**: `<ArtifactPath>/test-results/` (TRX files)
- **Scenario artifacts**: `<ArtifactPath>/scenarios/<scenario-id>/`
  - `metadata.json` - Test execution metadata
  - `diagnostics.json` - Diagnostic output
  - `*.esp` - Generated plugin files
- **xUnit logs**: xUnit ITestOutputHelper outputs to console

## CI Workflow

1. Push changes to a PR targeting `main` or `develop`.
2. GitHub Actions workflow `.github/workflows/e2e-tests.yml` triggers automatically.
3. The workflow:
   - Runs on `windows-latest`
   - Builds the solution
   - Executes integration tests
   - Uploads artifacts:
     - `test-results` - TRX test result files
     - `scenario-artifacts` - Per-scenario outputs
     - `generated-esps` - All ESP/ESM files
     - `diagnostics` - CSV, log, and diagnostic JSON files

### Baseline Comparison

On PRs, a separate job compares generated artifacts against baselines:
- New scenarios without baselines are flagged
- Changes to existing scenario outputs are highlighted
- Use the workflow dispatch with `Update baseline artifacts` to generate a baseline update PR

## Directory Structure

```
tests/IntegrationTests/
├── Infrastructure/
│   ├── Models/                    # E2EScenarioDefinition, ScenarioRunArtifact, etc.
│   ├── ViewModelHarness.cs        # Main test harness
│   ├── TestServiceProvider.cs     # DI container for tests
│   ├── AsyncTestHarness.cs        # Timeout/cancellation handling
│   ├── EspFileValidator.cs        # ESP validation via Mutagen
│   ├── ScenarioCatalog.cs         # Loads JSON scenario manifests
│   ├── ScenarioManifestSerializer.cs  # JSON serialization
│   ├── ScenarioArtifactPublisher.cs   # Artifact publishing
│   ├── BaselineDiff.cs            # Baseline comparison
│   └── README.md                  # Infrastructure documentation
├── Scenarios/                     # JSON scenario manifests
│   ├── scenario-basic-mapping.json
│   ├── scenario-dlc-remap.json
│   └── ...
├── Baselines/                     # Golden baseline artifacts
│   └── README.md
├── Tests/                         # Additional test classes
│   ├── ScenarioArtifactPublisherTests.cs
│   └── BaselineDiffTests.cs
└── ViewModelE2ETests.cs           # Main E2E test class

```

## Troubleshooting

### ESP not generated
- Check `MapperViewModel.StatusMessage` captured in test output
- Ensure `TestServiceProvider` injected correct paths
- Verify scenario's `builderActionName` is registered

### Test timeout
- Increase `timeoutSeconds` in scenario manifest (default: 120)
- Check for infinite loops in builder actions

### Missing builder action
- Verify action is registered in `TestDataFactoryScenarioExtensions.RegisterAllActions()`
- Check spelling of `builderActionName` in JSON manifest

### Flaky baseline diffs
- Update validation profile to ignore additional header fields
- Add patterns to `BaselineDiff.AddIgnorePattern()` for dynamic content

### Missing dependencies
- Run `dotnet restore` at repo root
- Confirm `Mutagen.Bethesda.Fallout4` v0.51.5 is referenced

### CI failures
- Check uploaded `test-results` artifact for TRX details
- Review `diagnostics` artifact for scenario-specific errors
- Compare `scenario-artifacts` with expected baselines

## Key Classes

| Class | Purpose |
|-------|---------|
| `ViewModelHarness` | Orchestrates E2E test execution |
| `TestServiceProvider` | Provides DI container with test-safe services |
| `AsyncTestHarness` | Manages timeouts and cancellation |
| `EspFileValidator` | Validates generated ESP files |
| `ScenarioCatalog` | Loads and manages scenario manifests |
| `ScenarioArtifactPublisher` | Publishes test artifacts |
| `BaselineDiff` | Compares outputs against baselines |
