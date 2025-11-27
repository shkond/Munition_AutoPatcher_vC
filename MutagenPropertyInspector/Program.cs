using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using System.Reflection;

namespace MutagenPropertyInspector;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Mutagen API Inspector for Fallout4 ===");
        Console.WriteLine();

        try
        {
            Console.WriteLine("Creating GameEnvironment...");
            
            // GameEnvironment を作成
            using var env = GameEnvironment.Typical.Fallout4(Fallout4Release.Fallout4);
            var linkCache = env.LinkCache;

            Console.WriteLine($"Load Order: {env.LoadOrder.Count()} plugins loaded");
            Console.WriteLine();

            // OMOD (ObjectModification) を調査
            InspectObjectModifications(env);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }

        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    static void InspectObjectModifications(IGameEnvironment<IFallout4Mod, IFallout4ModGetter> env)
    {
        Console.WriteLine("=== ObjectModification (OMOD) Investigation ===");
        Console.WriteLine();

        try
        {
            // WinningOverrides を取得（既存コードと同じ形式）
            var omods = env.LoadOrder.PriorityOrder.ObjectModification().WinningOverrides().ToList();
            Console.WriteLine($"Total OMODs: {omods.Count}");
            Console.WriteLine();

            // 最初の 5 件で詳細調査
            var sampleSize = Math.Min(5, omods.Count);
            Console.WriteLine($"Inspecting first {sampleSize} OMODs in detail:");
            Console.WriteLine();

            for (int i = 0; i < sampleSize; i++)
            {
                var omod = omods[i];
                Console.WriteLine($"=== OMOD #{i + 1}: {omod.EditorID} ({omod.FormKey}) ===");

                // Properties を調査
                if (omod.Properties != null && omod.Properties.Count > 0)
                {
                    Console.WriteLine($"Properties Count: {omod.Properties.Count}");
                    
                    foreach (var prop in omod.Properties)
                    {
                        Console.WriteLine($"  ---Property Entry---");
                        
                        // Property の型情報
                        var propType = prop.GetType();
                        Console.WriteLine($"  .NET Type: {propType.Name}");
                        
                        // Property enum の値
                        try
                        {
                            var propertyValue = prop.Property;
                            Console.WriteLine($"  Property enum: {propertyValue}");
                            Console.WriteLine($"  Property enum Type: {propertyValue.GetType().FullName}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  Property enum: ERROR - {ex.Message}");
                        }

                        // Step プロパティ
                        try
                        {
                            var step = prop.Step;
                            Console.WriteLine($"  Step: {step} (hex: 0x{step:X})");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  Step: ERROR - {ex.Message}");
                        }

                        // すべてのプロパティを列挙（リフレクション）
                        Console.WriteLine($"  All Public Properties:");
                        foreach (var p in propType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                        {
                            try
                            {
                                var val = p.GetValue(prop);
                                if (val != null)
                                {
                                    Console.WriteLine($"    {p.Name} = {val} (Type: {val.GetType().Name})");
                                }
                                else
                                {
                                    Console.WriteLine($"    {p.Name} = (null)");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"    {p.Name} = ERROR: {ex.Message}");
                            }
                        }
                        Console.WriteLine();
                    }
                }
                else
                {
                    Console.WriteLine($"Properties: None");
                }

                Console.WriteLine();
            }

            // "Ammo" という文字列を含む Property を持つ OMOD を探す
            Console.WriteLine();
            Console.WriteLine("=== Searching for Ammo-related OMODs ===");
            
            var ammoOmods = omods.Where(o => 
                o.Properties != null && 
                o.Properties.Any(p => p.Property.ToString().Contains("Ammo", StringComparison.OrdinalIgnoreCase))
            ).Take(10).ToList();

            Console.WriteLine($"Found {ammoOmods.Count} OMODs with 'Ammo' in Property:");
            Console.WriteLine();
            
            foreach (var omod in ammoOmods)
            {
                Console.WriteLine($"OMOD: {omod.EditorID} ({omod.FormKey})");
                
                foreach (var prop in omod.Properties.Where(p => p.Property.ToString().Contains("Ammo", StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine($"  Property: {prop.Property}");
                    Console.WriteLine($"  Step: 0x{prop.Step:X}");
                    
                    // Value1 を探す
                    var value1Prop = prop.GetType().GetProperty("Value1");
                    if (value1Prop != null)
                    {
                        try
                        {
                            var value1 = value1Prop.GetValue(prop);
                            Console.WriteLine($"  Value1: {value1} (hex: 0x{Convert.ToUInt32(value1):X8})");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  Value1: ERROR - {ex.Message}");
                        }
                    }
                    
                    // Value2 を探す
                    var value2Prop = prop.GetType().GetProperty("Value2");
                    if (value2Prop != null)
                    {
                        try
                        {
                            var value2 = value2Prop.GetValue(prop);
                            Console.WriteLine($"  Value2: {value2}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  Value2: ERROR - {ex.Message}");
                        }
                    }
                    
                    Console.WriteLine();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR in InspectObjectModifications: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}
