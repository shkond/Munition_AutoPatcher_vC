# Data Model — E2E Test Implementation Plan

## Overview
The E2E suite revolves around declarative scenario definitions, reusable validation profiles, and rich run artifacts that capture inputs, outputs, and diagnostics. These models live inside `tests/IntegrationTests/Infrastructure` so they can be consumed by both xUnit fixtures and helper utilities.

## Entities

### `E2EScenarioDefinition`
| Field | Type | Description / Validation |
| --- | --- | --- |
| `Id` | string (slug) | Unique scenario identifier used for folder names/logging. `[a-z0-9_-]+`, required. |
| `DisplayName` | string | Human-readable title; max 80 chars; required for reports. |
| `Description` | string | Optional markdown summary of what the scenario covers. |
| `PluginSeeds` | `List<PluginSeed>` | Ordered collection describing each plugin to materialize via `TestEnvironmentBuilder`. Must contain at least one entry. |
| `GameDataRoot` | string | Optional override; defaults to temp root returned by `TestServiceProvider.CreateTestDirectories()`. Must be absolute if set. |
| `OutputRelativePath` | string | Relative subfolder inside the test output root where generated ESPs/logs land; defaults to `scenario-{Id}`. |
| `ExpectedEspName` | string | File name (including extension) expected from `IEspPatchService`. Required. |
| `ValidationProfile` | `ESPValidationProfile` | Structural + binary validation instructions (by reference, see below). Required. |
| `ScenarioAssertions` | `List<ScenarioAssertion>` | Additional ViewModel-level assertions (e.g., status text, log markers). Optional; each must define `Target` + `ExpectedValue`. |
| `ConfigOverrides` | `Dictionary<string,string>` | Key/value overrides passed into `IConfigService` (e.g., disable DLC exclusion). Keys must match existing config service setters. |
| `TimeoutSeconds` | int? | Optional; シナリオ全体のタイムアウト（CancellationTokenSourceの設定に使用）。未指定時は既定300秒。 |

**Relationships**:
- `E2EScenarioDefinition` *has one* `ESPValidationProfile` (shared profiles allowed).
- Each `PluginSeed` maps to helper methods on `TestEnvironmentBuilder` (e.g., `CreateBasicWeaponAmmoScenario`).

**Validation Rules**:
- Duplicate `Id` values rejected during scenario discovery.
- `PluginSeeds` may not point to the same plugin filename twice within a scenario.
- `ConfigOverrides` keys must exist on the test ConfigService stub (hard validation to catch typos early).

### `PluginSeed`
| Field | Type | Description |
| --- | --- | --- |
| `Name` | string | Plugin filename (e.g., `TestMod.esp`). Required. |
| `BuilderAction` | `Action<TestEnvironmentBuilder>` | Delegate that mutates the builder; typically points to `TestDataFactory` extension. Required. |
| `BaselineCopySource` | string? | Optional path to an existing ESP that should be copied into the test data directory before running (for large fixtures). |
| `OwnsEnvironment` | bool | この`PluginSeed`が専用`GameEnvironment`を作成する場合true（テスト終了時にDispose）。falseの場合は共有環境を参照。 |

### `ESPValidationProfile`
| Field | Type | Description / Validation |
| --- | --- | --- |
| `ProfileId` | string | Identifier so multiple scenarios can reuse the same validation rules. |
| `BaselineArtifacts` | string? | Path to the golden ESP used for byte comparison. Optional; if empty, suite only performs structural checks. Must exist when specified. |
| `IgnoreHeaderFields` | `List<HeaderField>` | Enumerates mod-header fields to zero/skip during byte diff (e.g., `Timestamp`, `NextFormId`, `Author`). Defaults to `Timestamp` + `NextFormId`. |
| `AllowedWarnings` | `List<string>` | Set of warning substrings that are tolerated for this scenario (e.g., small file warning). |
| `FatalErrorPatterns` | `List<string>` | エラーメッセージ部分一致で致命判定するパターン（例: "FormKey resolution failed for master record"）。該当時はテスト失敗。 |
| `StructuralExpectations` | `StructuralExpectation` | Aggregated counts + predicates (see below).

`StructuralExpectation`
| Field | Type | Description |
| --- | --- | --- |
| `WeaponCount` | `Range` | Expected WEAP records that should appear in the generated ESP; range allows tolerance. |
| `AmmoCount` | `Range` | Expected AMMO records or overrides touched. |
| `CobjCount` | `Range` | Expected number of COBJ overrides written. |
| `CustomChecks` | `List<CustomCheck>` | Extra inspectors (e.g., confirm that a specific form key appears). Each check provides a lambda receiving the parsed `Fallout4Mod`. |

`Range` simple struct with `Min`/`Max` ints.

`CustomCheck`
| Field | Type | Description |
| --- | --- | --- |
| `Description` | string | Human-readable label reported when failing. |
| `Execute` | `Func<Fallout4Mod, ValidationResult>` | Returns pass/fail plus optional error text.

### `ScenarioRunArtifact`
| Field | Type | Description |
| --- | --- | --- |
| `ScenarioId` | string | Foreign key to `E2EScenarioDefinition.Id`. |
| `ExecutionTimestampUtc` | DateTime | Start time of the run. |
| `Duration` | TimeSpan | Total execution duration. |
| `TempDataPath` | string | Physical path to the synthetic `Data` directory used for Mutagen. |
| `TempOutputPath` | string | Physical path where `MapperViewModel` emitted the ESP. |
| `GeneratedEspPath` | string | Full path to the produced ESP (should match `TempOutputPath/ExpectedEspName`). |
| `ValidationResult` | `ValidationResult` | Captures counts, warnings, errors (reuse the structure from `EspFileValidator`). |
| `Diagnostics` | `DiagnosticBundle` | Logger出力とDiagnosticWriterファイルを集約。詳細は下記。 |
| `ArtifactsRoot` | string | Location zipped/uploaded by CI for download. |

**State transitions**:
1. `Initialized` — directories created, scenario metadata logged.
2. `ViewModelExecuted` — `MapperViewModel` commands finished (success/fail recorded).
3. `EspValidated` — `ValidationResult` computed.
4. `Published` — artifacts zipped and uploaded (CI step). Each transition appends to run manifest for traceability.

### `DiagnosticBundle`
| Field | Type | Description |
| --- | --- | --- |
| `LogFilePaths` | `List<string>` | xUnit ITestOutputHelper経由のログまたはファイルベースログへのパス |
| `DiagnosticWriterOutputs` | `List<string>` | `IDiagnosticWriter`が生成したCSV/JSONパス |
| `ValidationReports` | `List<string>` | `EspFileValidator`のdiffレポートパス |
| `CIArtifactRoot` | string? | CI環境でのアップロード先ルート（GitHub Actions artifacts等） |

### `ValidationResult` (test-layer variant)
| Field | Type | Description |
| --- | --- | --- |
| `IsValid` | bool | Computed flag (no errors). |
| `Errors` | `List<string>` | Structural or binary validation failures. |
| `Warnings` | `List<string>` | Non-blocking findings (e.g., small file). |
| `RecordCount` | int | Total of WEAP/AMMO/COBJ touched. |
| `FileSizeBytes` | long | Size of the generated ESP. |
| `HasValidHeader` | bool | Whether the parsed header passed deterministic checks.

## Relationships Summary
- `E2EScenarioDefinition 1..*` `PluginSeed` (composition).
- `E2EScenarioDefinition 1..1` `ESPValidationProfile` (reference; multiple scenarios can reuse via dictionary lookup).
- `ScenarioRunArtifact` references both `E2EScenarioDefinition` (by Id) and `ESPValidationProfile` (captured indirectly in serialized manifest for auditing).

## Notes
- Data structures stay in test assemblies to avoid shipping them with production binaries.
- Serialization format for scenario manifests should be JSON (UTF-8, newline delimited) to ease diffing.
- Scenario definitions may live as strongly-typed objects (preferred) or YAML/JSON descriptors loaded into the above structure before tests execute.
