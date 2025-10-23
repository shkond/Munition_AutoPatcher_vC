# Copilot / Contributor instructions — Munition AutoPatcher

このファイルは、リポジトリへコードを追加・修正するときに守るべき前提知識と運用ルールをまとめたものです。
主に Mutagen と Mod Organizer 2 (MO2) を使った実行フロー、デバッグ手順、テスト方針、よくある落とし穴について記載します。

## 目的
- 新しくこのリポジトリに入る開発者や自動化エージェントが、意図せず誤った前提でコードを書かないようにする。
- Mutagen と MO2 の挙動（VFS）を尊重した実装を促進する。

## 環境と想定
- プロジェクトは .NET 8 / WPF (`net8.0-windows`) をターゲットにしています。
- テストランナー / 自動テストは `tests/AutoTests` に配置されています。
- 開発環境では Mod Organizer 2 のプロファイル／overwrite 構成がよく使われます。

## Mutagen と MO2（重要）
- MO2 が提供する仮想ファイルシステム (VFS) を常に尊重してください。MO2 経由でプロセスを起動すれば、プロセスは "マージされた" Data フォルダを見ることができます。これは非常に重要な前提です。
- 絶対にやってはいけない：MO2 の仮想化を無視して `overwrite` フォルダだけを Mutagen に与え、VFS を手動で再現しようとすること。多くの MOD が見えなくなり、結果が不正確になります。
- 実装の方針：Mutagen の `GameEnvironment`（例: `GameEnvironment.Typical.Fallout4(...)`）を優先して使い、環境が利用できない場合のみ従来の `PluginListings.LoadOrderListings` + `LoadOrder.Import` のフォールバックを行うこと。

## API の使い方（型安全）
- Mutagen は強く型付けされた API を提供します。可能な限り直接型を使って `PriorityOrder.Weapon().WinningOverrides()` のように呼んでください。
- リフレクションは原則禁止です。リフレクションを使うと型安全性を失い、メンテナンス負荷が急増します。どうしても必要な場合は明確な理由とテストを添えて PR を出してください。

## デバッグの慣習
- Debug 用の一時的な待機やコンソール表示は `#if DEBUG` で囲んでください。リリースビルドに混入することは避けます。
- このリポジトリでは `DebugConsole` ヘルパーを追加しています。Debug ビルド時に `DebugConsole.Show()` を呼ぶと Win32 コンソールが開き、標準入出力がそのコンソールにリダイレクトされます。終了時は `DebugConsole.Hide()` を呼んでください。
- テストや WPF アプリの起動時に "アタッチ待ち" を入れる場合、`Console.ReadLine()` を `#if DEBUG` 範囲内で使う運用としています。

## VS Code / launch.json / tasks.json の運用
- `launch.json` の `.NET Launch (WPF)` はプロジェクトのビルド出力を正しく参照するように設定してください（例: `${workspaceFolder}/MunitionAutoPatcher/bin/Debug/net8.0-windows/MunitionAutoPatcher.dll`）。
- 開発時は `console` を `integratedTerminal` にしておくとコンソール出力が VS Code のターミナルで見やすくなります。
- `tasks.json` の `build` タスクはプロジェクトの実際の csproj ファイルを指すようにしてください（サブフォルダにある場合は `${workspaceFolder}/MunitionAutoPatcher/MunitionAutoPatcher.csproj` のように）。

## テストの実行ルール（MO2 実環境での手順）
1. MO2 から `AutoTests` を起動する（または MO2 のプロファイル内で dotnet 実行ファイル/AutoTests.dll を起動する）。
2. テストは Debug ビルドで `DebugConsole.Show()` による待機を行います。コンソール上に表示されたメッセージでデバッガをアタッチしてから Enter を押してください。
3. テスト出力（特に `WinningOverrides` のカウントなど）を確認し、期待値と違う場合は `LoadOrderService` と `WeaponsService` の両方を検査してください。

## WeaponsService 周りの注意点
- `WeaponsService.ExtractWeaponsAsync()` が 0 を返す場合の最初の疑いは "必要なマスター（ESM）が Mutagen に見えていない" ことです。MO2 経由での起動であれば通常は解決します。もし `GameEnvironment` で 0 になる場合は、`env.DataFolderPath` 内に ESM が存在するかを確認してください。

## コーディングチェックリスト（PR 時）
- 新しいコードはまずローカルで Debug ビルドが通ること。
- MO2 対応のコードであれば、MO2 での実行による検証ログ（`env.DataFolderPath`、ロードオーダー件数、`WinningOverrides` 件数）を示すこと。
- リフレクションを使った場合は理由を明記し、代替案がないことを示すこと。
- `#if DEBUG` ブロックを使う場合、Release に影響が出ないことを確認すること。

## よくあるトラブルシュート（短く）
- 起動時にロードオーダーが `null` または空: MO2 経由で起動していないか、`GameEnvironment` の検出に失敗している可能性。
- Weapon の件数が 0: `env.DataFolderPath` に Fallout4.esm 等のマスターがあるか、またはテスト対象プラグインが実際に武器レコードを含むかを確認。
- ファイルパスの不一致（tasks.json / launch.json）: ルートとサブプロジェクトのパスに注意。

---
作業中にこのポリシーに疑問がある場合は小さな PR を作り、テストログと理由を添えて議論してください。

（このファイルは将来的に README へ要約を移すことを想定しています）
