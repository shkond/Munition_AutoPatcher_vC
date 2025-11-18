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

## 今後の追記方針

- ここに挙げた ADR は暫定サマリです。今後、具体的な PR やリファクタリングのタイミングで、
  より詳細な ADR を追加・更新していきます。
- 新しいインターフェース追加や、Mutagen の扱い方を大きく変えるような変更を行う場合は、
  必ずこのファイルにエントリを追加してください。