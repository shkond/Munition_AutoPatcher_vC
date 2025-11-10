using Mutagen.Bethesda.Plugins.Cache;

namespace MunitionAutoPatcher.Services.Interfaces
{
    public interface ILinkResolver
    {
        ILinkCache? LinkCache { get; }
        bool TryResolve(object linkLike, out object? result);
        bool TryResolve<TGetter>(object linkLike, out TGetter? result) where TGetter : class?;
        object? ResolveByKey(Models.FormKey key);
    }
}
