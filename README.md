# Munition AutoPatcher vC

Fallout 4武器自動パッチツール - WPF MVVM実装

## 概要

このツールは、Fallout 4の武器MODに対して、RobCo Patcherの設定ファイル（INI）を自動生成するためのWPFアプリケーションです。Mutagenを使用してプラグインから武器データを抽出し、弾薬とのマッピングを行い、パッチ設定を生成します。

## 機能

- **設定画面**: ゲームデータパスの設定、出力パスの設定、マッピング戦略の設定
- **武器データ抽出**: Mutagenを使用したプラグインからの武器データ抽出（実装済み）
  - 自動的にゲームのインストールを検出
  - ロードオーダーを考慮した武器レコードの読み込み
  - WinningOverridesを使用した競合解決
  - 武器の名前、ダメージ、発射速度、デフォルト弾薬などを抽出
- **マッピング画面**: 武器と弾薬のマッピング管理、マッピング生成
- **INI生成**: RobCo Patcher用の設定ファイル生成（実装済み）
  - タイムスタンプ付きINIファイル生成
  - 手動マッピングフラグのサポート
  - ディレクトリの自動作成
- **ログ/ステータス**: リアルタイムログ表示とステータス表示

## 技術スタック

- .NET 8.0
- WPF (Windows Presentation Foundation)
- MVVM (Model-View-ViewModel) パターン
- Microsoft.Extensions.DependencyInjection (DI)
- Microsoft.Extensions.Hosting
- Mutagen.Bethesda.Fallout4 (0.51.5) - プラグイン読み込みとデータ抽出

## プロジェクト構造

```
MunitionAutoPatcher/
├── Models/                          # データモデル
│   ├── FormKey.cs
│   ├── WeaponData.cs
│   ├── WeaponMapping.cs
│   ├── StrategyConfig.cs
│   ├── AmmoCategory.cs
│   └── AmmoData.cs
├── ViewModels/                      # ビューモデル
│   ├── ViewModelBase.cs
│   ├── MainViewModel.cs
│   ├── SettingsViewModel.cs
│   ├── MapperViewModel.cs
│   ├── WeaponMappingViewModel.cs
│   └── AmmoViewModel.cs
├── Views/                           # ビュー (XAML + Code-behind)
│   ├── MainWindow.xaml
│   ├── SettingsView.xaml
│   └── MapperView.xaml
├── Services/                        # サービス層
│   ├── Interfaces/
│   │   ├── IOrchestrator.cs
│   │   ├── IWeaponsService.cs
│   │   ├── IRobCoIniGenerator.cs
│   │   ├── ILoadOrderService.cs
│   │   └── IConfigService.cs
│   └── Implementations/
│       ├── OrchestratorService.cs
│       ├── WeaponsService.cs
│       ├── RobCoIniGenerator.cs
│       ├── LoadOrderService.cs
│       └── ConfigService.cs
├── Commands/                        # コマンドハンドラ
│   ├── RelayCommand.cs
│   └── AsyncRelayCommand.cs
├── Converters/                      # 値変換
│   ├── BoolToVisibilityConverter.cs
│   └── InverseBoolConverter.cs
└── App.xaml.cs                      # アプリケーションエントリポイント + DI設定
```

## ビルド方法

### 前提条件

- .NET 8.0 SDK以降
- Windows 10/11
- Visual Studio 2022 または Visual Studio Code（推奨）

### ビルドコマンド

```bash
# ソリューションディレクトリで
dotnet restore
dotnet build
```

### 実行方法

```bash
# ソリューションディレクトリで
dotnet run --project MunitionAutoPatcher/MunitionAutoPatcher.csproj
```

または Visual Studio で F5 キーを押して実行します。

## 使用方法

1. **設定画面**:
   - ゲームデータパス（Fallout4の Data フォルダ）を設定
   - 出力INIファイルのパスを設定
   - マッピング戦略を選択
   - 「武器データ抽出を開始」ボタンをクリック

2. **マッピング画面**:
   - メニューから「マッピング」を選択
   - 「マッピング生成」ボタンをクリックして武器と弾薬のマッピングを生成
   - マッピングテーブルで確認・編集
   - 「INI生成」ボタンをクリックしてRobCo Patcher用の設定ファイルを生成

3. **ログ確認**:
   - 画面下部のログパネルで処理状況を確認

## 今後の実装予定 (TODOs)

### 次のPRで実装予定の機能

- [x] Mutagen統合: 実際のプラグイン読み込みと武器データ抽出 ✓
- [x] INI生成の実装: 実際のRobCo Patcher INIファイル生成 ✓
- [x] ロードオーダー検証: 実際のプラグインロードオーダーの読み込みと検証 ✓
- [ ] 弾薬データ抽出: プラグインからの弾薬データ抽出
- [ ] 自動マッピングロジック: 名前ベース、タイプベースの自動マッピング
- [ ] マニュアルマッピング編集: UIでの手動マッピング編集機能
- [ ] 設定の永続化: アプリケーション設定のJSON保存/読み込み
- [ ] エラーハンドリング: エラー処理とユーザーへのフィードバック改善
- [ ] プレビュー機能: INI生成前のプレビュー表示
- [ ] エクスポート/インポート: マッピングデータのエクスポート/インポート
- [ ] Mod Organizer 2 / Vortex統合: 外部モッドマネージャー対応

### 将来的な拡張機能

- ユニットテスト追加
- 多言語対応（英語など）
- マッピングルールのテンプレート機能
- バッチ処理モード
- プラグイン競合検出

## アーキテクチャ

### MVVM パターン

- **Model**: データとビジネスロジック（Models/, Services/）
- **View**: UI表示（Views/）
- **ViewModel**: ViewとModelの仲介、プレゼンテーションロジック（ViewModels/）

### 依存性注入 (DI)

App.xaml.cs で Microsoft.Extensions.DependencyInjection を使用してサービスとViewModelを登録し、DIコンテナから解決しています。

### 非同期処理

AsyncRelayCommand を使用して、長時間実行される処理（データ抽出、INI生成など）を非同期で実行し、UIのレスポンスを維持します。

## 注意事項

- **最新の実装状況**: Mutagen統合が完了し、実際のプラグイン読み込みと武器データ抽出が可能になりました。INIファイルの生成も実装されています。
- Windows専用アプリケーションです（WPF使用のため）。
- .NET 8.0以降が必要です。
- Fallout 4がインストールされている必要があります（またはゲームデータパスを手動で設定）。

### 重要: 大規模な MOD（例: Dank_ECO.esp）に関する注意

- Dank_ECO.esp のような大規模かつ多機能な MOD は、武器や弾薬、その他多数のプロパティを広範に変更します。そのため自動解析による「弾薬変更の確定判定」は誤検出やノイズを大量に生む可能性が高く、ツール側で正確に判定することが非常に難しいケースが多くあります。

- 現在の方針（推奨）:
   - デフォルトではこうした大規模 MOD を自動検出対象から除外します（false positive を防ぐための安全策）。
   - Dank_ECO.esp を含めて解析したい場合は、`config/config.json` の `excludedPlugins` 設定を編集して除外リストから削除してください（手動での確認・レビューを強く推奨します）。

- ドキュメントとログ:
   - 抽出実行時に除外されたプラグイン名とスキップしたレコード数をログに出力します。除外の影響を把握したうえで手動で解析を行ってください。

- 代替案（上級者向け）:
   - 除外にせず「審査モード」で処理を行い、検出はするが `Confirmed` フラグは付けずに詳細ダンプのみ出力する運用も可能です（将来的にオプションとして追加予定）。

### ローカル設定ファイルについて

- 本リポジトリでは、実行時のローカル設定ファイルとして `config/config.json` を使用します。
- セキュリティおよび環境差（各開発者のゲームパス等）のため、このファイルはリポジトリに含めず、各自のローカル環境に置いてください。
- リポジトリの `.gitignore` に `config/config.json` が追加済みです。既にコミット済みでリモートに存在する場合は、ローカルに残したまま `git rm --cached config/config.json` で追跡解除されています。
- 初回起動時は設定画面からゲームの Data フォルダと出力パスを指定してください。指定した値は `config/config.json` に保存されます。


## ライセンス

TBD

## 貢献

TBD