using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Fallout4;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AutoTests;

internal class Program
{
    // Mainメソッドを同期処理に戻し、シンプルにします
    private static async Task<int> Main(string[] args)
    {
#if DEBUG
        try
        {
            MunitionAutoPatcher.DebugConsole.Show();
            Console.WriteLine("--- DEBUG MODE (Simplified Test) ---");
            Console.WriteLine("デバッガをアタッチしてください。アタッチ後、Enterキーを押すとテストを続行します...");
            Console.ReadLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine("AutoTests: Debug wait failed: " + ex.Message);
        }
#endif

        try
        {
            Console.WriteLine("Mutagenのゲーム環境を自動検出でロードしています...");

            // ★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★
            // ★★★ これが最重要部分です。手動のパス設定を一切行わず、Mutagenにすべてを任せます。★★★
            // ★★★ MO2経由で実行されていれば、これだけで仮想化された環境を自動的に見つけ出します。★★★
            // ★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★
            using var env = GameEnvironment.Typical.Fallout4(Fallout4Release.Fallout4);

            Console.WriteLine($"成功！仮想化されたDataパスを検出しました: {env.DataFolderPath}");
            Console.WriteLine($"ロードオーダー内のプラグイン数: {env.LoadOrder.Count}");
            Console.WriteLine("--- 検出されたプラグイン一覧 ---");
            foreach (var modListing in env.LoadOrder)
            {
                // modListing may implement IModListingGetter with a ModKey property
                try
                {
                    var fileName = modListing.Key.FileName;
                    Console.WriteLine($"- {fileName}");
                }
                catch
                {
                    Console.WriteLine("- <unknown plugin>");
                }
            }
            Console.WriteLine("---------------------------------");

            // DIAGNOSTIC: enumerate ammo by scanning weapons and resolving their Ammo FormLink via LinkCache
            try
            {
                var weaponList = env.LoadOrder.PriorityOrder.Weapon().WinningOverrides().ToList();
                var resolvedAmmo = new System.Collections.Generic.List<(string Key, string Name)>();
                for (int i = 0; i < Math.Min(50, weaponList.Count); i++)
                {
                    try
                    {
                        var w = weaponList[i];
                        var link = w.Ammo;
                        if (!link.IsNull && link.TryResolve(env.LinkCache, out var ammoRec))
                        {
                            var key = $"{ammoRec.FormKey.ModKey.FileName}:{ammoRec.FormKey.ID:X8}";
                            var name = ammoRec.Name?.String ?? ammoRec.EditorID ?? "<no name>";
                            resolvedAmmo.Add((key, name));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Weapon {i}] ammo-resolve error: {ex.Message}");
                    }
                }

                Console.WriteLine($"Resolved ammo entries from weapons: {resolvedAmmo.Count}");
                for (int i = 0; i < Math.Min(20, resolvedAmmo.Count); i++)
                {
                    var a = resolvedAmmo[i];
                    Console.WriteLine($"[AmmoSample {i}] {a.Key} - Name='{a.Name}'");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("GameEnvironment weapon->ammo diagnostic failed: " + ex.Message);
            }

            var allWeapons = env.LoadOrder.PriorityOrder.Weapon().WinningOverrides().ToList();
            Console.WriteLine($"\nロードオーダー全体から {allWeapons.Count} 個のユニークな武器レコードを検出しました。");

            // Print a few samples to help debugging parsing issues
            Console.WriteLine("--- Sample weapon FormKey.ToString() outputs (first 10) ---");
            for (int i = 0; i < Math.Min(10, allWeapons.Count); i++)
            {
                try
                {
                    Console.WriteLine($"[{i}] {allWeapons[i].FormKey.ToString()}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{i}] <error getting FormKey.ToString()>: {ex.Message}");
                }
            }
            Console.WriteLine("---------------------------------");

            // Generate RobCo INI into repository artifacts folder
            try
            {
                // Build an ammo lookup map by scanning weapons' DefaultAmmo entries to help fill missing ModKey/FileName cases
                var ammoMap = new System.Collections.Generic.Dictionary<uint, MunitionAutoPatcher.Models.FormKey>();
                    try
                    {
                        foreach (var w2 in allWeapons)
                        {
                            try
                            {
                                if (w2.Ammo?.FormKey != null)
                                {
                                    var aid = w2.Ammo.FormKey.ID;
                                    var aplugin = string.Empty;
                                    try { aplugin = w2.Ammo.FormKey.ModKey.FileName; } catch (Exception ex) { Console.WriteLine("AutoTests: failed reading Ammo FormKey.ModKey.FileName: " + ex.Message); aplugin = string.Empty; }
                                    if (aid != 0u && !string.IsNullOrEmpty(aplugin))
                                    {
                                        if (!ammoMap.ContainsKey(aid))
                                            ammoMap[aid] = new MunitionAutoPatcher.Models.FormKey { PluginName = aplugin, FormId = aid };
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("AutoTests: failed processing weapon while building ammo map: " + ex);
                            }
                        }
                        Console.WriteLine($"Ammo lookup entries built from weapons: {ammoMap.Count}");
                    }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to build ammo lookup from weapons: {ex.Message}");
                }

                // Build mappings from detected weapons
                var mappings = new System.Collections.Generic.List<MunitionAutoPatcher.Models.WeaponMapping>();
                int idx = 0;
                int fallbackResolved = 0;
                foreach (var w in allWeapons)
                {
                    idx++;
                    try
                    {
                        // Prefer using Mutagen getter properties directly to get reliable plugin filename and ID
                        var modKeyFile = "";
                        uint id = 0;
                        try
                        {
                            // Read FormKey properties directly; Mutagen types may be non-nullable
                            modKeyFile = w.FormKey.ModKey.FileName;
                            id = w.FormKey.ID;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("AutoTests: failed reading weapon FormKey properties: " + ex.Message);
                        }

                        if (string.IsNullOrEmpty(modKeyFile) || id == 0u)
                        {
                            Console.WriteLine($"[{idx}] Skipping weapon because ModKey/FileName or ID was not available (Mod='{modKeyFile}', ID=0x{id:X8})");
                            continue;
                        }

                        var weaponKey = new MunitionAutoPatcher.Models.FormKey
                        {
                            PluginName = modKeyFile,
                            FormId = id
                        };

                        var ammoKey = new MunitionAutoPatcher.Models.FormKey();
                        try
                        {
                            if (w.Ammo != null && w.Ammo.FormKey != null)
                            {
                                var ammoMod = string.Empty;
                                uint ammoId = 0u;
                                try { ammoMod = w.Ammo.FormKey.ModKey.FileName; } catch (Exception ex) { Console.WriteLine("AutoTests: failed reading ammo ModKey.FileName: " + ex.Message); ammoMod = string.Empty; }
                                try { ammoId = w.Ammo.FormKey.ID; } catch (Exception ex) { Console.WriteLine("AutoTests: failed reading ammo ID: " + ex.Message); ammoId = 0u; }

                                if (!string.IsNullOrEmpty(ammoMod) && ammoId != 0u)
                                {
                                    ammoKey = new MunitionAutoPatcher.Models.FormKey
                                    {
                                        PluginName = ammoMod,
                                        FormId = ammoId
                                    };
                                }
                                else if (ammoId != 0u && ammoMap.TryGetValue(ammoId, out var fallback))
                                {
                                    // Fill missing plugin name using ammo lookup map
                                    ammoKey = fallback;
                                    fallbackResolved++;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error building mapping for weapon #{idx}: {ex}");
                        }

                        mappings.Add(new MunitionAutoPatcher.Models.WeaponMapping
                        {
                            WeaponFormKey = weaponKey,
                            AmmoFormKey = ammoKey,
                            Strategy = "Default",
                            IsManualMapping = false
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error building mapping for weapon #{idx}: {ex}");
                    }
                }

                Console.WriteLine($"Built mappings count: {mappings.Count}");
                Console.WriteLine($"Fallback-resolved ammo entries: {fallbackResolved}");

                // Find repository root by searching for the solution file
                string FindRepoRoot(string start)
                {
                    var dir = new System.IO.DirectoryInfo(start);
                    while (dir != null)
                    {
                        if (System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "MunitionAutoPatcher.sln")))
                            return dir.FullName;
                        dir = dir.Parent;
                    }
                    return System.IO.Path.GetFullPath(start);
                }

                var assemblyFolder = AppContext.BaseDirectory;
                var repoRoot = FindRepoRoot(assemblyFolder);
                var artifactsDir = System.IO.Path.Combine(repoRoot, "artifacts");
                if (!System.IO.Directory.Exists(artifactsDir)) System.IO.Directory.CreateDirectory(artifactsDir);
                var tmp = System.IO.Path.Combine(artifactsDir, $"munition_autopatcher_{DateTime.Now:yyyyMMdd_HHmmss}.ini");

                var iniGen = new MunitionAutoPatcher.Services.Implementations.RobCoIniGenerator();
                var ok = await iniGen.GenerateIniAsync(tmp, mappings, new Progress<string>(s => Console.WriteLine(s)));
                Console.WriteLine($"INI written: {ok} -> {tmp}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"INI generation failed: {ex.Message}");
            }

            Console.WriteLine("\nテストは正常に完了しました。");
            return 0; // 成功
        }
        catch (Exception ex)
        {
            Console.WriteLine("\n★★★ エラーが発生しました ★★★");
            Console.WriteLine("Mutagenがゲーム環境をロードできませんでした。");
            Console.WriteLine("考えられる原因:");
            Console.WriteLine("1. このプログラムがMO2経由で実行されていない。");
            Console.WriteLine("2. MO2のバージョンが古い、またはアンチウイルスソフトが干渉している。");
            Console.WriteLine("3. Fallout 4のマスターファイル(.esm)が仮想Dataフォルダ内に見つからない。");
            Console.WriteLine("\n--- エラー詳細 ---");
            Console.WriteLine(ex.ToString());
            return 1; // 失敗
        }
        finally
        {
#if DEBUG
        Console.WriteLine("\nテストを終了するには何かキーを押してください...");
        Console.ReadLine();
        try { MunitionAutoPatcher.DebugConsole.Hide(); } catch (Exception ex) { Console.WriteLine("AutoTests: DebugConsole.Hide failed: " + ex.Message); }
#endif
    }
    }
}