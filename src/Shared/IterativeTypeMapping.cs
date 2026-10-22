using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN.AdapterInternals;

internal static class IterativeTypeMapping
{
    public static TypeSignature Map<T>(T root, Func<T, TypeSignature?> find,
        Func<T, IEnumerable<T>> dependencies, Func<T, TypeSignature> create,
        Func<T, Func<TypeSignature>, TypeSignature> publish, MappingContext resources) where T : class
    {
        var pending = new Stack<(T Type, IEnumerator<T>? Items)>();
        var completed = new List<T>();
        pending.Push((root, null));
        try
        {
            while (pending.Count > 0)
            {
                resources.SpendWork();
                var (type, items) = pending.Pop();
                if (items is not null)
                {
                    if (items.MoveNext())
                    {
                        pending.Push((type, items));
                        pending.Push((items.Current, null));
                    }
                    else
                    {
                        items.Dispose();
                        publish(type, () => create(type));
                    }
                    continue;
                }
                completed.Add(type);
                if (find(type) is not null) continue;
                pending.Push((type, dependencies(type).GetEnumerator()));
            }
            return find(root)!;
        }
        finally
        {
            foreach (var frame in pending) frame.Items?.Dispose();
            // 弱缓存的原生子类型在父节点构造结束前必须保持存活。
            GC.KeepAlive(completed);
        }
    }
}
