// コンパイル時型チェック用の実験的コード
// このコードは実行しません。コンパイルの成功/失敗のみ確認します。

using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;

namespace MutagenPropertyInspector;

public class PropertyTypeInspector
{
    public static void InspectWeaponModification(IWeaponModificationGetter weaponMod)
    {
        // Phase 3で成功している型安全アクセス
        foreach (var property in weaponMod.Properties)
        {
            // propertyの型を確認
            if (property is IFormLinkGetter formLink)
            {
                var fk = formLink.FormKey;
                Console.WriteLine($"Found FormLink: {fk}");
            }
        }
    }

    public static void InspectArmorModification(IArmorModificationGetter armorMod)
    {
        // 仮説1の検証: IArmorModificationGetterもPropertiesを持つか？
        // コンパイルエラーがなければ、型安全アクセス可能
        foreach (var property in armorMod.Properties)
        {
            // propertyの型を確認
            if (property is IFormLinkGetter formLink)
            {
                var fk = formLink.FormKey;
                Console.WriteLine($"Found FormLink in Armor: {fk}");
            }
            
            // propertyの実際の型を出力
            Console.WriteLine($"Property type: {property.GetType().Name}");
        }
    }

    public static void InspectNpcModification(INpcModificationGetter npcMod)
    {
        // 仮説2の検証: INpcModificationGetterもPropertiesを持つか？
        // コンパイルエラーがなければ、型安全アクセス可能
        foreach (var property in npcMod.Properties)
        {
            // propertyの型を確認
            if (property is IFormLinkGetter formLink)
            {
                var fk = formLink.FormKey;
                Console.WriteLine($"Found FormLink in NPC: {fk}");
            }
            
            // propertyの実際の型を出力
            Console.WriteLine($"Property type: {property.GetType().Name}");
        }
    }

    public static void InspectGenericObjectMod(IObjectModificationGetter omod)
    {
        // 基底型からのアクセス可能性確認
        // AttachPointは既に確認済み（Phase 3成功）
        var ap = omod.AttachPoint;
        Console.WriteLine($"AttachPoint: {ap.FormKey}");
    }
}
