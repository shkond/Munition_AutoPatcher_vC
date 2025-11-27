# Mutagen Fallout4 OMOD/Weapon Ammo API 仕様

このドキュメントは、Mutagen.Bethesda.Fallout4 v0.51 における弾薬関連 API の型情報を記載しています。
GitHub MCP で確認した Mutagen-Modding/Mutagen リポジトリの情報に基づきます。

## 確認日時
2025-11-22

## IObjectModificationGetter (OMOD)

### 基本構造
- **名前空間**: `Mutagen.Bethesda.Fallout4`
- **型**: Interface (getter)
- **対応ファイル**: 
  - `ObjectModification.cs` (手書き部分)
  - `ObjectModification_Generated.cs` (生成部分)

### 重要なプロパティ

#### Properties
- **型**: `ExtendedList<AObjectModProperty>`
- **説明**: OMOD が変更するプロパティのリスト
- **各要素**: `AObjectModProperty` 型（抽象基底クラス）

## AObjectModProperty

### 基本構造
- **名前空間**: `Mutagen.Bethesda.Fallout4`
- **型**: Abstract class (`AObjectModProperty<T>` でジェネリック、T は `ObjectModProperty` enum 型)
- **対応ファイル**: `AObjectModProperty_Generated.cs`
- **具象型**: Mutagen は型ごとに具象クラスを生成するが、通常は `IAObjectModPropertyGetter<T>` インターフェース経由でアクセス
- **FormID の有無**: `ValueType.FormIdInt` または `ValueType.FormIdFloat` の場合のみ FormID を保持

### 重要なプロパティ

#### Property
- **型**: `T` (ジェネリック型パラメータ、実際は `ObjectModProperty` enum)
- **説明**: 変更対象のプロパティの種類を示す

#### ValueType
- **型**: `ObjectModProperty.ValueType` (enum)
- **説明**: 値の種類
  - `Int = 0`
  - `Float = 1`
  - `Bool = 2`
  - `String = 3`
  - `FormIdInt = 4` ← **弾薬変更で使用**
  - `Enum = 5`
  - `FormIdFloat = 6`

#### Value1
- **型**: `float`
- **説明**: プロパティ値1（ValueType によって解釈が異なる）
  - **重要**: `ValueType.FormIdInt` の場合、`Value1` には 32bit の FormID が `float` として格納されている
  - FormID として使用する場合は `(uint)prop.Value1` でキャスト

#### Value2
- **型**: `float`
- **説明**: プロパティ値2（ValueType によって解釈が異なる）

#### Step
- **型**: `float`
- **説明**: プロパティのステップ値

### ValueType と FunctionType の対応

Mutagen では、`ValueType` から `FunctionType` へのマッピングが定義されています：
- `ValueType.FormIdInt` → `FunctionType.FormID` ← **弾薬変更はこれ**
- `ValueType.FormIdFloat` → `FunctionType.Float`（FormID としては使用されない）

**重要**: 弾薬変更を行う OMOD では **必ず** `ValueType.FormIdInt` が使用されます。
他の ValueType が来るケースは想定不要です。将来的に他の ValueType で弾薬関連データが来る可能性は低いですが、
もし来た場合は警告ログを出して無視する方針とします。

### 重要な派生型

Mutagen では、ValueType に応じて以下のような具象型が存在する可能性があります：
- FormID/Int の場合、`Value1` を `uint` として解釈し、FormKey として扱える

## ObjectModProperty (enum)

### 基本構造
- **名前空間**: `Mutagen.Bethesda.Fallout4`
- **型**: Enum
- **対応ファイル**: `ObjectModProperty.cs`

### 弾薬関連の重要な値

#### Ammo
- **説明**: 武器の使用弾薬を変更
- **判定方法**: `prop.Property == ObjectModProperty.Ammo`

#### Projectile
- **説明**: 射出物（Projectile）を変更
- **判定方法**: `prop.Property == ObjectModProperty.Projectile`

### 備考
- OMOD が弾薬を変更する場合、`Properties` リスト内に `ObjectModProperty.Ammo` や `ObjectModProperty.Projectile` を持つエントリが存在します
- `ValueType` が FormID/Int の場合、`Value1` を FormKey として解釈可能

## IWeaponGetter (Weapon)

### 基本構造
- **名前空間**: `Mutagen.Bethesda.Fallout4`
- **型**: Interface (getter)
- **対応ファイル**: `Weapon.cs`

### 弾薬関連プロパティ

#### Data.Ammo
- **型**: `IFormLinkGetter<IAmmunitionGetter>`
- **説明**: 武器のデフォルト弾薬
- **null 判定**: `weapon.Data?.Ammo.IsNull ?? true` で判定

#### Data.Projectile
- **型**: `IFormLinkGetter<IProjectileGetter>`
- **説明**: 武器の射出物（デフォルト）

## IAmmunitionGetter

### 基本構造
- **名前空間**: `Mutagen.Bethesda.Fallout4`
- **型**: Interface (getter, Major Record)

### 重要なプロパティ
- **FormKey**: 弾薬の一意識別子
- **EditorID**: エディタ上の ID

## FormKey と FormLink

### FormKey
- **名前空間**: `Mutagen.Bethesda.Plugins`
- **型**: Struct
- **説明**: ModKey (プラグイン名) + FormID (レコード ID) の組み合わせ

### IFormLinkGetter<T>
- **説明**: FormKey への参照を保持
- **IsNull**: リンクが未設定かどうかを判定
- **FormKey**: 参照先の FormKey を取得

## 型安全な実装方針

### OMOD の弾薬変更判定（型安全版）

```csharp
if (omod is IObjectModificationGetter omodGetter)
{
    var properties = omodGetter.Properties;
    foreach (var prop in properties)
    {
        // Ammo または Projectile プロパティをチェック
        if (prop.Property == ObjectModProperty.Ammo || 
            prop.Property == ObjectModProperty.Projectile)
        {
            // ValueType が FormIdInt の場合のみ処理（Ammo 変更の標準形式）
            if (prop.ValueType == ObjectModProperty.ValueType.FormIdInt)
            {
                // Value1 から FormID を取得（float として格納されているので uint にキャスト）
                var formId = (uint)prop.Value1;
                
                // OMOD 自体の ModKey を使って完全な FormKey を構築
                var baseModKey = omodGetter.FormKey.ModKey;
                var ammoFormKey = new FormKey(modKey: baseModKey, id: formId);
                
                // LinkCache で IAmmunitionGetter を解決
                // （実装では IMutagenAccessor 経由で解決）
            }
            else
            {
                // 想定外の ValueType の場合は警告ログを出して無視
                _logger.LogWarning(
                    "Unexpected ValueType {ValueType} for Ammo/Projectile property in OMOD {EditorId}",
                    prop.ValueType, omodGetter.EditorID);
            }
        }
    }
}
```

### FormKey 構築の詳細

**FormKey のコンストラクタ**:
```csharp
public FormKey(ModKey modKey, uint id)
```

**実装例**:
```csharp
// OMOD 自体の FormKey から ModKey を取得
var baseModKey = omod.FormKey.ModKey;  // 例: "MyWeaponMod.esp"

// Value1 には FormID（下位24bit）が float として格納されている
var formId = (uint)prop.Value1;        // 例: 0x001234

// 完全な FormKey を構築
var ammoFormKey = new FormKey(baseModKey, formId);
// 結果: "001234:MyWeaponMod.esp"
```

### LinkCache の責務分担（設計決定）

**決定事項**: `MutagenV51Detector` は **FormKey の構築まで** を責務とし、
**IAmmunitionGetter の解決は IMutagenAccessor 経由** で行います。

**理由**:
1. Mutagen 境界を守る設計（憲法準拠）
2. `MutagenAccessor` が既に `TryResolveRecord<T>` を提供している
3. LinkCache のライフサイクル管理を Detector に持ち込まない

**実装パターン**:
```csharp
public class MutagenV51Detector : IAmmunitionChangeDetector
{
    private readonly IMutagenAccessor _accessor;
    private readonly IResourcedMutagenEnvironment _env;
    
    // Detector 内で FormKey を構築
    var ammoFormKey = new FormKey(omodGetter.FormKey.ModKey, formId);
    
    // IMutagenAccessor で解決
    if (_accessor.TryResolveRecord<IAmmunitionGetter>(_env, 
        MunitionAutoPatcher.Models.FormKey.FromMutagenFormKey(ammoFormKey), 
        out var ammoGetter))
    {
        // ammoGetter を使用
    }
}
```

### 注意点

1. **ValueType の確認**: `ValueType.FormIdInt` の場合のみ FormID として解釈（これが標準）
2. **ModKey の取得**: OMOD 自体の `FormKey.ModKey` を使用して完全な FormKey を構築
3. **float → uint キャスト**: `Value1` は `float` 型だが、実際には 32bit FormID が格納されている
4. **FormKey の変換**: Mutagen の `FormKey` と `MunitionAutoPatcher.Models.FormKey` の変換が必要

## フェーズ2: インターフェース設計の方向性

### ITypedAmmunitionChangeDetector 導入の目的

**目的**:
1. **ツール内部での型安全性向上**: `IObjectModificationGetter` / `IAmmunitionGetter` ベースで detector を使い回せるようにする
2. **外向きインターフェースの維持**: `IAmmunitionChangeDetector` (object ベース) は、外部・バージョン非依存の抽象層として残す
3. **実装の明確化**: 型安全な実装ロジックと object ベースのアダプター層を分離

**実装方針**:
```csharp
// 内部用の型安全インターフェース（新規追加）
public interface ITypedAmmunitionChangeDetector : IAmmunitionChangeDetector
{
    bool DoesOmodChangeAmmo(
        IObjectModificationGetter omod,
        IAmmunitionGetter? originalAmmo,
        out IAmmunitionGetter? newAmmo);
}

// MutagenV51Detector は両方を実装
public class MutagenV51Detector : ITypedAmmunitionChangeDetector
{
    // 実ロジックは型安全版に集約
    public bool DoesOmodChangeAmmo(
        IObjectModificationGetter omod,
        IAmmunitionGetter? originalAmmo,
        out IAmmunitionGetter? newAmmo)
    {
        // 型安全な実装（上記の例を参照）
    }
    
    // object 版は型チェック + キャストして typed 版に委譲
    public bool DoesOmodChangeAmmo(object omod, object? originalAmmoLink, out object? newAmmoLink)
    {
        newAmmoLink = null;
        
        // 型チェック
        if (omod is not IObjectModificationGetter omodGetter)
            return _fallback.DoesOmodChangeAmmo(omod, originalAmmoLink, out newAmmoLink);
        
        // originalAmmo の変換
        IAmmunitionGetter? originalAmmoTyped = TryExtractAmmoFromObject(originalAmmoLink);
        
        // typed 版に委譲
        if (DoesOmodChangeAmmo(omodGetter, originalAmmoTyped, out var newAmmoTyped))
        {
            newAmmoLink = newAmmoTyped;
            return true;
        }
        
        return false;
    }
}
```

**利点**:
- リフレクションを最小化（typed パスが優先される）
- テストが書きやすい（typed インターフェースを直接モック可能）
- 将来の拡張に備えた柔軟性（他のゲーム対応時に object 版を残せる）

## 参照リンク

- [ObjectModification.cs](https://github.com/Mutagen-Modding/Mutagen/blob/main/Mutagen.Bethesda.Fallout4/Records/Major%20Records/ObjectModification.cs)
- [ObjectModification_Generated.cs](https://github.com/Mutagen-Modding/Mutagen/blob/main/Mutagen.Bethesda.Fallout4/Records/Major%20Records/ObjectModification_Generated.cs)
- [AObjectModProperty_Generated.cs](https://github.com/Mutagen-Modding/Mutagen/blob/main/Mutagen.Bethesda.Fallout4/Records/Common%20Subrecords/AObjectModProperty_Generated.cs)
- [ObjectModProperty.cs](https://github.com/Mutagen-Modding/Mutagen/blob/main/Mutagen.Bethesda.Fallout4/Records/Common%20Subrecords/ObjectModProperty.cs)
- [Weapon.cs](https://github.com/Mutagen-Modding/Mutagen/blob/main/Mutagen.Bethesda.Fallout4/Records/Major%20Records/Weapon.cs)
- [FormKey.cs](https://github.com/Mutagen-Modding/Mutagen/blob/main/Mutagen.Bethesda.Core/Plugins/FormKey.cs)

## Repository verification (2025-11-25)

Summary of checks performed against the Mutagen repository (source file references included):

- **Per-record Property enums confirmed**: `Weapon.Property`, `Armor.Property`, `Npc.Property` are defined and used as the generic `AObjectModProperty<T>` type parameter (example: `Weapon.Property.Ammo = 61`). See `Weapon.cs` and generated modification types.
- **Generic/blank fallback**: `AObjectModification.NoneProperty` is an empty enum used for the generic/unknown OMOD case. See `ObjectModification.cs` where `ObjectTemplateBinaryCreateTranslation<AObjectModification.NoneProperty>` is chosen for generic/unknown record types.
- **Typed properties list**: Generated classes expose typed property collections, e.g. `ExtendedList<AObjectModProperty<Weapon.Property>>` with `IReadOnlyList<IAObjectModPropertyGetter<Weapon.Property>>` for `IWeaponModificationGetter`. See `WeaponModification_Generated.cs` and `AObjectModProperty_Generated.cs`.
- **ValueType mapping (ammo semantics)**: `ObjectModProperty.ValueType` contains `FormIdInt` and `FormIdFloat`; `ValueType.FormIdInt` maps to `FunctionType.FormID` and is the form used for ammo changes (value stored in `Value1` as a 32-bit FormID cast via `(uint)prop.Value1`). See `ObjectModProperty.cs`.

These findings confirm the design notes in this document: Mutagen uses record-specific `Property` enums for type safety, an empty `NoneProperty` for generic cases, and `ValueType.FormIdInt` as the canonical representation for ammo changes.

If you want, I can also paste the 100–200 line excerpts (with file/line references) for any of the referenced files (`ObjectModification.cs`, `Weapon.cs`, `WeaponModification_Generated.cs`, `AObjectModProperty_Generated.cs`, `ObjectModProperty.cs`).

## Excerpts from Mutagen source (selected snippets)

Below are selected contiguous excerpts copied from the Mutagen repository for quick reference.

`Mutagen.Bethesda.Fallout4/Records/Major Records/WeaponModification_Generated.cs`
public partial class WeaponModification :
    AObjectModification,
    IEquatable<IWeaponModificationGetter>,
    ILoquiObjectSetter<WeaponModification>,
    IWeaponModificationInternal
{
    protected WeaponModification()
    {
        CustomCtor();
    }
    partial void CustomCtor();

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private ExtendedList<AObjectModProperty<Weapon.Property>> _Properties = new ExtendedList<AObjectModProperty<Weapon.Property>>();
    public ExtendedList<AObjectModProperty<Weapon.Property>> Properties
    {
        get => this._Properties;
        init => this._Properties = value;
    }
    #region Interface Members
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    IReadOnlyList<IAObjectModPropertyGetter<Weapon.Property>> IWeaponModificationGetter.Properties => _Properties;
    #endregion

    public override void Print(
        StructuredStringBuilder sb,
        string? name = null)
    {
        WeaponModificationMixIn.Print(
            item: this,
            sb: sb,
            name: name);
    }

    public new class Mask<TItem> :
        AObjectModification.Mask<TItem>,
        IEquatable<Mask<TItem>>,
        IMask<TItem>
    {
        public Mask(TItem initialValue)
        : base(initialValue)
        {
            this.Properties = new MaskItem<TItem, IEnumerable<MaskItemIndexed<TItem, AObjectModProperty.Mask<TItem>?>>?>(initialValue, []);
        }
        ...
    }
    ...
}

`Mutagen.Bethesda.Fallout4/Records/Major Records/AObjectModification_Generated.cs` (IAObjectModificationGetter / binary overlay snippets)
public abstract partial class AObjectModification :
    Fallout4MajorRecord,
    IAObjectModificationInternal,
    IEquatable<IAObjectModificationGetter>,
    ILoquiObjectSetter<AObjectModification>
{
    protected AObjectModification()
    {
        CustomCtor();
    }
    partial void CustomCtor();

    public TranslatedString? Name { get; set; }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    ITranslatedStringGetter? IAObjectModificationGetter.Name => this.Name;

    public TranslatedString? Description { get; set; }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    ITranslatedStringGetter? IAObjectModificationGetter.Description => this.Description;

    public Model? Model { get => _Model; set => _Model = value; }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    IModelGetter? IAObjectModificationGetter.Model => this.Model;

    public UInt16 Unknown { get; set; } = default(UInt16);
    public Byte MaxRank { get; set; } = default(Byte);
    public Byte LevelTierScaledOffset { get; set; } = default(Byte);

    private readonly IFormLink<IKeywordGetter> _AttachPoint = new FormLink<IKeywordGetter>();
    public IFormLink<IKeywordGetter> AttachPoint
    {
        get => _AttachPoint;
        set => _AttachPoint.SetTo(value);
    }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    IFormLinkGetter<IKeywordGetter> IAObjectModificationGetter.AttachPoint => this.AttachPoint;

    private ExtendedList<ObjectModItem> _Items = new ExtendedList<ObjectModItem>();
    public ExtendedList<ObjectModItem> Items { get => this._Items; init => this._Items = value; }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    IReadOnlyList<IObjectModItemGetter> IAObjectModificationGetter.Items => _Items;

    private ExtendedList<ObjectModInclude> _Includes = new ExtendedList<ObjectModInclude>();
    public ExtendedList<ObjectModInclude> Includes { get => this._Includes; init => this._Includes = value; }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    IReadOnlyList<IObjectModIncludeGetter> IAObjectModificationGetter.Includes => _Includes;
    ...
}

partial class AObjectModificationBinaryCreateTranslation
{
    public static partial ParseResult FillBinaryDataParseCustom(
        MutagenFrame frame,
        IAObjectModificationInternal item,
        PreviousParse lastParsed)
    {
        frame.ReadSubrecordHeader(RecordTypes.DATA);
        var includeCount = frame.ReadInt32();
        var propertyCount = frame.ReadInt32();
        item.Unknown = frame.ReadUInt16();
        var formType = new RecordType(frame.ReadInt32());
        item.MaxRank = frame.ReadUInt8();
        item.LevelTierScaledOffset = frame.ReadUInt8();
        ...
        switch (item)
        {
            case IArmorModificationInternal armorMod:
                armorMod.Properties.SetTo(
                    ObjectTemplateBinaryCreateTranslation<Armor.Property>.ReadProperties(frame, propertyCount));
                break;
            case INpcModificationInternal npcMod:
                npcMod.Properties.SetTo(
                    ObjectTemplateBinaryCreateTranslation<Npc.Property>.ReadProperties(frame, propertyCount));
                break;
            case IWeaponModificationInternal weaponMod:
                weaponMod.Properties.SetTo(
                    ObjectTemplateBinaryCreateTranslation<Weapon.Property>.ReadProperties(frame, propertyCount));
                break;
            case IObjectModificationInternal objMod:
                objMod.Properties.SetTo(
                    ObjectTemplateBinaryCreateTranslation<AObjectModification.NoneProperty>.ReadProperties(frame, propertyCount));
                break;
            case IUnknownObjectModificationInternal unknown:
                unknown.Properties.SetTo(
                    ObjectTemplateBinaryCreateTranslation<AObjectModification.NoneProperty>.ReadProperties(frame, propertyCount));
                unknown.ModificationType = formType;
                break;
            default:
                throw new NotImplementedException();
        }
        return (int)AObjectModification_FieldIndex.Model;
    }
}

partial class ObjectModificationBinaryOverlay
{
    public IReadOnlyList<IAObjectModPropertyGetter<AObjectModification.NoneProperty>> Properties { get; internal set; } = Array.Empty<IAObjectModPropertyGetter<AObjectModification.NoneProperty>>();
}
partial class WeaponModificationBinaryOverlay
{
    public IReadOnlyList<IAObjectModPropertyGetter<Weapon.Property>> Properties { get; internal set; } = Array.Empty<IAObjectModPropertyGetter<Weapon.Property>>();
}

`Mutagen.Bethesda.Fallout4/Records/Common Subrecords/AObjectModProperty_Generated.cs`
public abstract partial class AObjectModProperty<T> :
    IAObjectModProperty<T>,
    IEquatable<IAObjectModPropertyGetter<T>>,
    ILoquiObjectSetter<AObjectModProperty<T>>
    where T : struct, Enum
{
    public AObjectModProperty()
    {
        CustomCtor();
    }
    partial void CustomCtor();

    public T Property { get; set; } = default(T);
    public Single Step { get; set; } = default(Single);

    public virtual void Print(
        StructuredStringBuilder sb,
        string? name = null)
    {
        AObjectModPropertyMixIn.Print(
            item: this,
            sb: sb,
            name: name);
    }
}

public partial interface IAObjectModPropertyGetter<out T> :
    ILoquiObject,
    IBinaryItem,
    IFormLinkContainerGetter,
    ILoquiObject<IAObjectModPropertyGetter<T>>
    where T : struct, Enum
{
    object CommonInstance(Type type0);
    object? CommonSetterInstance(Type type0);
    object CommonSetterTranslationInstance();
    static ILoquiRegistration StaticRegistration => AObjectModProperty_Registration.Instance;
    T Property { get; }
    Single Step { get; }
}

`Mutagen.Bethesda.Fallout4/Records/Common Subrecords/ObjectModProperty.cs` (ValueType enum + mapping)
public static class ObjectModProperty
{
    public enum ValueType
    {
        Int = 0,
        Float = 1,
        Bool = 2,
        String = 3,
        FormIdInt = 4,
        Enum = 5,
        FormIdFloat = 6,
    }

    public enum FunctionType
    {
        Float,
        Bool,
        Enum,
        FormID
    }

    public static FunctionType GetFunctionType(ValueType val)
    {
        return val switch
        {
            ValueType.Int => FunctionType.Float,
            ValueType.Float => FunctionType.Float,
            ValueType.Bool => FunctionType.Bool,
            ValueType.String => FunctionType.Float,
            ValueType.FormIdInt => FunctionType.FormID,
            ValueType.Enum => FunctionType.Enum,
            ValueType.FormIdFloat => FunctionType.Float,
            _ => throw new ArgumentOutOfRangeException(nameof(val), val, null)
        };
    }
    ...
}

`Mutagen.Bethesda.Fallout4/Records/Common Subrecords/ObjectModIntProperty_Generated.cs` (Value fields / FunctionType)
public partial class ObjectModIntProperty<T> :
    AObjectModProperty<T>,
    IEquatable<IObjectModIntPropertyGetter<T>>,
    ILoquiObjectSetter<ObjectModIntProperty<T>>,
    IObjectModIntProperty<T>
    where T : struct, Enum
{
    public ObjectModIntProperty()
    {
        CustomCtor();
    }

    public UInt32 Value { get; set; } = default(UInt32);
    public UInt32 Value2 { get; set; } = default(UInt32);
    public ObjectModProperty.FloatFunctionType FunctionType { get; set; } = default(ObjectModProperty.FloatFunctionType);
    ...
}

