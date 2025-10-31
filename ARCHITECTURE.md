# Application Architecture

## Main Window Layout

```
┌─────────────────────────────────────────────────────────────────────┐
│ Munition AutoPatcher                                          [_][□][X]│
├─────────────────────────────────────────────────────────────────────┤
│ ファイル(F)                    ヘルプ(H)                              │
│   ├─ 設定                        ├─ バージョン情報                   │
│   ├─ マッピング                                                       │
│   ├─ ─────                                                            │
│   └─ 終了                                                             │
├─────────────────────────────────────────────────────────────────────┤
│                                                                       │
│  [Content Area - Settings View or Mapper View]                      │
│                                                                       │
│                                                                       │
│                                                                       │
│                                                                       │
│                                                                       │
│                                                                       │
├─────────────────────────────────────────────────────────────────────┤
│ Status: 準備完了                                                      │
├─────────────────────────────────────────────────────────────────────┤
│ ログ                                                                  │
│ ┌─────────────────────────────────────────────────────────────────┐ │
│ │ [HH:mm:ss] アプリケーションを初期化しています...                  │ │
│ │ [HH:mm:ss] 初期化が完了しました                                  │ │
│ │ [HH:mm:ss] 準備完了                                              │ │
│ └─────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────┘
```

## Core Architectural Principles

### MVVM (Model-View-ViewModel)
- **Views**: Purely UI-focused, defined in XAML (`SettingsView.xaml`, `MapperView.xaml`).
- **ViewModels**: Manage UI state and logic (`SettingsViewModel.cs`, `MapperViewModel.cs`). They connect the View to the application's business logic via data binding and commands.
- **Models**: Represent application data structures (`WeaponData.cs`, `StrategyConfig.cs`).

### Service Layer & Dependency Injection
- Business logic is encapsulated in services with clearly defined interfaces (e.g., `IWeaponsService`, `IConfigService`).
- All dependencies (ViewModels, Services) are provided via a Dependency Injection container, promoting loose coupling and high testability.

### Resource Management & Mutagen Integration
- Interaction with the Mutagen API is a critical, resource-intensive task. This is managed through a robust, abstracted factory pattern.
- `IMutagenEnvironmentFactory`: A factory responsible for creating Mutagen environment instances.
- `IResourcedMutagenEnvironment`: An interface that combines the `IMutagenEnvironment` (operation abstraction) with `IDisposable`.
- Callers **must** obtain an environment from the factory within a `using` block (`using (var env = factory.Create()) { ... }`). This guarantees that the environment and its associated file handles are correctly disposed of, preventing memory leaks and file locks.

### Modularity and Single Responsibility
- Complex operations are broken down into smaller, single-responsibility helper classes. For example, the `WeaponOmodExtractor` service delegates tasks to:
  - `CandidateEnumerator`: Finds potential weapon mod candidates.
  - `ReverseMapBuilder`: Builds reverse-lookup maps for efficient processing.
  - `DiagnosticWriter`: Writes diagnostic and log files.
- This approach enhances maintainability, testability, and readability.

### Asynchronous Operations & UI Responsiveness
- Long-running tasks, such as data extraction, are executed on background threads using `Task.Run` and `AsyncRelayCommand`.
- This ensures the UI remains responsive. Progress is reported to the user via status bar updates, progress indicators, and detailed log messages.

### Observable Error Handling
- A project-wide policy mandates that exceptions must not be silently ignored.
- Empty `catch {}` blocks are forbidden. Instead, exceptions are caught and logged via a centralized `AppLogger` (`catch (Exception ex) { AppLogger.Log(ex, "..."); }`) to ensure all errors are observable for debugging and support.

## Functional Flow
1. **Settings Screen** → Set game data path, output path, and mapping strategy.
2. **Start Extraction** → Trigger weapon and OMOD data extraction from game files. This is an asynchronous process.
3. **Mapper Screen** → View extracted weapons and their potential mappings.
4. **Generate Mappings** → Create weapon-to-ammo mappings based on selected strategies.
5. **Generate INI** → Create the final `RobCoPatcher.ini` configuration file based on the mappings.

## 注意事項（表示言語）
本アプリケーションでは、Mutagen が提供する翻訳文字列（ITranslatedString）から表示用文字列を取得する際に、優先言語の選択が重要です。

現在の実装では、Mutagen の翻訳文字列を日本語 → 英語 → TargetLanguage の順で選択するロジックを採用しています。このため、日本語環境では以下のように正しい日本語が取得でき、UI 上で文字化け（mojibake）は発生していません（例: "5.56口径弾", "フュージョン・セル", "ショットガンシェル" 等）。

ただし、英語など別の言語環境で他者が検証を行う場合、TargetLanguage の扱いや環境依存のコードページにより表示に問題が出る可能性があります。従って、UI 側で表示の優先言語（例: 日本語、英語、または EditorID）を明示的に設定できるようにする必要があります。

具体的には以下を注意事項として記載します:
- UI に「優先表示言語」を追加し、ユーザーが日本語／英語／EditorID の順序を切り替えられるようにすること。
- デフォルトは環境に依存せず安全な設定（例: 日本語環境では日本語優先、英語環境では英語優先）とし、ユーザーが任意に上書き可能にすること。
- さらに、翻訳エントリが存在しない場合は EditorID をフォールバック表示することで、文字化けや不正なレンダリングの回避を容易にすること。

この対応により、異なるロケール環境での表示差異による問題を未然に防ぎ、外部の検証者が使用する環境でも安定して正しい文字列を表示できるようになります。