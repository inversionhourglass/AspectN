using AspectN.Evaluation;
using AspectN.Metadata;
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

    [Fact]
    public void MapMethod_ClosedTransitiveConstraints_PreservesIndependentQueryContexts()
    {
        var system = new ReflectionTypeSystem();
        var stringMethod = system.Mapper.MapMethod(typeof(Owner<string>).GetMethod("M")!);
        var intMethod = system.Mapper.MapMethod(typeof(Owner<int>).GetMethod("M")!);
        var stringType = system.Mapper.MapType(typeof(string));
        var intType = system.Mapper.MapType(typeof(int));

        Assert.Equal(MatchStatus.Matched, system.AreEqual(stringMethod.ReturnType.Type, intMethod.ReturnType.Type).Status);
        Assert.Equal(MatchStatus.Matched, system.IsAssignableTo(stringMethod.ReturnType.Type, stringType).Status);
        Assert.Equal(MatchStatus.NotMatched, system.IsAssignableTo(stringMethod.ReturnType.Type, intType).Status);
        Assert.Equal(MatchStatus.Matched, system.IsAssignableTo(intMethod.ReturnType.Type, intType).Status);
        Assert.Equal(MatchStatus.NotMatched, system.IsAssignableTo(intMethod.ReturnType.Type, stringType).Status);
        Assert.Equal(MatchStatus.Matched, system.IsAssignableTo(stringMethod.ReturnType.Type, stringType).Status);
        Assert.Contains(system.GetInheritanceClosure(stringMethod.ReturnType.Type).Ancestors,
            ancestor => system.AreEqual(ancestor, stringType).Status == MatchStatus.Matched);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MapMethod_ForeignGenericArgument_PreservesItsOwnConstraintOwner(bool nestedArgument)
    {
        var system = new ReflectionTypeSystem();
        var foreign = typeof(Foreign<,>).GetGenericArguments()[0];
        var argument = nestedArgument ? typeof(List<>).MakeGenericType(foreign) : foreign;
        var method = typeof(ContextOwner<string>).GetMethod("M")!.MakeGenericMethod(argument);
        var candidate = system.Mapper.MapMethod(method);
        var objectType = system.Mapper.MapType(typeof(object));
        var stringType = system.Mapper.MapType(typeof(string));

        foreach (var use in new[] { candidate.GenericArguments[0], candidate.ReturnType.Type, candidate.Parameters[0].Type.Type })
        {
            var parameter = use is NamedTypeSignature named ? named.TypeArguments[0] : use;
            Assert.Equal(MatchStatus.Matched, system.IsAssignableTo(parameter, objectType).Status);
            Assert.Equal(MatchStatus.NotMatched, system.IsAssignableTo(parameter, stringType).Status);
            Assert.Contains(system.GetInheritanceClosure(parameter).Ancestors,
                ancestor => system.AreEqual(ancestor, objectType).Status == MatchStatus.Matched);
        }
    }

    [Theory]
    [InlineData("Outer", typeof(string), typeof(int))]
    [InlineData("Inner", typeof(int), typeof(string))]
    public void MapMethod_NestedClosedOwner_SubstitutesCorrectFlattenedSlot(string methodName, Type expected, Type other)
    {
        var system = new ReflectionTypeSystem();
        var candidate = system.Mapper.MapMethod(typeof(NestedOwner<string>.Nested<int>).GetMethod(methodName)!);

        Assert.Equal(MatchStatus.Matched,
            system.IsAssignableTo(candidate.ReturnType.Type, system.Mapper.MapType(expected)).Status);
        Assert.Equal(MatchStatus.NotMatched,
            system.IsAssignableTo(candidate.ReturnType.Type, system.Mapper.MapType(other)).Status);
    }

    private sealed class Owner<TOuter>
    {
        public static T M<T, U>(T value) where T : U where U : TOuter => value;
    }

    private sealed class ContextOwner<TOuter>
    {
        public static V M<V>(V value) => value;
    }

    private sealed class Foreign<T, U> where T : U;

    private sealed class NestedOwner<TOuter>
    {
        public sealed class Nested<TInner>
        {
            public static V Outer<V>(V value) where V : TOuter => value;
            public static V Inner<V>(V value) where V : TInner => value;
        }
    }
}
