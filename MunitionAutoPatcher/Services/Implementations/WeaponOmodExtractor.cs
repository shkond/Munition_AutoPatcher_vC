using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using System.Text;

namespace MunitionAutoPatcher.Services.Implementations;

public class WeaponOmodExtractor : IWeaponOmodExtractor
{
    private readonly ILoadOrderService _loadOrderService;

    public WeaponOmodExtractor(ILoadOrderService loadOrderService)
    {
        _loadOrderService = loadOrderService;
    }

    public async Task<List<OmodCandidate>> ExtractCandidatesAsync(IProgress<string>? progress = null)
    {
        progress?.Report("OMOD/COBJ の候補を抽出しています...");
        var results = new List<OmodCandidate>();

        var loadOrder = await _loadOrderService.GetLoadOrderAsync();
        if (loadOrder == null)
        {
            progress?.Report("エラー: ロードオーダーが取得できませんでした");
            return results;
        }

        try
        {
            using var env = GameEnvironment.Typical.Fallout4(Fallout4Release.Fallout4);

            // 1) Enumerate all ConstructibleObject records and record those that create objects (CreatedObject will usually be a weapon/item)
            var cobjs = env.LoadOrder.PriorityOrder.ConstructibleObject().WinningOverrides();
            foreach (var cobj in cobjs)
            {
                try
                {
                    var created = cobj.CreatedObject;
                    if (created.IsNull) continue;

                    // Record the created object's FormKey as a candidate. Resolution to a Weapon record (to inspect Ammo) may be done later.
                    results.Add(new OmodCandidate
                    {
                        CandidateType = "COBJ",
                        CandidateFormKey = new Models.FormKey { PluginName = created.FormKey.ModKey.FileName, FormId = created.FormKey.ID },
                        CandidateEditorId = cobj.EditorID ?? string.Empty,
                        CandidateAmmo = null,
                        CandidateAmmoName = string.Empty,
                        SourcePlugin = cobj.FormKey.ModKey.FileName,
                        Notes = $"COBJ source: {cobj.FormKey.ModKey.FileName}:{cobj.FormKey.ID:X8}",
                        SuggestedTarget = "CreatedWeapon"
                    });
                }
                catch { }
            }

            // 2) ObjectMod enumeration is not performed here (API surface differs per Mutagen version); subject to future enhancement.

            // 3) (Reference scanning via LinkCache was skipped to avoid version-specific API complexity.)

            // 4) Write CSV for debugging into artifacts
            try
            {
                var repoRoot = FindRepoRoot();
                var artifactsDir = System.IO.Path.Combine(repoRoot, "artifacts", "RobCo_Patcher");
                if (!System.IO.Directory.Exists(artifactsDir))
                    System.IO.Directory.CreateDirectory(artifactsDir);

                var fileName = $"weapon_omods_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var path = System.IO.Path.Combine(artifactsDir, fileName);
                using var sw = new System.IO.StreamWriter(path, false, Encoding.UTF8);
                sw.WriteLine("CandidateType,BaseWeapon,BaseEditorId,CandidateFormKey,CandidateEditorId,CandidateAmmo,SourcePlugin,Notes,SuggestedTarget");
                foreach (var c in results)
                {
                    var baseKey = c.BaseWeapon != null ? $"{c.BaseWeapon.PluginName}:{c.BaseWeapon.FormId:X8}" : string.Empty;
                    var candKey = c.CandidateFormKey != null ? $"{c.CandidateFormKey.PluginName}:{c.CandidateFormKey.FormId:X8}" : string.Empty;
                    var ammoKey = c.CandidateAmmo != null ? $"{c.CandidateAmmo.PluginName}:{c.CandidateAmmo.FormId:X8}" : string.Empty;
                    sw.WriteLine($"{c.CandidateType},{baseKey},{Escape(c.BaseWeaponEditorId)},{candKey},{Escape(c.CandidateEditorId)},{ammoKey},{c.SourcePlugin},{Escape(c.Notes)},{c.SuggestedTarget}");
                }
                sw.Flush();
                progress?.Report($"OMOD 抽出 CSV を生成しました: {path}");
            }
            catch (Exception ex)
            {
                progress?.Report($"警告: CSV の出力に失敗しました: {ex.Message}");
            }

            progress?.Report($"抽出完了: {results.Count} 件の候補を検出しました");
            return results;
        }
        catch (Exception ex)
        {
            progress?.Report($"エラー: OMOD 抽出中に例外が発生しました: {ex.Message}");
            return results;
        }
    }

    private string Escape(string? s)
    {
        if (s == null) return string.Empty;
        return s.Replace("\"", "\\\"").Replace(',', ';');
    }

    private string FindRepoRoot()
    {
        try
        {
            var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var solutionPath = System.IO.Path.Combine(dir.FullName, "MunitionAutoPatcher.sln");
                if (System.IO.File.Exists(solutionPath))
                    return dir.FullName;
                dir = dir.Parent;
            }
        }
        catch { }
        return AppContext.BaseDirectory;
    }
}
