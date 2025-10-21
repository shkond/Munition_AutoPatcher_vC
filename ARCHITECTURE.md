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
