using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;

namespace AspectN.Tests.Evaluation;

public sealed class ConstructorSelectorTests
{
    [Theory]
    [InlineData("ctor(Service(..))", CallableKind.InstanceConstructor, MatchStatus.Matched)]
    [InlineData("ctor(Service(..))", CallableKind.StaticConstructor, MatchStatus.NotMatched)]
    [InlineData("cctor(Service)", CallableKind.InstanceConstructor, MatchStatus.NotMatched)]
    [InlineData("cctor(Service)", CallableKind.StaticConstructor, MatchStatus.Matched)]
    [InlineData("ctor(Service(..))", CallableKind.OrdinaryMethod, MatchStatus.NotMatched)]
    public void Match_Constructors_UseDisjointDomains(
        string source,
        CallableKind kind,
        MatchStatus expected)
    {
        var result = Match(source, Callable(kind));

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public void Match_InstanceConstructor_MatchesParametersAndRefKinds()
    {
        var candidate = Callable(
            CallableKind.InstanceConstructor,
            new ParameterSignature(
                "name",
                0,
                new TypeUseSignature(
                    Named("System", "String"),
                    RefKind.None,
                    TypeUseKind.Parameter)),
            new ParameterSignature(
                "value",
                1,
                new TypeUseSignature(
                    Named("System", "Int32", TypeKind.Struct),
                    RefKind.Ref,
                    TypeUseKind.Parameter)));

        var result = Match("ctor(Service(string, ref int))", candidate);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Bind_Constructor_DeclaringTypeBindsVariablesBeforeParameters()
    {
        // 声明类型 Foo<$T> 先正向引入 $T，参数内 !$T 只是引用；该模式必须合法编译。
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseMemberExpression("ctor(Foo<$T>(!$T))"));

        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        Assert.DoesNotContain(
            compilation.Diagnostics,
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

        var fooOfString = Generic("Example", "Foo", Named("System", "String"));
        var stringParameter = Callable(
            CallableKind.InstanceConstructor,
            fooOfString,
            new ParameterSignature(
                "value",
                0,
                new TypeUseSignature(
                    Named("System", "String"),
                    RefKind.None,
                    TypeUseKind.Parameter)));
        var intParameter = Callable(
            CallableKind.InstanceConstructor,
            fooOfString,
            new ParameterSignature(
                "value",
                0,
                new TypeUseSignature(
                    Named("System", "Int32", TypeKind.Struct),
                    RefKind.None,
                    TypeUseKind.Parameter)));

        Assert.Equal(
            MatchStatus.NotMatched,
            pattern!.Match(stringParameter, new StubTypeSystem()).Status);
        Assert.Equal(
            MatchStatus.Matched,
            pattern!.Match(intParameter, new StubTypeSystem()).Status);
    }

    private static NamedTypeSignature Generic(
        string @namespace,
        string name,
        params TypeSignature[] arguments)
    {
        var identity = new TypeIdentity(
            new AssemblyIdentity("Example.Assembly"),
            @namespace,
            name,
            arguments.Length,
            TypeKind.Class);
        return new NamedTypeSignature(
            identity,
            arguments,
            new NamedTypeSignature(identity));
    }

    private static MatchResult Match(
        string source,
        CallableSignature candidate)
    {
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseMemberExpression(source));
        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        return pattern!.Match(candidate, new StubTypeSystem());
    }

    private static CallableSignature Callable(
        CallableKind kind,
        TypeSignature declaringType,
        params ParameterSignature[] parameters)
    {
        return new CallableSignature(
            ".ctor",
            kind,
            Accessibility.Public,
            isStatic: false,
            isAsync: false,
            declaringType,
            new TypeUseSignature(
                Named("System", "Void", TypeKind.Struct),
                RefKind.None,
                TypeUseKind.Return),
            parameters,
            Array.Empty<GenericParameterSignature>(),
            Array.Empty<TypeSignature>(),
            Array.Empty<AttributeSignature>(),
            Array.Empty<AttributeSignature>());
    }

    private static CallableSignature Callable(
        CallableKind kind,
        params ParameterSignature[] parameters)
    {
        return new CallableSignature(
            kind == CallableKind.StaticConstructor ? ".cctor" : ".ctor",
            kind,
            Accessibility.Public,
            isStatic: kind == CallableKind.StaticConstructor,
            isAsync: false,
            Named("Example", "Service"),
            new TypeUseSignature(
                Named("System", "Void", TypeKind.Struct),
                RefKind.None,
                TypeUseKind.Return),
            parameters,
            Array.Empty<GenericParameterSignature>(),
            Array.Empty<TypeSignature>(),
            Array.Empty<AttributeSignature>(),
            Array.Empty<AttributeSignature>());
    }

    private static NamedTypeSignature Named(
        string @namespace,
        string name,
        TypeKind kind = TypeKind.Class)
    {
        return new NamedTypeSignature(
            new TypeIdentity(
                new AssemblyIdentity("Example.Assembly"),
                @namespace,
                name,
                0,
                kind));
    }
}
