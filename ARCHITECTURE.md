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

## ログ戦略 (Logging Strategy)

このプロジェクトでは、それぞれ特定の目的を持つ2つの異なるログ記録メカニズムを利用します。

### 1. `ILogger` (Microsoft.Extensions.Logging)

-   **レイヤー**: ビジネスロジック層 (`MunitionAutoPatcher.Services`)
-   **目的**: サービス内での構造化されたセマンティックなログ記録。このロガーは依存性注入（DI）コンテナと統合されています。
-   **用途**: アルゴリズムの実行ステップ、データに関する警告、処理中のエラーなど、アプリケーションの中核ロジックに関連するイベントのログに使用します。ビジネスプロセスのデバッグとトレースを容易にします。
-   **出力**: `Host`を介して構成されます。デフォルトでは、デバッグおよびコンソール出力にストリーミングされます。

### 2. `AppLogger`

-   **レイヤー**: アプリケーション層 (UI、例: `ViewModels`, `App.xaml.cs`)
-   **目的**: UI関連のイベントログ、およびユーザー向けのメッセージや重要なアプリケーションのライフサイクルイベントのためのシンプルなファイルベースの永続化。
-   **用途**: ユーザーインターフェースに関連するイベント（必要に応じてボタンクリックなど）、アプリケーションの起動/シャットダウン、またはユーザーがアクセスできる永続的なログファイルへの書き込みに使用します。
-   **出力**: ファイル（`App.log`など）に書き込み、UIコンポーネントに接続できます。

## 既知のアーキテクチャ上の問題 (Known Architectural Issues)

### 1. 例外のサイレントな握りつぶし (Silent Exception Swallowing)

**問題点:**
`ARCHITECTURE.md`の「Observable Error Handling」原則では、空の`catch`ブロックを禁止し、すべての例外をログに記録することを規定しています。しかし、現在のコードベースにはこの原則に違反する箇所が複数存在します。

-   **`AppLogger.cs`**: ロガー自体の安定性を確保する意図と思われますが、ファイルI/OやUIディスパッチ中の例外が`catch {}`ブロックによって完全に握りつぶされています。これにより、ログ出力の失敗が検知できません。
-   **`LinkCacheHelper.cs`**: リフレクションを多用する複雑なコード内で、エラーログの記録処理自体が空の`catch`で囲まれているなど、例外が無視される箇所があります。

**影響:**
これらの問題は、アプリケーションが予期せず失敗した際に、原因調査を著しく困難にします。

**推奨される対策:**
すべての`catch`ブロックで、最低でも`Debug.WriteLine(ex)`を呼び出すか、適切なロガー（`ILogger`または`AppLogger`）を使用して例外を記録するようにコードを修正する必要があります。

### 2. 多重責務クラス (Classes with Multiple Responsibilities)

**問題点:**
単一責任の原則（Single Responsibility Principle）に反し、一つのクラスが複数の異なる責務を担っている例が存在します。これにより、クラスの凝集度が低下し、保守性やテスト性が損なわれる可能性があります。

-   **`AppLogger.cs`**: 静的クラスでありながら、ログメッセージのUI通知、非同期でのファイル書き込み、ログファイルのパス管理と初期化、内部エラーのハンドリングなど、ロギング以外の多数の責務（ファイルI/O、ディレクトリ管理）を担っています。
-   **`LinkCacheHelper.cs`**: オブジェクト解決のための多様なリフレクション処理、キャッシュ、再入防止、エラー抑制、型変換など、関連性の低い複数のユーティリティ機能が単一のヘルパークラスに集約されています。
-   **`WeaponsService.cs`**: 武器データの抽出という主要な責務に加え、文字コード問題の修正、診断ファイルの直接書き込み、複雑なデータマッピング、型変換など、本質的でない多くの責務を含んでいます。特にファイルI/Oは別のサービスに委譲すべきです。

**影響:**
-   **保守性の低下**: 一つの変更が、関連性の低い他の機能に意図しない影響を与えるリスクが高まります。
-   **テスト性の低下**: クラスが多くの外部リソース（ファイルシステムなど）に直接依存するため、単体テストが困難になります。

**推奨される対策:**
将来的なリファクタリングで、これらのクラスの責務を分割することを推奨します。例えば、ファイルI/Oは専用のサービスに、型変換や文字列操作は独立したユーティリティクラスに、といった形で責務を分離します。
