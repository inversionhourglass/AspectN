using System.Runtime.CompilerServices;
using AspectN.TypeSystem;

namespace AspectN.AdapterInternals;

internal static class NativeTypeLimitGuard
{
    public static HashSet<T> CreateValidated<T>() where T : class => new(ReferenceComparer<T>.Instance);

    public static void Validate<T>(T root, Func<T, int> arity, Func<T, IEnumerable<T>> children,
        Func<T, IEnumerable<T>> contexts, MappingContext resources, ISet<T> validated) where T : class
    {
        resources.SpendWork();
        if (validated.Contains(root)) return;
        var pending = new Stack<(T? Type, int Depth, IEnumerator<T>? Items, bool Declaration)>();
        var visited = new Dictionary<T, int>(ReferenceComparer<T>.Instance);
        var declarations = new HashSet<T>(ReferenceComparer<T>.Instance);
        pending.Push((root, 1, null, false));
        try
        {
            while (pending.Count > 0)
            {
                resources.SpendWork();
                var (type, depth, items, declaration) = pending.Pop();
                if (items is not null)
                {
                    if (!items.MoveNext()) { items.Dispose(); continue; }
                    pending.Push((null, depth, items, declaration));
                    if (!declaration || declarations.Add(items.Current))
                        pending.Push((items.Current, depth, null, false));
                    continue;
                }
                resources.CheckDepth(depth);
                if (visited.TryGetValue(type!, out var previous) && previous >= depth) continue;
                visited[type!] = depth;
                resources.CheckArity(arity(type!));
                // 按需枚举，宽输入不能在预算检查前把全部后代放进工作栈。
                pending.Push((null, 1, contexts(type!).GetEnumerator(), true));
                pending.Push((null, checked(depth + 1), children(type!).GetEnumerator(), false));
            }
            foreach (var type in visited.Keys) { resources.SpendWork(); validated.Add(type); }
        }
        finally
        {
            foreach (var frame in pending) frame.Items?.Dispose();
        }
    }

    public static int AddArity(int total, int own, MappingContext resources)
    {
        var result = (long)total + own;
        resources.CheckArity(result);
        return checked((int)result);
    }

    private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
    {
        public static ReferenceComparer<T> Instance { get; } = new();
        public bool Equals(T? left, T? right) => ReferenceEquals(left, right);
        public int GetHashCode(T value) => RuntimeHelpers.GetHashCode(value);
    }
}
