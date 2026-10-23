using System.Reflection;

namespace AspectN.MappingDepthProbe;

// 现代宿主也要定点验证旧运行时所需的扫描算法；反射入口仅属于测试探针。
internal static class RuntimeTypeFactsProbe
{
    private const string TYPE_NAME = "AspectN.Reflection.Internal.RuntimeTypeFacts";
    private const string METHOD_NAME = "ContainsIntPtr";
    private const string EXPECTED_SIGNATURE = "non-public, non-generic static bool ContainsIntPtr(System.Type)";

    internal static MethodInfo Resolve(Assembly assembly)
    {
        var facts = assembly.GetType(TYPE_NAME);
        if (facts is null)
            throw new InvalidOperationException(
                $"Probe could not locate type '{TYPE_NAME}' in assembly '{assembly.FullName}'. Expected entry: {EXPECTED_SIGNATURE}.");

        var candidates = facts.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.Name == METHOD_NAME)
            .ToArray();
        // 比较精确形参类型，避免默认绑定器接纳 object 等重载；只允许唯一的完整签名匹配。
        var matches = candidates.Where(method => method.IsStatic && !method.IsPublic && !method.IsGenericMethod &&
                method.ReturnType == typeof(bool) && method.GetParameters() is [{ ParameterType: var parameterType }] &&
                parameterType == typeof(Type))
            .ToArray();
        if (matches.Length == 1) return matches[0];

        var actual = candidates.Length == 0 ? "<none>" : string.Join("; ", candidates.Select(method =>
            $"{(method.IsPublic ? "public" : "non-public")} {(method.IsStatic ? "static" : "instance")} {method}"));
        throw new InvalidOperationException(
            $"Probe could not uniquely locate entry '{TYPE_NAME}.{METHOD_NAME}' in assembly '{assembly.FullName}'. " +
            $"Expected: {EXPECTED_SIGNATURE}; matching entries: {matches.Length}; actual candidates: {actual}.");
    }
}
