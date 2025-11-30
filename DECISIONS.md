# Munition AutoPatcher vC - 決定事項と開発記録

このドキュメントは、Munition AutoPatcher vC の「重要な設計判断（Architecture Decision）」と、
それに紐づく開発の経緯を記録するためのものです。

- 設計やアーキテクチャに関する合意事項を、後から辿れるようにする
- なぜそうしたのか（Why）を残し、将来の自分/他の開発者の判断を助ける
- Pull Request やチャットログに埋もれがちな情報を一箇所に集約する

**運用ルール（最小）**
- 「設計レベル」の変更を行ったら、必ずここにエントリを 1 つ追加する
- 1 つのエントリは「1 つの決定」に絞る（複数の決定を混ぜない）
- 書き方は以下のテンプレートに従う

---

## テンプレート

新しい決定を追加する際は、このテンプレートをコピーして編集します。

```markdown
### ADR-XXX: <短いタイトル>

- **Date**: YYYY-MM-DD
- **Status**: Proposed / Accepted / Deprecated / Superseded by ADR-YYY
- **Related-PR**: #<number> (あれば)
- **Author**: @<GitHubユーザ名>

#### Context
- なぜこの決定が必要になったのか
- どのような問題・背景があったのか
- 関係するファイルや機能（例: `WeaponOmodExtractor`, Mutagen Accessor など）

#### Decision
- 採用した方針の要約
- 「こうする」という具体的なルール・インターフェース・責務分担など

#### Alternatives
- 検討したが採用しなかった案と、その理由
  - 案A: ……（却下理由）
  - 案B: ……（却下理由）

#### Consequences
- この決定によって得られるメリット
- 発生しうるデメリットや制約
- 将来的に変更する場合のコストや考慮点
```

---

## 既存の主要な決定（サマリ）

> 詳細は今後、個別の ADR として整理していきます。ここでは、すでにプロジェクトで合意済みの
> 重要な決定を箇条書きでまとめています。

### ADR-001: WPF MVVM + DI アーキテクチャ

- **Date**: 2025-10-28
- **Status**: Accepted
- **Context**:
  - Fallout 4 の武器 MOD から RobCo Patcher 用の設定を生成する WPF アプリとして、
    UI・ロジック・データを明確に分離し、テストしやすく保守性の高い構成にしたい。
- **Decision**:
  - View: XAML による UI 表現のみ（ビジネスロジック禁止）
  - ViewModel: UI 状態・コマンド・検証を担当（DI でサービスを受け取る）
  - Model/Service: ビジネスロジックとデータ操作（UI 非依存）
  - 依存性注入に `Microsoft.Extensions.DependencyInjection` を採用し、すべてのサービスは DI コンテナから提供する。
- **Consequences**:
  - テスト容易性と拡張性が向上する一方、初期実装のコード量と抽象化レイヤは増える。

---

### ADR-002: Mutagen を IMutagenAccessor 経由でのみ利用する

- **Date**: 2025-11-18
- **Status**: Accepted
- **Context**:
  - Mutagen.Bethesda.Fallout4 は強力だが複雑であり、API の変更やバージョン差が頻繁に起こりうる。
  - ViewModel や個別のサービスが Mutagen の型に直接依存すると、リファクタリングやバージョンアップが困難になる。
- **Decision**:
  - Mutagen の呼び出しはすべて `IMutagenAccessor`（またはその下の Strategy）に集約する。
  - View/ViewModel/Service は Mutagen を直接参照せず、Accessor が提供する安定な抽象 API にのみ依存する。
- **Alternatives**:
  - 各サービスが直接 Mutagen を参照する案
    - 取り回しは簡単だが、Mutagen の変更がコード全体に波及するため却下。
- **Consequences**:
  - Accessor 層の責務は増えるが、Mutagen の仕様変更や検出ロジックの改良をその層に閉じ込めやすくなる。
  - テストでは `IMutagenAccessor` をモックするだけで多くのケースをカバーできる。

---

### ADR-003: Mutagen バージョン pin なし + Detector パターンで機能検知

- **Date**: 2025-11-18
- **Status**: Accepted
- **Context**:
  - 長期的には Mutagen のバージョンをアップデートし続けたいが、そのたびに全面的なコード修正は避けたい。
  - 以前は特定バージョン（例: 0.51.x）に依存する Detector 実装があり、バージョンアップが難しくなっていた。
- **Decision**:
  - パッケージバージョンを「固定」せず、アプリ起動時に Mutagen の機能（メソッド・型の有無）を検出する。
  - `MutagenCapabilityDetector` で機能検知し、`IMutagenApiStrategy` の実装を切り替える。
- **Alternatives**:
  - バージョンを固定し、Mutagen の更新タイミングで手動移行する案
    - 一見シンプルだが、将来の保守性と柔軟性を損なうため不採用。
- **Consequences**:
  - Accessor/Detector 層が多少複雑になるが、将来の Mutagen 更新を低コストで吸収できる。
  - リフレクション等を使う場合も、この層に閉じ込めて型安全 API に変換する必要がある。

---

### ADR-004: ログ設計 — ILogger + IAppLogger + AppLoggerProvider

- **Date**: 2025-11-18
- **Status**: Accepted
- **Context**:
  - 既存コードでは `AppLogger` によるログと、将来導入予定の `ILogger<T>`（Microsoft.Extensions.Logging）が混在する可能性がある。
  - ログ出力先パス、重大度、UI 通知のルールを統一したい。
- **Decision**:
  - 既定ログパスは `./artifacts/logs/munition_autopatcher_ui.log` とし、アプリ起動時に作成と書込可否を検証する。
  - サービス層は `ILogger<T>` を使用し、UI 層とユーザ通知は `IAppLogger` を使用する。
  - `AppLoggerProvider` が `ILoggerProvider` を実装し、ログのファイル出力と `IAppLogger` への転送（Warning 以上）を行う。
  - コンソール出力（`Console.WriteLine` / `Debug.WriteLine`）は禁止。
- **Consequences**:
  - ログ経路が明確になり、重大なエラーを UI に確実に届けられる。
  - ログ初期化/フォールバック処理が App 起動コードに必要になる。

---

### ADR-005: AI 支援開発のステージ制運用

- **Date**: 2025-11-18
- **Status**: Accepted
- **Context**:
  - AI が Mutagen API や内部仕様を十分に参照せず、リフレクションや推測コードに依存する提案をすることがあった。
  - 型安全で保守しやすいコードを得るためには、AI 利用のプロセス自体を制御する必要がある。
- **Decision**:
  - AI を利用する際は、以下のステージを明示的に分けて運用する:
    - Stage 1: API 選定レビュー（ProposedAPIs 列挙のみ。コード生成禁止）
    - Stage 2: 設計合意（入出力・ErrorPolicy・Performance・DisposePlan の合意）
    - Stage 3: 最小スパイク（短い snippet / pseudo-code）
    - Stage 4: 本実装
  - AI への固定ガードレール:
    - Reflection / dynamic 使用禁止
    - Mutagen 呼び出しは Accessor 経由のみ
    - 非同期 + WinningOverrides + LinkCache + DisposePlan を明示
  - Stage 1 の AI 回答には必ず以下のセクションを含める:
    - ProposedAPIs / Rationale / ErrorPolicy / Performance / DisposePlan / References
- **Consequences**:
  - 1 回で「コードまで」出してもらうより、やり取りは増えるが、設計品質と型安全性が向上する。
  - 決定された設計は本ファイルと CONSTITUTION に反映しやすくなる。

---

### ADR-006: IntegrationTests および LinkCacheHelperTests のコンパイルエラー修正

- **Date**: 2025-11-19
- **Status**: Accepted
- **Context**:
  - `IntegrationTests` および `LinkCacheHelperTests` プロジェクトにおいて、Mutagen のバージョンアップやコードの不整合により多数のコンパイルエラーが発生していた。
  - 主な原因は、Mutagen 0.43 以降での API 変更（`WriteTo` の削除など）、型システムの厳格化（`IFormLink` のキャスト）、およびテストコード内の不適切なモック実装であった。
- **Decision**:
  - **Mutagen API の更新**:
    - `Fallout4Mod.WriteTo` が削除されたため、`WriteToBinary` に置き換えた。
    - `IFormLink<T>` から `IFormLinkNullable<IInterfaceGetter>` への暗黙的変換エラーに対し、`.AsSetter().AsNullable()` を明示的に呼び出すように修正した。
  - **テストコードのモック化方針**:
    - `LinkResolverTests` において、手動で作成したダミークラス（Fake Class）が `ILinkCache` インターフェースの要件を満たさずエラーとなっていた。
    - 手動実装をやめ、`Moq` ライブラリを使用して `ILinkCache` および関連インターフェース（`IMajorRecordGetter`, `IAmmunitionGetter`）をモック化する方針に変更した。
  - **Null 許容性の厳格化への対応**:
    - テストデータプロバイダ（`MemberData`）において、`object[]` ではなく `object?[]` を使用し、`null` 値を含むテストケースでの警告（CS8619, CS8625）を解消した。
  - **FormKey の初期化**:
    - `FormKey` クラスのコンストラクタ変更に伴い、オブジェクト初期化子（Object Initializer）を使用する形式に統一した。
- **Consequences**:
  - プロジェクト全体のビルドが正常に通るようになり、CI/CD やローカル開発環境でのテスト実行が可能になった。
  - `Moq` の導入により、テストコードがより標準的かつ保守しやすくなった。
  - Mutagen の新しい API に準拠したことで、将来のバージョンアップへの耐性が向上した。

---

### ADR-007: E2E テストハーネス — ViewModel 駆動アーキテクチャ

- **Date**: 2025-11-25
- **Status**: Accepted
- **Related-PR**: N/A
- **Context**:
  - 手動統合テストに依存していたため、リグレッション検出が困難で、CI における自動検証ができなかった。
  - WPF シェルを使用せずに MapperViewModel の動作を検証し、生成された ESP ファイルを自動的に検証する必要があった。
  - シナリオの追加が容易で、コード変更なしにカバレッジを拡大できる宣言的なアプローチが求められた。
- **Decision**:
  - **ViewModelHarness パターン**:
    - `ViewModelHarness` が `TestEnvironmentBuilder` を使用して Mutagen 環境を構築
    - `TestServiceProvider` が `App.xaml.cs` の DI 登録をミラーリングし、テスト安全な実装に置き換え
    - `AsyncTestHarness` が CancellationToken とタイムアウト強制を管理
  - **ScenarioCatalog による宣言的シナリオ**:
    - シナリオは `tests/IntegrationTests/Scenarios/*.json` に JSON マニフェストとして定義
    - `ScenarioCatalog` がマニフェストをロードし、`E2EScenarioDefinition` にマテリアライズ
    - Builder アクションは `TestDataFactoryScenarioExtensions` で登録し、シナリオから名前で参照可能
  - **ESP バリデーション**:
    - `EspFileValidator` が Mutagen オーバーレイを使用して構造的な期待値（weapon/ammo/cobj カウント）を検証
    - `ESPValidationProfile` がヘッダーフィールドの無視ルールと許容される警告を定義
  - **アーティファクト管理**:
    - `ScenarioArtifactPublisher` が ESP、診断情報、メタデータを CI アクセス可能な場所に公開
    - `BaselineDiff` が生成されたアーティファクトをベースラインと比較し、回帰を検出
- **Alternatives**:
  - **Selenium/UI オートメーション**: WPF シェル起動のオーバーヘッドが大きく、テストが脆弱になるため却下。
  - **サービス層のみのテスト**: ViewModel 統合の問題を検出できないため、不十分と判断。
  - **コード内シナリオ定義**: シナリオ追加ごとにコード変更が必要になるため、宣言的 JSON アプローチを採用。
- **Consequences**:
  - **メリット**:
    - ESP 生成の CI 強制により、リグレッションを早期に検出可能。
    - JSON マニフェストによりシナリオ追加が容易。
    - `TestServiceProvider` により ViewModel ロジックをモック可能な依存関係でテスト可能。
    - アーティファクト公開により CI での診断が改善。
  - **デメリット**:
    - テストインフラの初期実装コストが増加。
    - シナリオカタログとシリアライザの保守が必要。
    - ベースラインのドリフトには承認ワークフローが必要。

---

### ADR-008: E2E テストの DTO 署名とキャンセル戦略

- **Date**: 2025-11-25
- **Status**: Accepted
- **Related-PR**: N/A
- **Context**:
  - E2E テストハーネスには、シナリオ定義とテスト実行アーティファクトに対する安定した DTO 契約が必要。
  - 長時間実行されるテストには適切なキャンセルとタイムアウト処理が必要。
- **Decision**:
  - **コア DTO**:
    - `E2EScenarioDefinition`: シナリオ ID、表示名、プラグインシード、検証プロファイル、アサーション
    - `PluginSeed`: プラグイン名、ビルダーアクション参照、環境所有権フラグ
    - `ESPValidationProfile`: プロファイル ID、無視するヘッダーフィールド、構造的期待値
    - `ScenarioRunArtifact`: 実行状態、期間、パス、診断バンドル、検証結果
  - **キャンセル戦略**:
    - `AsyncTestHarness` がテストスコープの `CancellationTokenSource` を管理
    - シナリオごとの `TimeoutSeconds`（デフォルト 120 秒）でタイムアウトを強制
    - キャンセル時のグレースフルシャットダウン（リソースのクリーンアップ後に状態を報告）
  - **シリアライゼーション**:
    - `ScenarioManifestSerializer` が `System.Text.Json` でシナリオ JSON を読み書き
    - `CountRangeJsonConverter` がショートハンド形式（`"exact:5"`, `"atleast:3"`）をサポート
    - スキーマ違反に対する厳格なバリデーションエラー
- **Consequences**:
  - DTO 契約により、テストコードとシナリオマニフェスト間の型安全性を保証。
  - タイムアウト強制により、テストが無限に実行されることを防止。
  - JSON シリアライゼーションによりシナリオの手動編集と CI 統合が容易に。

---

### ADR-009: FormKeyNormalizer 二重拡張子バグ修正と関連バグチェーン

- **Date**: 2025-11-30
- **Status**: Accepted
- **Related-PR**: N/A
- **Author**: @shkond

#### Context
- E2Eテストで「ESPファイルは作成されるがレコードが空」という現象が発生。
- 調査の結果、複数のバグが連鎖して発生していることが判明した。
- 根本原因は `FormKeyNormalizer` における `ModKey` コンストラクタの誤用。
- 詳細なデバッグ記録は `espdebug.md` に記載。

#### Decision

**1. FormKeyNormalizer の修正（根本原因）**
- `new ModKey(fileName, modType)` を `ModKey.FromNameAndExtension(fileName)` に変更。
- Mutagen API の挙動:
  - `new ModKey("TestMod.esp", ModType.Plugin)` → `"TestMod.esp.esp"` ✗
  - `ModKey.FromNameAndExtension("TestMod.esp")` → `"TestMod.esp"` ✓

**2. WeaponDataExtractor の CandidateFormKey 修正**
- `CandidateFormKey` を CreatedObject (Weapon) ではなく COBJ レコードの FormKey に設定。
- 後続の `AttachPointConfirmer` が COBJ を解決するために必要。

**3. MutagenV51EnvironmentAdapter の InMemoryLinkCache 対応**
- `InnerGameEnvironment` プロパティを追加。
- コンストラクタに `ILinkCache? inMemoryLinkCache` パラメータを追加。
- `EffectiveLinkCache` プロパティでインメモリを優先するように変更。

**4. TestEnvironmentBuilder のモッド重複作成防止**
- `WithPlugin()` で同じ ModKey に対して既存の mod を再利用するように変更。

#### Alternatives
- ModKey の入力から拡張子を手動で除去する案 → 複雑でエラーを招きやすいため却下。
- Mutagen API を直接使用せず文字列操作で FormKey を構築する案 → 型安全性が失われるため却下。

#### Consequences
- **メリット**:
  - E2Eテスト含む193件全てのテストが成功。
  - Mutagen の ModKey API の正しい使用法が明確化。
  - LinkCache 解決が正常に動作し、ESP 生成が成功。
- **デメリット**:
  - なし（純粋なバグ修正）。
- **学んだ教訓**:
  - Mutagen API は入力形式に敏感。`new ModKey()` と `ModKey.FromNameAndExtension()` の使い分けが重要。
  - 複合バグの診断には E2E テスト → 単体テスト → 診断ログの段階的アプローチが有効。

---

## 今後の追記方針

- ここに挙げた ADR は暫定サマリです。今後、具体的な PR やリファクタリングのタイミングで、
  より詳細な ADR を追加・更新していきます。
- 新しいインターフェース追加や、Mutagen の扱い方を大きく変えるような変更を行う場合は、
  必ずこのファイルにエントリを追加してください。