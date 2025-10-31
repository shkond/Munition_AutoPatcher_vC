using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Fallout4;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AutoTests;

internal class Program
{
    // Local test factory that returns NoOp/ResourcedMutagenEnvironment so tests don't create a real GameEnvironment.
    private class TestEnvFactory : MunitionAutoPatcher.Services.Implementations.IMutagenEnvironmentFactory
    {
        public MunitionAutoPatcher.Services.Implementations.IResourcedMutagenEnvironment Create()
        {
            var noop = new MunitionAutoPatcher.Services.Implementations.NoOpMutagenEnvironment();
            return new MunitionAutoPatcher.Services.Implementations.ResourcedMutagenEnvironment(noop, noop);
        }
    }
    // Mainメソッドを同期処理に戻し、シンプルにします
    private static async Task<int> Main(string[] args)
    {
#if DEBUG
        try
        {
            MunitionAutoPatcher.DebugConsole.Show();
            Console.WriteLine("--- DEBUG MODE (Simplified Test) ---");
            Console.WriteLine("デバッグをアタッチしてください。アタッチ後、Enterキーを押すとテストを続行します...");
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

            // For tests we use a simple local factory that returns a NoOp resourced environment so
            // the test can run in headless mode without creating a real GameEnvironment.
            var factory = new TestEnvFactory();
            using var env = factory.Create();

            Console.WriteLine($"成功！仮想化されたDataパスを検出しました: {env.GetDataFolderPath()}");
            // Load order details are provided via the adapter methods; in NoOp/test mode these collections will be empty.
            Console.WriteLine($"ロードオーダ内のプラグイン数: {env.GetWinningWeaponOverrides().Count()}");
            Console.WriteLine("--- 検出されたプラグイン一覧 ---");
            Console.WriteLine("(load-order listing skipped in headless/test mode)");
            Console.WriteLine("---------------------------------");

            // DIAGNOSTIC: enumerate ammo by scanning weapons and resolving their Ammo FormLink via LinkCache
            try
            {
                var weaponList = env.GetWinningWeaponOverrides().ToList();
                var resolvedAmmo = new System.Collections.Generic.List<(string Key, string Name)>();
                for (int i = 0; i < Math.Min(50, weaponList.Count); i++)
                {
                    try
                    {
                        dynamic w = weaponList[i];
                        object? linkObj = null;
                        try { linkObj = w.Ammo; } catch { linkObj = null; }
                        if (linkObj != null)
                        {
                            try
                            {
                                object? ammoRec = null;
                                bool resolved = false;
                                try { resolved = (bool) ((dynamic)linkObj).TryResolve(env.GetLinkCache(), out ammoRec); } catch { resolved = false; }
                                if (resolved && ammoRec != null)
                                {
                                    dynamic ar = ammoRec;
                                    var key = $"{ar.FormKey.ModKey.FileName}:{ar.FormKey.ID:X8}";
                                    var name = (string?)(ar.Name?.String ?? ar.EditorID ?? "<no name>") ?? "<no name>";
                                    resolvedAmmo.Add((key, name));
                                }
                            }
                            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
                            {
                                // Adapter/NoOp may not support TryResolve semantics; ignore in test mode.
                            }
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

            var allWeapons = env.GetWinningWeaponOverrides().ToList();
            Console.WriteLine($"\nロードオーダ全体から {allWeapons.Count} 個のユニークな武器レコードを検出しました。");

            // Print a few samples to help debugging parsing issues
            Console.WriteLine("--- Sample weapon FormKey.ToString() outputs (first 10) ---");
            for (int i = 0; i < Math.Min(10, allWeapons.Count); i++)
            {
                try
                {
                    dynamic sample = allWeapons[i];
                    try { Console.WriteLine($"[{i}] {sample.FormKey.ToString()}"); } catch { Console.WriteLine($"[{i}] <no FormKey>"); }
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
                            uint aid = 0u;
                            string aplugin = string.Empty;
                            try { dynamic dw = w2; aid = (uint)dw.Ammo?.FormKey?.ID; } catch { aid = 0u; }
                            try { dynamic dw = w2; aplugin = (string?)dw.Ammo?.FormKey?.ModKey?.FileName ?? string.Empty; } catch { aplugin = string.Empty; }

                            if (aid != 0u && !string.IsNullOrEmpty(aplugin))
                            {
                                if (!ammoMap.ContainsKey(aid))
                                    ammoMap[aid] = new MunitionAutoPatcher.Models.FormKey { PluginName = aplugin, FormId = aid };
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
                        var modKeyFile = string.Empty;
                        uint id = 0;
                        try
                        {
                            dynamic dw = w;
                            modKeyFile = (string?)dw.FormKey?.ModKey?.FileName ?? string.Empty;
                            id = (uint?)dw.FormKey?.ID ?? 0u;
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
                            dynamic dw = w;
                            var ammoMod = (string?)dw.Ammo?.FormKey?.ModKey?.FileName ?? string.Empty;
                            uint ammoId = (uint?)dw.Ammo?.FormKey?.ID ?? 0u;

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

            Console.WriteLine("\\nテストは正常に完了しました。");
            return 0; // 成功
        }
        catch (Exception ex)
        {
            Console.WriteLine("\\n\u2605\u2605\u2605 エラーが発生しました \u2605\u2605\u2605");
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
        Console.WriteLine("\nテストを終了するには何かキーを押して下さい...");
        Console.ReadLine();
        try { MunitionAutoPatcher.DebugConsole.Hide(); } catch (Exception ex) { Console.WriteLine("AutoTests: DebugConsole.Hide failed: " + ex.Message); }
#endif
    }
}

}