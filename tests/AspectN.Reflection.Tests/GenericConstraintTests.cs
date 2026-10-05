using AspectN.Evaluation;
using Xunit;

namespace AspectN.Reflection.Tests;

public sealed class GenericConstraintTests
{
    [Fact]
    public void IsAssignableTo_TransitiveConstraint_ReachesDeclaredTypeParameter()
    {
        var system = new ReflectionTypeSystem();
        var method = typeof(Owner<string>).GetMethod(nameof(Owner<string>.M))!;
        var candidate = system.Mapper.MapType(method.GetGenericArguments()[0]);
        // CLR 返回的方法形参隶属开放定义，即使从 Owner<string> 取得 MethodInfo；
        // 其 Type 对象本身不携带封闭声明上下文，闭包仍应包含声明约束 Outer。
        var declaringDefinition = method.GetGenericArguments()[0].DeclaringMethod!.DeclaringType!;
        Assert.True(declaringDefinition.IsGenericTypeDefinition);
        var target = system.Mapper.MapType(declaringDefinition.GetGenericArguments()[0]);
        Assert.Equal(MatchStatus.Matched, system.IsAssignableTo(candidate, target).Status);
        Assert.Contains(system.GetInheritanceClosure(candidate).Ancestors,
            ancestor => system.AreEqual(ancestor, target).Status == MatchStatus.Matched);
    }

    private sealed class Owner<TOuter>
    {
        public static void M<T, U>() where T : U where U : TOuter { }
    }
}
