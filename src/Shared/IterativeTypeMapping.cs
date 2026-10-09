using AspectN.Metadata;

namespace AspectN.AdapterInternals;

internal static class IterativeTypeMapping
{
    public static TypeSignature Map<T>(T root, Func<T, TypeSignature?> find,
        Func<T, IEnumerable<T>> dependencies, Func<T, TypeSignature> create,
        Func<T, Func<TypeSignature>, TypeSignature> publish) where T : class
    {
        var pending = new Stack<(T Type, bool Expanded)>();
        var completed = new List<T>();
        pending.Push((root, false));
        while (pending.Count > 0)
        {
            var (type, expanded) = pending.Pop();
            completed.Add(type);
            if (find(type) is not null) continue;
            if (expanded)
            {
                publish(type, () => create(type));
                continue;
            }
            // 先映射依赖再构造父节点；create 中的子类型查询仅命中缓存，不再递归展开。
            pending.Push((type, true));
            foreach (var dependency in dependencies(type)) pending.Push((dependency, false));
        }
        var result = find(root)!;
        // 弱缓存的原生子类型在父节点构造结束前必须保持存活。
        GC.KeepAlive(completed);
        return result;
    }
}
