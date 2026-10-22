using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;

namespace AspectN.Tests.Evaluation;

public sealed class ConstructorSelectorTests
{
    [Theory]
    [InlineData("ctor(tasklike())", MethodKind.InstanceConstructor, "Task", false)]
    [InlineData("ctor(tasklike())", MethodKind.InstanceConstructor, "ValueTask", false)]
    [InlineData("ctor(tasklike<int>())", MethodKind.InstanceConstructor, "Task", true)]
    [InlineData("ctor(tasklike<int>())", MethodKind.InstanceConstructor, "ValueTask", true)]
    [InlineData("cctor(tasklike)", MethodKind.StaticConstructor, "Task", false)]
    [InlineData("cctor(tasklike)", MethodKind.StaticConstructor, "ValueTask", false)]
    [InlineData("cctor(tasklike<int>)", MethodKind.StaticConstructor, "Task", true)]
    [InlineData("cctor(tasklike<int>)", MethodKind.StaticConstructor, "ValueTask", true)]
    public void Match_TaskLikeConstructorDeclaringType_UsesShape(string source, MethodKind kind, string name, bool generic)
    {
        var identity = new TypeIdentity(new AssemblyIdentity("Example.Assembly"), "System.Threading.Tasks", name,
            generic ? 1 : 0, name == "Task" ? TypeKind.Class : TypeKind.Struct);
        var declaringType = generic
            ? new NamedTypeSignature(identity, new[] { Named("System", "Int32", TypeKind.Struct) }, new NamedTypeSignature(identity))
            : new NamedTypeSignature(identity);

        Assert.Equal(MatchStatus.Matched, Match(source, Callable(kind, declaringType)).Status);
    }

    [Theory]
    [InlineData("ctor(tasklike())", MethodKind.InstanceConstructor, MatchStatus.NotMatched)]
    [InlineData("cctor(tasklike)", MethodKind.StaticConstructor, MatchStatus.NotMatched)]
    [InlineData("ctor(Example.tasklike())", MethodKind.InstanceConstructor, MatchStatus.Matched)]
    [InlineData("cctor(Example.tasklike)", MethodKind.StaticConstructor, MatchStatus.Matched)]
    public void Match_LiteralTaskLikeConstructorDeclaringType_RequiresQualification(string source, MethodKind kind, MatchStatus expected)
    {
        Assert.Equal(expected, Match(source, Callable(kind, Named("Example", "tasklike"))).Status);
    }

    [Fact]
    public void Match_TaskLikeMemberPathDeclaringType_RemainsLiteralName()
    {
        Assert.Equal(MatchStatus.Matched, Match("method(* tasklike.*(..))",
            Callable(MethodKind.OrdinaryMethod, Named("", "tasklike"))).Status);
        Assert.Equal(MatchStatus.NotMatched, Match("method(* tasklike.*(..))",
            Callable(MethodKind.OrdinaryMethod, Named("System.Threading.Tasks", "Task"))).Status);
    }

    [Theory]
    [InlineData("ctor(Service(..))", MethodKind.InstanceConstructor, MatchStatus.Matched)]
    [InlineData("ctor(Service(..))", MethodKind.StaticConstructor, MatchStatus.NotMatched)]
    [InlineData("cctor(Service)", MethodKind.InstanceConstructor, MatchStatus.NotMatched)]
    [InlineData("cctor(Service)", MethodKind.StaticConstructor, MatchStatus.Matched)]
    [InlineData("ctor(Service(..))", MethodKind.OrdinaryMethod, MatchStatus.NotMatched)]
    public void Match_Constructors_UseDisjointDomains(
        string source,
        MethodKind kind,
        MatchStatus expected)
    {
        var result = Match(source, Callable(kind));

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public void Match_InstanceConstructor_MatchesParametersAndRefKinds()
    {
        var candidate = Callable(
            MethodKind.InstanceConstructor,
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
            AspectNParser.ParseMemberExpression("ctor(Foo<$T>(!$T))"), new AspectNCompilationOptions(GenericVariableMode.Capture));

        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        Assert.DoesNotContain(
            compilation.Diagnostics,
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

        var fooOfString = Generic("Example", "Foo", Named("System", "String"));
        var stringParameter = Callable(
            MethodKind.InstanceConstructor,
            fooOfString,
            new ParameterSignature(
                "value",
                0,
                new TypeUseSignature(
                    Named("System", "String"),
                    RefKind.None,
                    TypeUseKind.Parameter)));
        var intParameter = Callable(
            MethodKind.InstanceConstructor,
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
        MethodSignature candidate)
    {
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseMemberExpression(source));
        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        return pattern!.Match(candidate, new StubTypeSystem());
    }

    private static MethodSignature Callable(
        MethodKind kind,
        TypeSignature declaringType,
        params ParameterSignature[] parameters)
    {
        return new MethodSignature(
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

    private static MethodSignature Callable(
        MethodKind kind,
        params ParameterSignature[] parameters)
    {
        return new MethodSignature(
            kind == MethodKind.StaticConstructor ? ".cctor" : ".ctor",
            kind,
            Accessibility.Public,
            isStatic: kind == MethodKind.StaticConstructor,
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
