1. LinkResolver / LinkCacheHelper 現状
実装状況
LinkResolver は以下の複数経路を持つ複合実装になっています:

TryResolve(object linkLike) と TryResolve<TGetter>(object linkLike) のオーバーロード。
ResolveByKey(FormKey key) と ResolveByKey(object key) の 2 系統。
内部に _linkCache を object として保持し、_typedLinkCache が ILinkCache? に格納される（private readonly object _linkCache; private readonly ILinkCache? _typedLinkCache;）。
キャッシュ（ConcurrentDictionary<string, object?> _cache）で再解決を抑制。
カスタム FormKey（MunitionAutoPatcher.Models.FormKey）を Mutagen の FormKey に変換する TryToMutagenFormKey が存在。
LinkCacheHelper は非常に多くの反射パス（TryResolve, Resolve(string), TryResolve(FormKey), 3引数 TryResolve(FormKey, Type, out), ジェネリック TryResolve<T> など）をスキャンしようとしている巨大ユーティリティ。

現行の問題点
解決経路が「複雑で冗長」かつ「成功/失敗のログが Debug 中心」であり、実行時に何がヒットしているかを Information レベルで即把握しづらい。
ResolveByKey(FormKey key) 内で:
Mutagen 変換 → typed fast-path → fallback → LinkCacheHelper.TryResolveViaLinkCache など段階的処理を行うが、実際の FO4 環境下で typed fast-path がヒットしているかを示すログが出力されていない（Debug レベル）。
3 引数 TryResolve(FormKey, Type, out IMajorRecordGetter) の反射スキャン (TryThreeParamFormKeyResolve) が存在するが、Mutagen 実装側の実際のシグネチャに確実に一致している保証がない（本来 Mutagen はジェネリック TryResolve<TGetter>(FormKey, out TGetter) が基本）。
カスタム FormKey → Mutagen FormKey 変換処理が散在（TryToMutagenFormKey、MutagenReflectionHelpers、LinkCacheHelper 内）。重複と責務過多で保守性が低下。

状態評価
リポジトリは「解決のためのコードは多数存在するが、一貫性とトレーサビリティが低く、実行時に resolvedToOmod=0 を引き起こす要因の切り分けが難しい構成」になっている。
既に LinkResolver はキャッシュを持っているため、「過度な反射フォールバックより、型付きジェネリック Fast-Path を明示的に列挙する方が速く安定」するはず。

2. ConfirmationContext 構築
現状
ConfirmationContext は Resolver (ILinkResolver?) と LinkCache (ILinkCache?) の両方をプロパティとして保持（以前の改善案どおり型が整理済み）。
WeaponOmodExtractor.BuildConfirmationContextAsync:
環境（environment）や formLinkCache が null の場合に BuildConcreteLinkCache(environment) → load order からキャッシュ構築のフォールバック。
実際に actualLinkCache が取得できれば新しい LinkResolver を構築し、そうでなければ既存 resolver にフォールバック。
ログ: “constructed resolver with concrete LinkCache” / “falling back to provided resolver without concrete LinkCache”。
問題点（ログから想定）
実行時ログで FormLinkCachePresent が False のままとの報告が以前あったが、現行コードでは LinkCache と Resolver 両方を持つ仕組みに改修済み。
ただし AttachPointConfirmer の resolvedToOmod が 0 → ConfirmationContext.LinkCache か Resolver の内部で実際に Mutagen レコード取得ができていない。
BuildConfirmationContextAsync 内の BuildLinkCacheFromLoadOrder の成功/失敗をより詳細に出す追加ログ（mod 数、型、TryResolve テスト結果）がないため、LinkCache の内容品質が不明。
3. AttachPointConfirmer の挙動
現状
Confirm 内で:
先頭 N (=20) 件の候補ダンプ（CandidateDump）を Information レベルで出力済み。
解決カウンタ（rootNull, createdObjMissing, createdObjResolveFail, createdObjNotOmod）を統計ログ出力。
ResolveOmodForCandidate 内で:
Mutagen FormKey 変換後に context.Resolver?.TryResolve(mfk.Value, ...) を試す。
ダメなら context.Resolver?.ResolveByKey(candidate.CandidateFormKey)。
さらに context.LinkCache.TryResolve<IObjectModificationGetter> / IConstructibleObjectGetter などの typed パスを列挙。
resolvedToOmod++ する地点は omodGetter != null の直後。
それでも実行時には resolvedToOmod=0 のままとの報告 → 上記 typed / generic パスが実際にはヒットしていない。
問題点
TryResolve / typed resolve が失敗した際の詳細（どの FormKey で MISS したか）が Information レベルに出ていないため、失敗原因（FormKey 間違い vs LinkCache 不備 vs タイプ不一致）を特定困難。
ToMutagenFormKey の判定基準（拡張子→ModType 分岐）と実際の LoadOrder の ModKey（Fallout4.esm など）整合性検証ログがない。
TryFindAttachPointLink が多数の OMOD の property 名を走査するが、OMOD として解決されていないので到達しないまま。
4. テストの状態
tests/LinkCacheHelperTests/* に多数のフォールバック・反射試験用のダミーキャッシュがあり、ヘルパのロジックは単体テストで網羅されている。
ただし実ゲームデータの ILinkCache を使った統合テスト（実在 FormKey を解決できるか）は存在しない（Fake のみ）。
WeaponOmodExtractor の Cancel テストはあるが、確認フェーズでの解決成功を検証するテストが不足。

6. 改善提案（次アクション優先度順）
最低 1 件の「既知バニラ OMOD / COBJ / WEAP FormKey」手動 resolve を Information レベルでログ出す Quick Check
例: Fallout4.esm:0004D00C (任意の ObjectModification) を LinkCache.TryResolve<IObjectModificationGetter> 直呼びして結果ログ。

LinkResolver を簡素化（型付き Fast-Path を最初に列挙、反射フォールバックは最後）

現行 LinkCacheHelper の巨大反射は縮小し、Mutagen のジェネリック TryResolve<T>(FormKey, out T) を優先。
MISS 時に Information ログで MISS {Mod}:{Id:X8} を確実に出す。
FormKey 正規化一元化

専用 FormKeyNormalizer.Normalize(string plugin, uint id) を 1 箇所で提供し、拡張子補完・大文字小文字調整・ModType 判定を統一。
AttachPointConfirmer での失敗詳細ログ拡張

rootNull の具体例（先頭 10 件の CandidateFormKey + CandidateType）を追加。
Typed resolve MISS 時に ResolveOmod MISS path=IObjectModificationGetter {plugin}:{id} のログ。
統合テスト追加

実際の LoadOrder（軽量 fixture, たとえば最小構成の 1 つの esp）で AttachPointConfirmer が resolvedToOmod>0 になることを検証。
過剰な反射パス削減

LinkCacheHelper から使用頻度が低い/成功率が低いパス（3引数 TryResolve Type 反射など）を一旦無効化しログを減少。
パフォーマンスを測定する（Stopwatch で 1000 件 TryResolve の平均 ms を出力）。

using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using MunitionAutoPatcher.Models;

namespace MunitionAutoPatcher.Services.Implementations
{
    /// <summary>
    /// Simplified LinkResolver focusing on explicit typed fast-path + generic fallback.
    /// </summary>
    public sealed class LinkResolver : ILinkResolver
    {
        private readonly ILinkCache _cache;
        private readonly ILogger<LinkResolver> _logger;
        private readonly ConcurrentDictionary<string, object?> _memo = new(StringComparer.Ordinal);

        public LinkResolver(ILinkCache cache, ILogger<LinkResolver> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public ILinkCache? LinkCache => _cache;

        public bool TryResolve(object linkLike, out object? result)
        {
            result = ResolveInternal(linkLike);
            return result != null;
        }

        public bool TryResolve<TGetter>(object linkLike, out TGetter? result) where TGetter : class?
        {
            var r = ResolveInternal(linkLike);
            result = r as TGetter;
            return result != null;
        }

        public object? ResolveByKey(FormKey key)
        {
            return ResolveInternal(key);
        }

        public object? ResolveByKey(MunitionAutoPatcher.Models.FormKey key)
        {
            var mfk = ToMutagenFormKey(key);
            return mfk.HasValue ? ResolveInternal(mfk.Value) : null;
        }

        private object? ResolveInternal(object key)
        {
            if (key == null) return null;

            string cacheKey = key switch
            {
                FormKey fk => $"FK:{fk.ModKey.FileName}:{fk.ID:X8}",
                MunitionAutoPatcher.Models.FormKey ck => $"CFK:{ck.PluginName}:{ck.FormId:X8}",
                _ => $"OBJ:{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(key)}:{key.GetType().Name}"
            };

            if (_memo.TryGetValue(cacheKey, out var cached))
                return cached;

            object? resolved = null;

            try
            {
                if (key is MunitionAutoPatcher.Models.FormKey custom)
                {
                    var mfk = ToMutagenFormKey(custom);
                    if (mfk.HasValue)
                        resolved = ResolveFormKeyFast(mfk.Value);
                }
                else if (key is FormKey fk)
                {
                    resolved = ResolveFormKeyFast(fk);
                }
                else
                {
                    // Try extracting FormKey property
                    var prop = key.GetType().GetProperty("FormKey");
                    var raw = prop?.GetValue(key);
                    if (raw is FormKey mfk2)
                        resolved = ResolveFormKeyFast(mfk2);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "LinkResolver: ResolveInternal failed for {Type}", key.GetType().FullName);
            }

            _memo[cacheKey] = resolved;
            return resolved;
        }

        private object? ResolveFormKeyFast(FormKey fk)
        {
            // Typed fast-path (add more if needed)
            if (_cache.TryResolve(fk, out var any))
            {
                _logger.LogInformation("LinkResolver: generic resolve SUCCESS {Mod}:{Id:X8}", fk.ModKey.FileName, fk.ID);
                return any;
            }

            _logger.LogInformation("LinkResolver: generic resolve MISS {Mod}:{Id:X8}", fk.ModKey.FileName, fk.ID);
            return null;
        }

        private FormKey? ToMutagenFormKey(MunitionAutoPatcher.Models.FormKey fk)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fk.PluginName) || fk.FormId == 0) return null;
                var fileName = System.IO.Path.GetFileName(fk.PluginName);
                var modType = fileName.EndsWith(".esm", StringComparison.OrdinalIgnoreCase) ? ModType.Master
                           : fileName.EndsWith(".esl", StringComparison.OrdinalIgnoreCase) ? ModType.Light
                           : ModType.Plugin;
                var mk = new ModKey(fileName, modType);
                return new FormKey(mk, fk.FormId);
            }
            catch
            {
                return null;
            }
        }
    }
}

8. 期待される改善効果
改善	効果
Fast-path 明示化	どの FormKey が成功/失敗したか Information ログで即確認可能
冗長反射の縮小	パフォーマンス改善・誤検出減少
正規化一元化	FormKey 変換の失敗要因を特定しやすくなる
追加テスト	実ゲームシナリオの解決成功を CI で検証可能
詳細ログ	resolvedToOmod=0 の継続原因（LinkCache不備 / FormKey不整合 / タイプミスマッチ）を直接分離
9. 次ステップ（最小実行順）
上記簡素化 LinkResolver を別ファイルとして導入（現行版はリネーム保持）。
WeaponOmodExtractor の確認フェーズで新 LinkResolver を強制使用（オプション設定で切替）。
ログで 1 回分の実行:
最初の 5 件 CandidateDump
LinkResolver: generic resolve MISS/SUCCESS 出力
AttachPointConfirmer: resolvedToOmod の増減確認
resolvedToOmod > 0 を確認後、Ammo 検出が 0 のままなら再帰走査強化。