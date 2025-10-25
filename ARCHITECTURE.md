# Application UI Overview

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

## Settings View

```
┌─────────────────────────────────────────────────────────────────────┐
│ 設定                                                                  │
│                                                                       │
│ ┌─── ゲームデータパス ─────────────────────────────────────────────┐ │
│ │ [C:\Games\Fallout4\Data                    ] [参照...] │ │
│ └─────────────────────────────────────────────────────────────────┘ │
│                                                                       │
│ ┌─── 出力INIファイルパス ──────────────────────────────────────────┐ │
│ │ [C:\Games\Fallout4\Data\RobCoPatcher.ini  ] [参照...] │ │
│ └─────────────────────────────────────────────────────────────────┘ │
│                                                                       │
│ ┌─── マッピング戦略 ───────────────────────────────────────────────┐ │
│ │ ☑ 名前で自動マッピング                                           │ │
│ │ ☑ タイプで自動マッピング                                         │ │
│ └─────────────────────────────────────────────────────────────────┘ │
│                                                                       │
│ ┌─── アクション ───────────────────────────────────────────────────┐ │
│ │ [     武器データ抽出を開始     ]                                 │ │
│ │ [████████████████████████████] (Processing indicator)            │ │
│ └─────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────┘
```

## Mapper View

```
┌─────────────────────────────────────────────────────────────────────┐
│ 武器マッピング                                                        │
│                                                                       │
│ [マッピング生成] [INI生成] [████████████████] (Processing)           │
│                                                                       │
│ ┌─────────────────────────────────────────────────────────────────┐ │
│ │武器名     │武器FormKey      │弾薬名         │弾薬FormKey│戦略│手動│ │
│ ├─────────┼────────────────┼──────────────┼──────────┼───┼───┤ │
│ │10mmピスト│Fallout4.esm:   │自動マッピング │N/A        │Def │[ ]│ │
│ │ル        │001234           │未実装         │           │ault│   │ │
│ ├─────────┼────────────────┼──────────────┼──────────┼───┼───┤ │
│ │コンバット│Fallout4.esm:   │自動マッピング │N/A        │Def │[ ]│ │
│ │ライフル  │005678           │未実装         │           │ault│   │ │
│ └─────────┴────────────────┴──────────────┴──────────┴───┴───┘ │
│                                                                       │
└─────────────────────────────────────────────────────────────────────┘
```

## Key Features

### UI Elements with Japanese Labels
- Menu items: ファイル, 設定, マッピング, 終了, ヘルプ, バージョン情報
- Headers: ゲームデータパス, 出力INIファイルパス, マッピング戦略, アクション
- Buttons: 参照..., 武器データ抽出を開始, マッピング生成, INI生成
- Checkboxes: 名前で自動マッピング, タイプで自動マッピング
- Table headers: 武器名, 武器FormKey, 弾薬名, 弾薬FormKey, 戦略, 手動

### Functional Flow
1. **Settings Screen** → Set game path, output path, mapping strategy
2. **Start Extraction** → Trigger stub weapon extraction (logs progress)
3. **Mapper Screen** → View extracted weapons
4. **Generate Mappings** → Create weapon-to-ammo mappings
5. **Generate INI** → Create RobCo Patcher configuration file

### Status & Logging
- Real-time status updates in status bar
- Timestamped log messages in bottom panel
- Progress indicators during async operations

### MVVM Architecture
- Clean separation: Views (XAML), ViewModels (binding logic), Services (business logic)
- Dependency injection for all services and ViewModels
- Command pattern for user actions (RelayCommand, AsyncRelayCommand)
- INotifyPropertyChanged for data binding

### Next Steps (TODOs)
All current functionality is stubbed. Future PRs will implement:
- Actual Mutagen integration for plugin parsing
- Real auto-mapping algorithms
- Complete INI generation
- Manual mapping editing
- Configuration persistence

### 注意事項（表示言語）
本アプリケーションでは、Mutagen が提供する翻訳文字列（ITranslatedString）から表示用文字列を取得する際に、優先言語の選択が重要です。

現在の実装では、Mutagen の翻訳文字列を日本語 → 英語 → TargetLanguage の順で選択するロジックを採用しています。このため、日本語環境では以下のように正しい日本語が取得でき、UI 上で文字化け（mojibake）は発生していません（例: "5.56口径弾", "フュージョン・セル", "ショットガンシェル" 等）。

ただし、英語など別の言語環境で他者が検証を行う場合、TargetLanguage の扱いや環境依存のコードページにより表示に問題が出る可能性があります。従って、UI 側で表示の優先言語（例: 日本語、英語、または EditorID）を明示的に設定できるようにする必要があります。

具体的には以下を注意事項として記載します:
- UI に「優先表示言語」を追加し、ユーザーが日本語／英語／EditorID の順序を切り替えられるようにすること。
- デフォルトは環境に依存せず安全な設定（例: 日本語環境では日本語優先、英語環境では英語優先）とし、ユーザーが任意に上書き可能にすること。
- さらに、翻訳エントリが存在しない場合は EditorID をフォールバック表示することで、文字化けや不正なレンダリングの回避を容易にすること。

この対応により、異なるロケール環境での表示差異による問題を未然に防ぎ、外部の検証者が使用する環境でも安定して正しい文字列を表示できるようになります。
