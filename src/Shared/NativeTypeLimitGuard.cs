using System.Runtime.CompilerServices;
using AspectN.Diagnostics;

namespace AspectN.AdapterInternals;

internal static class NativeTypeLimitGuard
{
    public static void Validate<T>(T root, Func<T, int> arity, Func<T, IEnumerable<T>> children,
        Func<T, IEnumerable<T>> contexts) where T : class
    {
        var pending = new Stack<(T Type, int Depth)>();
        var visited = new Dictionary<T, int>(ReferenceComparer<T>.Instance);
        var declarations = new HashSet<T>(ReferenceComparer<T>.Instance);
        pending.Push((root, 1));
        while (pending.Count > 0)
        {
            var (type, depth) = pending.Pop();
            CheckDepth(depth);
            if (visited.TryGetValue(type, out var previous) && previous >= depth) continue;
            visited[type] = depth;
            CheckArity(arity(type));
            foreach (var child in children(type)) pending.Push((child, depth + 1));
            // owner 是身份上下文，不是结构子节点；独立检查并去重，避免普通泛型声明的回环。
            foreach (var declaration in contexts(type))
                if (declarations.Add(declaration)) pending.Push((declaration, 1));
        }
    }

    public static void CheckDepth(int depth)
    {
        if (depth > AspectNLimits.MaximumTypeDepth)
            throw new AspectNLimitException(new AspectNDiagnostic(DiagnosticCodes.MetadataDepthExceeded,
                DiagnosticSeverity.Warning, "The type structure exceeds the supported depth of 64 levels."));
    }

    public static void CheckArity(int arity)
    {
        if (arity > AspectNLimits.MaximumGenericParameters)
            throw new AspectNLimitException(new AspectNDiagnostic(DiagnosticCodes.MetadataGenericArityExceeded,
                DiagnosticSeverity.Warning, "The effective type arity or method generic arity exceeds the supported limit of 64 parameters."));
    }

    public static int AddArity(int total, int own)
    {
        CheckArity(own);
        if (own > AspectNLimits.MaximumGenericParameters - total) CheckArity(AspectNLimits.MaximumGenericParameters + 1);
        return total + own;
    }

    private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
    {
        public static ReferenceComparer<T> Instance { get; } = new();
        public bool Equals(T? left, T? right) => ReferenceEquals(left, right);
        public int GetHashCode(T value) => RuntimeHelpers.GetHashCode(value);
    }
}
