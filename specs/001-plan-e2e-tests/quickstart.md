# Quickstart — ViewModel E2E Harness

## Prerequisites
- Windows 10/11 dev box (same as CI) with .NET 8 SDK installed.
- Fallout 4 masters available to Mutagen via `TestEnvironmentBuilder` (the builder mocks them, no local install required).
- Git branch `001-plan-e2e-tests` checked out and dependencies restored (`dotnet restore`).

## Local workflow
1. **Create scenario directories**
   ```powershell
   mkdir tests/IntegrationTests/Infrastructure/Scenarios -Force
   ```
2. **Implement helpers**: add `EspFileValidator`, `TestServiceProvider`, `TestDataFactoryExtensions`, and `ViewModelE2ETests` under `tests/IntegrationTests` as outlined in the plan.
3. **Seed scenarios** using `TestEnvironmentBuilder` + new extension helpers; confirm expected ESP names inside each definition.
4. **Run the suite**:
   ```powershell
   dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter "FullyQualifiedName~ViewModelE2ETests" -v minimal
   ```
5. **Inspect artifacts**: 
   - **Generated ESPs**: `%TEMP%/MunitionAutoPatcher_E2E_Tests/<run>/Output`
   - **Validation logs**: `tests/IntegrationTests/TestResults/<timestamp>/diagnostics/`（`IDiagnosticWriter`出力）
   - **xUnit logs**: xUnit ITestOutputHelperがCIコンソールに出力、CLI実行時は標準出力に表示
   - **CI artifacts**: GitHub Actions実行時は`e2e-test-results`としてzipアップロード

## CI workflow
1. Commit changes and push to `001-plan-e2e-tests`.
2. GitHub Actions workflow `.github/workflows/e2e-tests.yml` triggers on PRs → runs `dotnet test` with the same filter on `windows-latest`.
3. ESP artifacts + validation manifests are uploaded via `actions/upload-artifact` as `e2e-test-results` for offline inspection.

## Troubleshooting
- **ESP not generated**: check `MapperViewModel.StatusMessage` captured by the test harness and ensure `TestServiceProvider` injected the correct game/output paths.
- **Flaky byte diffs**: update the validation profile to ignore any additional header fields discovered via `EspFileValidator` logs.
- **Missing dependencies**: `dotnet restore` at the repo root and confirm `Mutagen.Bethesda.Fallout4` v0.51.5 stays referenced via `MunitionAutoPatcher.csproj`.
