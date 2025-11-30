# ESP生成バグ デバッグ記録

## 現象

**報告:** 「GUI上でESP作成を行うと、ESPは作成されるものの、レコードがない」

- ESP ファイルは正常に出力される
- しかし `WeaponMappings.Count == 0` のため、実際のレコードが生成されない

---

## 根本原因の連鎖

このバグは**複数の問題が連鎖**して発生していました。

### 1. TestEnvironmentBuilder のモッド重複作成

**場所:** `tests/IntegrationTests/TestInfrastructure/TestEnvironmentBuilder.cs`

**問題:** `WithPlugin()` メソッドが呼び出されるたびに新しい `SkyrimMod` を作成していた。

```csharp
// Before (問題のあるコード)
public TestEnvironmentBuilder WithPlugin(ModKey modKey, Action<SkyrimMod> configurePlugin)
{
    var mod = new SkyrimMod(modKey, SkyrimRelease.SkyrimSE);  // 毎回新規作成
    configurePlugin(mod);
    _mods[modKey] = mod;
    return this;
}
```

**影響:** 同じ ModKey に対して異なる参照が返され、テスト内でのレコード解決が不整合になる。

**修正:**
```csharp
// After (修正後)
public TestEnvironmentBuilder WithPlugin(ModKey modKey, Action<SkyrimMod> configurePlugin)
{
    if (!_mods.TryGetValue(modKey, out var mod))
    {
        mod = new SkyrimMod(modKey, SkyrimRelease.SkyrimSE);
        _mods[modKey] = mod;
    }
    configurePlugin(mod);
    return this;
}
```

---

### 2. MutagenV51EnvironmentAdapter の LinkCache プロパティ不足

**場所:** `MunitionAutoPatcher/Services/Implementations/MutagenV51EnvironmentAdapter.cs`

**問題:** 
- `InnerGameEnvironment` プロパティが存在しなかった
- `GetLinkCache()` メソッドがインメモリ LinkCache に対応していなかった

**影響:** 仮想環境テストで LinkCache の解決が常に失敗していた。

**修正:**
```csharp
// 追加されたプロパティとフィールド
private readonly ILinkCache? _inMemoryLinkCache;
public object? InnerGameEnvironment { get; }

// コンストラクタにインメモリ LinkCache パラメータを追加
public MutagenV51EnvironmentAdapter(
    IFallout4GameEnvironmentGetter environment,
    ILinkCache? inMemoryLinkCache = null)
{
    _environment = environment;
    _inMemoryLinkCache = inMemoryLinkCache;
    InnerGameEnvironment = environment;
}

// EffectiveLinkCache プロパティでインメモリを優先
private ILinkCache EffectiveLinkCache => 
    _inMemoryLinkCache ?? _environment.LinkCache;
```

---

### 3. WeaponDataExtractor の CandidateFormKey 誤設定

**場所:** `MunitionAutoPatcher/Services/Implementations/WeaponDataExtractor.cs`

**問題:** `CandidateFormKey` を **CreatedObject (Weapon)** の FormKey に設定していた。

```csharp
// Before (問題のあるコード)
if (TryGetPluginAndIdFromRecord(createdObject, out var plugin, out var formId))
{
    candidate.CandidateFormKey = new Models.FormKey(plugin, formId);
}
```

**正しい動作:** COBJ (ConstructibleObject) レコード自体の FormKey を使用すべき。
後続の `AttachPointConfirmer` は COBJ を解決しようとするため、Weapon の FormKey では解決に失敗する。

**修正:**
```csharp
// After (修正後)
if (TryGetPluginAndIdFromRecord(cobj, out var plugin, out var formId))
{
    candidate.CandidateFormKey = new Models.FormKey(plugin, formId);
}
```

---

### 4. FormKeyNormalizer の拡張子二重付加 (★ 最終的な根本原因)

**場所:** `MunitionAutoPatcher/Services/Helpers/FormKeyNormalizer.cs`

**問題:** `ModKey` コンストラクタの誤った使用法により、拡張子が二重に追加されていた。

```csharp
// Before (問題のあるコード)
var modKey = new ModKey(fileName, modType);
// 結果: "TestMod.esp" + ModType.Plugin → "TestMod.esp.esp"
```

**Mutagen API の挙動:**
- `new ModKey(name, modType)`: `name` に拡張子がない前提。ModType に基づいて拡張子を追加する
- `ModKey.FromNameAndExtension(fileName)`: ファイル名から拡張子をパースする

**影響:** 
- 入力: `TestMod.esp:00000802`
- 変換後: `TestMod.esp.esp:00000802`
- LinkCache での解決が失敗 → `rootNull=1` → WeaponMappings が空

**修正:**
```csharp
// After (修正後)
var modKey = ModKey.FromNameAndExtension(fileName);
// 結果: "TestMod.esp" → "TestMod.esp" (正しく解析)
```

---

## 診断に有効だった手法

### 1. 診断用 E2E テストの作成

```csharp
[Fact]
public async Task DiagnoseConfirmationContext_LogsResolverAndLinkCacheStatus()
```

- ConfirmationContext の各値をログ出力
- `MutagenFormKey` の実際の値を確認 → 二重拡張子を発見

### 2. Information レベルのトレースログ追加

`AttachPointConfirmer` に解決パスのトレースログを追加:

```csharp
_logger.LogInformation("ResolveOmod: MutagenFormKey={MutagenFormKey}", mutagenFormKey);
```

### 3. InMemoryLinkCache 単体テスト

COBJ 解決が可能かどうかを独立して検証:

```csharp
[Fact]
public void InMemoryLinkCache_CanResolve_CobjByFormKey()
```

---

## 学んだ教訓

### Mutagen API の ModKey 生成パターン

| パターン | 入力 | 結果 |
|---------|------|------|
| `new ModKey("TestMod", ModType.Plugin)` | 拡張子なし | `TestMod.esp` ✓ |
| `new ModKey("TestMod.esp", ModType.Plugin)` | 拡張子あり | `TestMod.esp.esp` ✗ |
| `ModKey.FromNameAndExtension("TestMod.esp")` | 拡張子あり | `TestMod.esp` ✓ |

### 複合バグの診断アプローチ

1. **E2E テストで全体像を把握** - どのステップで失敗しているか特定
2. **単体テストで個々のコンポーネントを検証** - 独立して動作確認
3. **診断ログで実行時の値を追跡** - 期待値との差異を発見
4. **API ドキュメント/ソースコードで正しい使用法を確認** - Mutagen MCP RAG を活用

---

## 修正されたファイル一覧

| ファイル | 変更内容 |
|---------|---------|
| `FormKeyNormalizer.cs` | `ModKey.FromNameAndExtension()` を使用 |
| `WeaponDataExtractor.cs` | CandidateFormKey を COBJ の FormKey に変更 |
| `MutagenV51EnvironmentAdapter.cs` | InMemoryLinkCache 対応、InnerGameEnvironment 追加 |
| `TestEnvironmentBuilder.cs` | モッドの重複作成防止、BuildInMemoryLinkCache() 追加 |
| `ViewModelHarness.cs` | InMemoryLinkCache を Adapter に渡す |
| `AttachPointConfirmer.cs` | 診断ログ追加 |

---

## 最終テスト結果

| テストプロジェクト | 結果 |
|---|---|
| WeaponDataExtractor.Tests | ✅ 4/4 成功 |
| ConfirmerTests | ✅ 20/20 成功 |
| LinkCacheHelperTests | ✅ 91/91 成功 |
| IntegrationTests | ✅ 78/78 成功 |

**合計: 193件のテストすべて成功**
