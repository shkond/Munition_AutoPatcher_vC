# ドキュメント / Documentation

このディレクトリには、Munition AutoPatcher vC プロジェクトの補足ドキュメントが含まれています。

## ドキュメント一覧

### 技術リファレンス

| ファイル | 説明 |
|---------|------|
| [Mutagen_FO4_AmmoNotes.md](Mutagen_FO4_AmmoNotes.md) | Mutagen.Bethesda.Fallout4 v0.51 における弾薬関連 API の型情報と実装パターン |

### 運用ガイド

| ファイル | 説明 |
|---------|------|
| [spaces-mcp-recommendation.md](spaces-mcp-recommendation.md) | Copilot Spaces + MCP サーバー連携の推奨設定と運用ガイドライン |

## テスト / Testing

### E2E Integration Test Harness

プロジェクトには ViewModel 駆動の E2E テストハーネスが含まれています：

| 場所 | 説明 |
|------|------|
| [tests/IntegrationTests/](../tests/IntegrationTests/) | E2E テストプロジェクト |
| [tests/IntegrationTests/Infrastructure/](../tests/IntegrationTests/Infrastructure/) | テストハーネスインフラ |
| [tests/IntegrationTests/Scenarios/](../tests/IntegrationTests/Scenarios/) | JSON シナリオマニフェスト |
| [tests/IntegrationTests/Baselines/](../tests/IntegrationTests/Baselines/) | ベースラインアーティファクト |

### E2E テストの実行

```powershell
# 全テスト実行
./run-integration-tests.ps1

# ViewModelE2E テストのみ実行
./run-integration-tests.ps1 -Suite ViewModelE2E

# アーティファクト出力先を指定
./run-integration-tests.ps1 -Suite ViewModelE2E -ArtifactPath ./my-artifacts
```

```bash
# Linux/macOS
./run-integration-tests.sh --suite ViewModelE2E
```

### シナリオの追加

新しいテストシナリオを追加するには：

1. `tests/IntegrationTests/Scenarios/` に JSON マニフェストを作成
2. `TestDataFactoryScenarioExtensions.cs` に必要な builder action を登録
3. テストを実行して自動的にシナリオが検出・実行されることを確認

詳細は [tests/IntegrationTests/Infrastructure/README.md](../tests/IntegrationTests/Infrastructure/README.md) を参照。

## 関連ドキュメント（ルートディレクトリ）

プロジェクトの主要ドキュメントはリポジトリのルートに配置されています：

| ファイル | 説明 |
|---------|------|
| [README.md](../README.md) | プロジェクト概要、セットアップ手順、使用方法 |
| [ARCHITECTURE.md](../ARCHITECTURE.md) | アプリケーションアーキテクチャとコア設計原則 |
| [DECISIONS.md](../DECISIONS.md) | アーキテクチャ決定記録（ADR） |
| [CONTRIBUTING.md](../CONTRIBUTING.md) | コントリビューションガイドライン |
| [CODING_CONVENTIONS.md](../CODING_CONVENTIONS.md) | コーディング規約と命名規則 |
| [MUTAGEN_API_REFERENCE.md](../MUTAGEN_API_REFERENCE.md) | Mutagen API ドキュメントの参照方法 |
| [CHANGELOG.md](../CHANGELOG.md) | 変更履歴 |
| [CODE_OF_CONDUCT.md](../CODE_OF_CONDUCT.md) | 行動規範 |
| [SECURITY.md](../SECURITY.md) | セキュリティポリシー |

## アーカイブ

過去の計画書や調査レポートは [`archive/`](../archive/) ディレクトリに保管されています。

このディレクトリには以下のようなドキュメントが含まれます：

- プロジェクト初期の設計計画書
- 完了した調査レポート
- 過去のリファクタリング計画
- PR サマリーや実装完了レポート

これらは参照用として保持されており、現在の開発作業にはルートディレクトリのドキュメントを使用してください。