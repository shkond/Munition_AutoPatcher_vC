# E2E Test Infrastructure

This directory contains the supporting infrastructure for the ViewModel-driven end-to-end test harness.

## Directory Structure

```text
Infrastructure/
├── Models/                      # Data contracts for scenarios, artifacts, and validation
│   ├── E2EScenarioDefinition.cs # Scenario manifest structure
│   └── ScenarioRunArtifact.cs   # Run results and diagnostics bundle
├── TestDataFactory.cs           # Existing test data generation helpers
├── TestEnvironmentBuilder.cs    # Existing Mutagen environment setup
├── TestServiceProvider.cs       # DI container mirroring App.xaml.cs for tests
├── AsyncTestHarness.cs          # CancellationToken/timeout coordination
├── EspFileValidator.cs          # ESP structural + binary validation
├── ViewModelHarness.cs          # MapperViewModel execution wrapper
├── ScenarioCatalog.cs           # Scenario discovery and loading
├── ScenarioManifestSerializer.cs# JSON serialization for manifests
├── ScenarioArtifactPublisher.cs # Artifact packaging for CI
└── README.md                    # This file
```

## Key Components

### TestServiceProvider
Mirrors the production DI registrations from `App.xaml.cs` while swapping:
- `IConfigService` → in-memory test configuration
- `IPathService` → temp-root isolation
- `IDiagnosticWriter` → scenario-specific output folder

### EspFileValidator
Validates generated ESP files using:
1. **Structural checks**: WEAP/AMMO/COBJ record counts via Mutagen overlays
2. **Binary comparison**: Header-normalized byte diffs against golden baselines

### ViewModelHarness
Coordinates scenario execution:
1. Builds `TestEnvironmentBuilder` with plugin seeds
2. Resolves `MapperViewModel` via `TestServiceProvider`
3. Runs ViewModel commands
4. Collects `ScenarioRunArtifact` with diagnostics

## Usage

See [quickstart.md](../../../specs/001-plan-e2e-tests/quickstart.md) for local and CI workflow instructions.

## Related Files

- `../Scenarios/` - JSON scenario manifests and embedded resources
- `../ViewModelE2ETests.cs` - Main xUnit test fixture
- `../Artifacts/` - Golden ESP baselines for CI validation
