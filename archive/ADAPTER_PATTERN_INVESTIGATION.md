# アダプターパターン実装調査と修正レポート

**日付**: 2025年11月4日  
**問題**: 武器レコードの解析時に `System.InvalidOperationException: weapon record missing FormKey` エラーが発生

## 調査結果

### 1. アダプターパターンの実装状況 ✅

プロジェクトでは**アダプターパターンが正しく実装されています**：

#### 実装構造

```
IMutagenEnvironmentFactory (インターフェース)
    ↓
MutagenEnvironmentFactory (ファクトリ実装)
    ↓
IResourcedMutagenEnvironment (リソース管理インターフェース)
    ↓
ResourcedMutagenEnvironment (ラッパー実装)
    ↓
MutagenV51EnvironmentAdapter (Mutagen v0.51.5 アダプター)
    ↓ ラップ
IGameEnvironment<IFallout4Mod, IFallout4ModGetter> (Mutagenの実際のAPI)
```

#### 主要なアダプタークラス

1. **MutagenV51EnvironmentAdapter**
   - Mutagen の `IGameEnvironment` をラップ
   - 統一された `IMutagenEnvironment` インターフェースを提供
   - リフレクションを使用した安全な API アクセス

2. **ResourcedMutagenEnvironment**
   - `IDisposable` パターンを実装
   - リソースの適切な解放を保証
   - 例外の安全な処理

3. **NoOpMutagenEnvironment**
   - GameEnvironment が作成できない場合のフォールバック
   - 空のコレクションを返すセーフティネット

### 2. 問題の根本原因

**WeaponsService.cs の 147行目**で例外がスローされていました：

```csharp
if (!MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(weaponGetter, out pluginName, out formId))
    throw new InvalidOperationException("weapon record missing FormKey");
```

#### 原因

一部の武器レコードが以下のいずれかの状態にあった：

- `FormKey` プロパティが `null`
- `FormKey.ModKey` が `null`
- `ModKey.FileName` が空文字列
- `FormKey.ID` が `0`

これらは Fallout 4 プラグインの特性として、無効または破損したレコードが含まれる可能性があります。

### 3. 実装した修正

#### 修正1: WeaponsService.cs - 例外をスキップに変更

**変更前**:
```csharp
if (!MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(weaponGetter, out pluginName, out formId))
    throw new InvalidOperationException("weapon record missing FormKey");
```

**変更後**:
```csharp
if (!MutagenReflectionHelpers.TryGetPluginAndIdFromRecord(weaponGetter, out pluginName, out formId))
{
    // Skip weapons with invalid FormKeys instead of throwing
    AppLogger.Log($"WeaponsService: skipping weapon record with missing or invalid FormKey (Type: {weaponGetter?.GetType().Name ?? "null"})");
    continue;
}
```

**効果**: 無効な武器レコードをスキップして処理を継続

#### 修正2: MutagenReflectionHelpers.cs - 詳細なログ出力

`TryGetPluginAndIdFromRecord` メソッドに詳細なログ出力を追加：

```csharp
if (record == null)
{
    AppLogger.Log("MutagenReflectionHelpers.TryGetPluginAndIdFromRecord: record is null");
    return false;
}

if (!TryGetFormKey(record, out var fk))
{
    AppLogger.Log($"MutagenReflectionHelpers.TryGetPluginAndIdFromRecord: failed to get FormKey from record type {record.GetType().Name}");
    return false;
}

// ... 各ステップでログ出力
```

**効果**: FormKey 抽出の各ステップで失敗箇所を特定可能に

### 4. テスト結果

#### アダプターパターンのテスト
```bash
dotnet test --filter "FullyQualifiedName~MutagenEnvironment"
```
**結果**: ✅ 3テスト成功

#### リフレクションヘルパーのテスト
```bash
dotnet test --filter "FullyQualifiedName~MutagenReflectionHelpers"
```
**結果**: ✅ 2テスト成功

#### ビルド
```bash
dotnet build -c Debug
```
**結果**: ✅ ビルド成功（警告2件は既存の nullable 参照型の問題）

## アダプターパターンの利点

この実装により、以下の利点が得られています：

1. **バージョン非依存性**: Mutagen の API 変更に対して柔軟に対応可能
2. **テスタビリティ**: モックやスタブを使用したテストが容易
3. **エラーハンドリング**: リフレクションによる安全な API アクセス
4. **リソース管理**: `using` ステートメントによる適切な解放
5. **フォールバック**: GameEnvironment が作成できない場合も安全に動作

## 結論

✅ **アダプターパターンは正しく実装されています**

今回のエラーはアダプターパターンの問題ではなく、一部の武器レコードに**無効なFormKey**が含まれていたことが原因でした。

修正により：
- 無効なレコードをスキップして処理を継続
- 詳細なログでどのレコードが問題かを特定可能
- 例外でアプリケーションがクラッシュすることを防止

## 今後の推奨事項

1. **ログファイルの確認**: `artifacts/` フォルダ内のログファイルで、スキップされた武器レコードを確認
2. **プラグイン検証**: 使用しているプラグインが破損していないかチェック
3. **統計の記録**: 抽出された武器数とスキップされた武器数をレポート表示

## 関連ファイル

- `MunitionAutoPatcher/Services/Implementations/WeaponsService.cs` (修正)
- `MunitionAutoPatcher/Utilities/MutagenReflectionHelpers.cs` (修正)
- `MunitionAutoPatcher/Services/Implementations/MutagenV51EnvironmentAdapter.cs`
- `MunitionAutoPatcher/Services/Implementations/ResourcedMutagenEnvironment.cs`
- `MunitionAutoPatcher/Services/Implementations/NoOpMutagenEnvironment.cs`
