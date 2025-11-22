以下は、このリポジトリで「型安全な MutagenV51Detector への移行」を進めるための、かなり具体的な実装計画です。  
段階・担当クラス・確認ポイントまで分解しています。

---

## フェーズ0：前提の整理とスコープ確定

### 0-1. ゴールの明文化

リポジトリ内の設計ドキュメント（`ADAPTER_PATTERN_INVESTIGATION.md` や新しい `plan` 系ファイル）に、次を明記します。

- Fallout 4 専用ツールとして進める（他ゲームはスコープ外）
- Mutagen v0.51.5 専用の最適化パスを持つ
- ただし将来の Mutagen バージョン変化や未知構造に備え、
  - `IAmmunitionChangeDetector` は引き続き object ベースを維持
  - `ReflectionFallbackDetector` は存続させる（最終セーフティネット）

これは後続の「遠慮ない typed 依存」を正当化するためのドキュメント上の根拠になります。

---

## フェーズ1：Mutagen の FO4 OMOD / Weapon 構造をきちんと把握する

### 1-1. Mutagen の定義を外部ツールで確認

プロジェクト内の DLL からは型定義が見えないので、リポジトリに書いてある方針どおり MCPツール（`@github`, `@deepwiki` 的なもの）を使い、

- `Mutagen.Bethesda.Fallout4` の ObjectModification 関連定義
  - `ObjectModification.xml` / `AObjectModProperty_Generated.cs` / `ObjectModItem` など
- `IObjectModificationGetter` の `Properties` の型と各プロパティの構造
- `Weapon.Property` enum（あるいは同等の Property/FunctionType 定義）

を項目として洗い出します。

ここで必要なのは：

- 「OMOD の Properties から ammo / projectile をどう判定するのが公式な流儀か」
- 「Weapon の ammo はどのプロパティとして表現されているか」

をソースコードレベルで正確に把握することです。

### 1-2. 結果の要約をプロジェクト内に残す

取得した情報を、簡単なメモとしてプロジェクト内に置くと後続・将来の保守が楽です。

例：

````markdown name=docs/Mutagen_FO4_AmmoNotes.md
- IObjectModificationGetter
  - Properties: IReadOnlyList<...>
    - 各エントリに Property, FunctionType, Value などが定義されている
    - Ammo 変更は Property = Weapon.Property.Ammo のときに発生
    - Projectile 変更は Property = Weapon.Property.Projectile のときに発生
  - ...

- IWeaponGetter
  - Data.Ammo: IFormLinkGetter<IAmmunitionGetter> (null or IsNull で判定)
  - ...
````

※ 実際の型名やプロパティ名は、MCPツールで取得した正しいものに差し替え。

---

## フェーズ2：MutagenV51Detector の interface レベルの方針決め

### 2-1. IAmmunitionChangeDetector は現状維持

`IAmmunitionChangeDetector` は現状のまま：

```csharp
bool DoesOmodChangeAmmo(object omod, object? originalAmmoLink, out object? newAmmoLink);
string Name { get; }
```

- 外部・将来拡張用の抽象層として存続
- object ベースシグネチャは変更しない

### 2-2. （オプション）内部用の typed インターフェースを追加

将来の拡張を見越して、FO4 専用の typed インターフェースを追加する案です。

```csharp name=MunitionAutoPatcher/Services/Interfaces/ITypedAmmunitionChangeDetector.cs url=https://github.com/shkond/Munition_AutoPatcher_vC/blob/main/MunitionAutoPatcher/Services/Interfaces/ITypedAmmunitionChangeDetector.cs
using Mutagen.Bethesda.Fallout4;

namespace MunitionAutoPatcher.Services.Interfaces;

public interface ITypedAmmunitionChangeDetector : IAmmunitionChangeDetector
{
    bool DoesOmodChangeAmmo(
        IObjectModificationGetter omod,
        IAmmunitionGetter? originalAmmo,
        out IAmmunitionGetter? newAmmo);
}
```

- まずは internal でもよいですし、最初から public でも構いません。
- `MutagenV51Detector` はこのインターフェースも実装し、実ロジックは typed メソッド側に集約していく設計です。

---

## フェーズ3：MutagenV51Detector の実装リファクタリング

ここがメインの作業です。

### 3-1. 現行実装をバックアップしつつ、新ロジックの骨組みを作る

ファイル [MutagenV51Detector.cs](https://github.com/shkond/Munition_AutoPatcher_vC/blob/main/MunitionAutoPatcher/Services/Implementations/MutagenV51Detector.cs) を以下のように段階的に変えていきます。

1. 先頭に Mutagen 名前空間の using を追加：

```csharp
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins; // FormKey 等を使うなら
```

2. `DoesOmodChangeAmmo` を「typed パス + fallback」の2段構造にする。

擬似コードの差分イメージ：

```csharp name=MunitionAutoPatcher/Services/Implementations/MutagenV51Detector.cs url=https://github.com/shkond/Munition_AutoPatcher_vC/blob/main/MunitionAutoPatcher/Services/Implementations/MutagenV51Detector.cs
public class MutagenV51Detector : IAmmunitionChangeDetector /*, ITypedAmmunitionChangeDetector (導入するなら) */
{
    // 既存フィールド・コンストラクタはそのまま

    public string Name => "MutagenV51Detector";

    public bool DoesOmodChangeAmmo(object omod, object? originalAmmoLink, out object? newAmmoLink)
    {
        newAmmoLink = null;
        if (omod == null) return false;

        try
        {
            // 1. Typed path: IObjectModificationGetter にキャストできる場合は型安全ロジックを使う
            if (omod is IObjectModificationGetter omodGetter)
            {
                IAmmunitionGetter? originalAmmoTyped = TryExtractAmmoFromObject(originalAmmoLink);
                IAmmunitionGetter? newAmmoTyped;
                if (TryDetectAmmoChangeTyped(omodGetter, originalAmmoTyped, out newAmmoTyped))
                {
                    newAmmoLink = newAmmoTyped ?? (object?)null;
                    return true;
                }

                // 型安全ロジックで「弾薬変更なし」と判断された場合は false
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MutagenV51Detector: typed detection path failed, falling back to reflection detector");
        }

        // 2. Typed path が使えない or 例外発生時のみ reflection fallback
        try
        {
            return _fallback.DoesOmodChangeAmmo(omod, originalAmmoLink, out newAmmoLink);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MutagenV51Detector: reflection fallback detector threw an exception");
            newAmmoLink = null;
            return false;
        }
    }

    // ここから下に、完全 typed なヘルパー群を追加していく
}
```

※ 実際の signature に合わせて `TryExtractAmmoFromObject` や `TryDetectAmmoChangeTyped` は private に定義します。

### 3-2. originalAmmoLink の typed 変換ヘルパー

`originalAmmoLink` は object ですが、MutagenV51 の世界では、

- `IAmmunitionGetter`
- `IFormLinkGetter<IAmmunitionGetter>`

あたりであることが多い想定です。これを FormKey 比較できるように helper を定義します。

```csharp
private static IAmmunitionGetter? TryExtractAmmoFromObject(object? value)
{
    if (value is IAmmunitionGetter ammo)
        return ammo;

    // もし IFormLinkGetter<IAmmunitionGetter> だった場合に解決できるなら、ここで解決する
    // ただし LinkCache が必要になるので、Detector 単体では解決しない方針もあり。
    // その場合は、FormKey ベース比較だけに留める。

    return null;
}

private static bool AreSameAmmo(object? candidate, IAmmunitionGetter? original)
{
    if (candidate is IAmmunitionGetter candAmmo && original != null)
    {
        // FormKey 同士を比較
        return candAmmo.FormKey.Equals(original.FormKey);
    }

    // 必要なら MutagenReflectionHelpers を利用して「どちらも FormKey を持っているなら比較」などを追加
    return false;
}
```

ここは「LinkCache をここまで引き入れるか？」で設計が分かれます。  
現状の Detector は LinkCache を持っていないので、**FormKey レベル比較だけに留める**のが素直です。

### 3-3. OMOD の typed 判定ロジック

フェーズ1で調べた FO4 型情報をもとに、`IObjectModificationGetter.Properties` を型安全に走査します。

擬似コード：

```csharp
private static bool TryDetectAmmoChangeTyped(
    IObjectModificationGetter omod,
    IAmmunitionGetter? originalAmmo,
    out IAmmunitionGetter? newAmmo)
{
    newAmmo = null;

    var properties = omod.Properties; // 実際のプロパティ名は Mutagen の定義に合わせる
    if (properties == null || properties.Count == 0)
        return false;

    foreach (var entry in properties)
    {
        if (!IsAmmoChangingProperty(entry))
            continue;

        var candidateAmmo = ExtractAmmoFromProperty(entry);
        if (candidateAmmo == null)
            continue;

        if (originalAmmo != null && AreSameAmmo(candidateAmmo, originalAmmo))
        {
            // 元の弾薬と同じ → 変更なしとしてスキップ
            continue;
        }

        newAmmo = candidateAmmo;
        return true;
    }

    return false;
}
```

`IsAmmoChangingProperty` / `ExtractAmmoFromProperty` は、Mutagen の FO4 定義に合わせて書きます：

```csharp
private static bool IsAmmoChangingProperty(ObjectModPropertyEntry entry)
{
    // 実際の型名に差し替え
    // 例: entry.Property == Weapon.Property.Ammo || entry.Property == Weapon.Property.Projectile
}

private static IAmmunitionGetter? ExtractAmmoFromProperty(ObjectModPropertyEntry entry)
{
    // entry.Value または entry.FormLink から IAmmunitionGetter (または FormKey) を取り出す
    // LinkCache が使えない場合は「FormKey を持っている object」として newAmmoLink に詰めてもよい
}
```

※ ここは MCPツールで FO4 の実際の型定義を見て、正しい型名・プロパティ名で埋める部分です。

### 3-4. 古いヒューリスティックの削除 or 退避

- `IsAmmoLikeName` / `IsLikelyFormLinkProperty` を使った名前ベースの探索は、
  - 新 typed 実装が安定してから削除する
  - あるいは、しばらくは private メソッドとして残しておき、必要なら debug 用に使う

今の方向性だと「完全な型安全化」を目標としているので、最終的には削除して構いません。

---

## フェーズ4：テスト計画

### 4-1. 単体テストの追加

新しい typed ロジック用に、テストを追加します。

- テストプロジェクト：`tests/AutoTests` または新規 `tests/DetectorTests`
- テストケース例：
  1. **Ammo 変更 OMOD**
     - Properties に「Ammo を別の弾薬にセットする」定義を持つ OMOD を構築
     - `DoesOmodChangeAmmo` が true を返し、`newAmmoLink` が期待通りの ammo を指すことを確認
  2. **Ammo 非変更 OMOD**
     - OMOD が他のプロパティ（Damage, Recoil など）のみを変更する場合
     - `DoesOmodChangeAmmo` が false を返すことを確認
  3. **元と同じ Ammo に設定する OMOD**
     - originalAmmo と同じ ammo にセットする OMOD
     - `DoesOmodChangeAmmo` が false を返すことを確認（「変更なし」とみなす仕様）
  4. **非 OMOD 型**
     - `omod` に全く関係ない object を渡し、fallback が呼ばれる or false になることを確認

テスト実装にあたっては、実際の Mutagen 型を new するか、または Moq を使ってインターフェースをモックする形のどちらかを選びます。  
`IObjectModificationGetter` が generated class でインスタンス化しづらい場合は、モックの方が現実的です。

### 4-2. 回帰テスト

- 既存の `ReflectionFallbackDetector` テストがあれば、そのまま動くことを確認
- 可能であれば、サンプル FO4 プラグインを使った統合テストで「vC 全体の動作」が変わっていないかを確認

---

## フェーズ5：ドキュメントとリファクタ後の整理

### 5-1. 設計ドキュメント更新

- `ADAPTER_PATTERN_INVESTIGATION.md` か新しい `DECISIONS_xxx.md` に、
  - `MutagenV51Detector` は FO4 + Mutagen v0.51 の型安全ロジックを持ち、
  - 失敗時のみ `ReflectionFallbackDetector` に委譲する
  - `IAmmunitionChangeDetector` は依然として object ベースだが、実装は typed を優先している

という決定を追記します。

### 5-2. 使われなくなったヘルパーの整理

- `MutagenReflectionHelpers` で ammo 判定専用に使っていたヘルパーがあれば、使用箇所を確認し、不要なら削除 or `Obsolete` 属性を付与します。
- `MutagenV51Detector` 内の不要メソッド（名前判定ベース）が残っていれば削除。

---

## このあとやるべき具体的な次の一手

AI がこの計画に沿ってコードを書き進める場合、次の順序がおすすめです。

1. MCP ツールで FO4 の `IObjectModificationGetter.Properties` 構造と `Weapon.Property` などを確認し、`docs/Mutagen_FO4_AmmoNotes.md` に簡単にまとめる。
2. `MutagenV51Detector` に `using Mutagen.Bethesda.Fallout4;` を追加し、  
   まずは「typed path + fallback」の二段構造だけを導入する（中身はまだ空でもよい）。
3. `TryDetectAmmoChangeTyped` の本体を、Mutagen の実際の型情報に合わせて実装する。
4. 単体テストを2〜3ケース書き、green になるまで直す。
5. 旧ヒューリスティック（`IsAmmoLikeName` 等）を削除 or コメントアウトし、再ビルド・再テスト。

ここまで進めば、「MutagenV51Detector の完全な型安全化」のコア部分は完了です。  
必要であれば、この計画をもとに実際の差分も提案できます。