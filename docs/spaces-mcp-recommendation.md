# Spaces + MCP（GitHub 管理）推奨設定

## 目的
- このドキュメントは、Munition_AutoPatcher_vC リポジトリで Copilot Spaces（または類似の Spaces）を GitHub 管理の MCP サーバ経由で安全かつ実用的に使うための推奨設定をまとめたものです。

## 前提
- GitHub アカウント（必要に応じて組織の SSO 承認）
- VS Code（最新）と GitHub Copilot（またはエンタープライズの Copilot）
- リポジトリは主に C# (WPF) で、GUI 実行はローカル Windows を想定

## 要点（短く）
- 最初は read-only ツールセットで接続、機能確認後に限定的な write 権限を追加
- MCP で与えるコンテキストは最小化（関連ファイルのみ、シークレットは渡さない）
- CI (GitHub Actions) や Actions logs を参照する場合は組織ポリシーを確認

## 推奨ワークフロー
1. VS Code で GitHub 拡張と Copilot をインストール
2. VS Code から "MCP: List Servers" で GitHub 管理の MCP を選択
3. OAuth サインインし、ツールセットを read-only で有効化（repository, issues が代表例）
4. Space を作成し、必要なシナリオ（ドキュメント作成、PR 作成補助、CI トラブルシュート等）を選定
5. 小さな操作（検索、ファイルストリーム）で動作確認
6. 必要なら段階的に write 権限（PR 作成など）を付与

## ツールセット（推奨）
- repository: read-only
  - 理由: コード参照とコンテキスト提供に必要。最初は読み取りのみで安全確認。
- issues: read-write (オプション)
  - 理由: 自動で Issue を作成させたい場合のみ許可。運用ルールを定めてから。
- pull_requests: read-write (オプション、制限付き)
  - 理由: 自動 PR 作成を許可する場合は、マージは必ず手動レビューにするか、特定ブランチに対して制限。
- actions: read-only
  - 理由: ワークフローのログ参照は便利。ジョブの再実行などは運用上慎重に。

## 権限付与のベストプラクティス
- Principle of Least Privilege（最小権限）を徹底
- write 権限はサービスアカウントや限定ユーザーに付与
- 自動マージや force-push は禁止
- 変更は PR ベースで行い、必ずレビュープロセスを通す

## MCP を使うときの CI 連携注意点
- Windows 特有の GUI ビルドはローカルや Windows runner で行うこと
- Actions でビルドする場合、workflow は secrets や環境変数を安全に管理する
- CI のログを MCP で参照する際に、機密情報が含まれていないか確認

## 具体的な手順（簡易）
1. VS Code: 拡張をインストール → Sign in to GitHub
2. Command Palette: "MCP: List Servers" → GitHub MCP を選択
3. OAuth で承認 → 拡張設定で toolset を選択（repository: read-only をまず有効化）
4. Copilot Spaces（または Space UI）で新しい Space を作成 → MCP サーバを選択
5. Space 内で "search repository" や "stream file" を試す

## 安全運用チェックリスト
- [ ] read-only でテスト済み
- [ ] 権限付与は段階的に
- [ ] PR の自動マージは無効
- [ ] 監査ログの有無を確認
- [ ] 機密データが渡らない設定を確認

## 付録: 参考リンク
- GitHub Docs — Setting up the GitHub MCP Server: https://docs.github.com/en/copilot/how-tos/provide-context/use-mcp/set-up-the-github-mcp-server?tool=vscode
- GitHub Blog — A practical guide on how to use the GitHub MCP server: https://github.blog/ai-and-ml/generative-ai/a-practical-guide-on-how-to-use-the-github-mcp-server/