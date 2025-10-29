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
│   ├── AmmoCategory.cs
│   ├── AmmoData.cs
│   ├── FormKey.cs
│   ├── OmodCandidate.cs
│   ├── StrategyConfig.cs
│   ├── WeaponData.cs
│   └── WeaponMapping.cs
├── ViewModels/                      # ビューモデル
│   ├── AmmoViewModel.cs
│   ├── MainViewModel.cs
│   ├── MapperViewModel.cs
│   ├── SettingsViewModel.cs
│   ├── ViewModelBase.cs
│   └── WeaponMappingViewModel.cs
├── Views/                           # ビュー (XAML + Code-behind)
│   ├── InputDialog.xaml
│   ├── MainWindow.xaml
│   ├── MapperView.xaml
│   └── SettingsView.xaml
├── Services/                        # サービス層
│   ├── Helpers/
│   │   ├── CandidateEnumerator.cs
│   │   ├── DiagnosticWriter.cs
│   │   └── ReverseMapBuilder.cs
│   ├── Interfaces/
│   │   ├── IAmmunitionChangeDetector.cs
│   │   ├── IConfigService.cs
│   │   ├── ILoadOrderService.cs
│   │   ├── IOrchestrator.cs
│   │   ├── IRobCoIniGenerator.cs
│   │   ├── IWeaponOmodExtractor.cs
│   │   └── IWeaponsService.cs
│   └── Implementations/
│       ├── ConfigService.cs
│       ├── DetectorFactory.cs
│       ├── LinkCacheHelper.cs
│       ├── LoadOrderService.cs
│       ├── MutagenV51Detector.cs
│       ├── OrchestratorService.cs
│       ├── ReflectionFallbackDetector.cs
│       ├── RobCoIniGenerator.cs
│       ├── WeaponOmodExtractor.cs
│       └── WeaponsService.cs
├── Commands/                        # コマンドハンドラ
│   ├── AsyncRelayCommand.cs
│   └── RelayCommand.cs
├── Converters/                      # 値変換
│   ├── BoolToVisibilityConverter.cs
│   └── InverseBoolConverter.cs
├── Utilities/                       # ユーティリティ
│   └── RepoUtils.cs
├── App.xaml.cs                      # アプリケーションエントリポイント + DI設定
├── AppLogger.cs                     # ロギング
└── DebugConsole.cs                  # デバッグコンソール
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

## CI / Continuous Integration

このリポジトリを CI パイプライン（例: GitHub Actions）で扱う際の推奨コマンドと注意点を示します。

- 推奨コマンド（ビルド / テスト / 公開）:

```bash
# 依存復元
dotnet restore

# ソリューションを Release ビルド
dotnet build MunitionAutoPatcher.sln -c Release

# テスト（プロジェクト単位、--no-build は事前にビルド済みの場合に有用）
dotnet test tests/AutoTests/AutoTests.csproj -c Release --no-build --verbosity normal

# 形式整形（任意）
dotnet format

# 発行（パッケージ化が必要な場合）
dotnet publish MunitionAutoPatcher/MunitionAutoPatcher.csproj -c Release -r win-x64 --self-contained false
```

注意: 本プロジェクトは WPF (.NET on Windows) を使っているため、GUI に依存するテストや実行は Linux ランナー上で失敗する可能性があります。フルテストを CI 上で実行する場合は `runs-on: windows-latest` を使うことを推奨します。

簡単な GitHub Actions の例:

```yaml
name: CI
on: [push, pull_request]
jobs:
   build:
      runs-on: windows-latest
      steps:
         - uses: actions/checkout@v4
         - name: Setup .NET
            uses: actions/setup-dotnet@v3
            with:
               dotnet-version: '8.0.x'
         - name: Restore
            run: dotnet restore
         - name: Build
            run: dotnet build MunitionAutoPatcher.sln -c Release --no-restore
         - name: Test
            run: dotnet test tests/AutoTests/AutoTests.csproj -c Release --no-build --verbosity normal
```

ポイント:
- WPF やプラットフォーム固有ライブラリ（Mutagen 等）を使うため、Windows ランナーでのテスト実行が最も互換性が高いです。
- ヘッドレス環境で GUI に依存するテストを実行する必要がある場合は、該当テストをカテゴリーやタグで分離し、CI ではスキップするか専用ジョブを用意してください。

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

### 次のPRで実装予定の機能（状態更新）

以下はコードベースの現状に合わせて状態を更新しています。

- [x] Mutagen統合: 実際のプラグイン読み込みと武器データ抽出 ✓ (実装済み: `WeaponsService` が Mutagen の `GameEnvironment` を使用しています)
- [x] INI生成の実装: 実際のRobCo Patcher INIファイル生成 ✓ (実装済み: `RobCoIniGenerator`)
- [x] ロードオーダー検証: 実際のプラグインロードオーダーの読み込みと検証 ✓
- [x] 弾薬データ抽出: プラグインからの弾薬データ抽出 ✓ (部分実装/実用的: `WeaponsService` が `_ammo` を構築し `GetAllAmmo()` で利用可能)
- [~] 自動マッピングロジック: 名前ベース、タイプベースの自動マッピング (部分実装 — UI 側に簡易ロジックと Orchestrator のスタブが存在。高度なルールは要改善)
- [~] マニュアルマッピング編集: UIでの手動マッピング編集機能 (実装済みの UI があるが UX/機能強化の余地あり)
- [x] 設定の永続化: アプリケーション設定のJSON保存/読み込み ✓ (実装済み: `ConfigService` が設定の読み書きを行います)
- [~] エラーハンドリング: エラー処理とユーザーへのフィードバック改善 (多くの空catchは AppLogger に置き換えられましたが、さらなる改善余地あり)
- [ ] プレビュー機能: INI生成前のプレビュー表示
- [ ] エクスポート/インポート: マッピングデータのエクスポート/インポート
- [ ] Mod Organizer 2 / Vortex統合: 外部モッドマネージャー対応

注: チェック済み (✓) は実装が確認できた項目、(~) は部分実装や改善余地のある項目です。README の TODO はドキュメントの粒度が実装状態と必ずしも一致していなかったため、現状に合わせて更新しました。

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

## ローカル設定ファイルについて

- 本リポジトリでは実行時のローカル設定ファイルとして `config/config.json` を使用しています。
- 注意: 現状リポジトリに `config/config.json` が含まれている状態です（環境固有のパスが記載されている可能性があります）。セキュリティと環境依存性を避けるため、通常はこのファイルをリポジトリで追跡しない運用を推奨します。
- 既に `.gitignore` に `config/config.json` が記載されているため、追跡済みのファイルは `git rm --cached config/config.json` を実行してインデックスから外すことを推奨します（ファイルはローカルに残ります）。
- なお、`ConfigService` はリポジトリローカルの `config/` 配下を優先して読み書きする実装になっているため、チームで共有するデフォルト値が必要な場合は `config/config.sample.json` のようなサンプルを用意して README に記載する運用が望ましいです。


## ライセンス

TBD

## 貢献

TBD