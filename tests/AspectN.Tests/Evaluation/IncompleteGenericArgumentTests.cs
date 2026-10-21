using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;

namespace AspectN.Tests.Evaluation;

public sealed class IncompleteGenericArgumentTests
{
    [Theory]
    [InlineData(2, "method(* M<int>(..))", MatchStatus.NotMatched)]
    [InlineData(2, "method(* M<int,string>(..))", MatchStatus.Matched)]
    [InlineData(2, "regex(.*)", MatchStatus.Matched)]
    [InlineData(0, "method(* M<int,string>(..))", MatchStatus.NotMatched)]
    [InlineData(0, "regex(.*)", MatchStatus.Matched)]
    public void Match_MethodArguments_PreservesDeclaredArity(int argumentCount, string expression, MatchStatus expected)
    {
        var assembly = new AssemblyIdentity("Example");
        GenericParameterSignature[] parameters =
        [
            new("T", GenericParameterOwnerKind.Method, "Example.Owner::M", assembly, 0),
            new("U", GenericParameterOwnerKind.Method, "Example.Owner::M", assembly, 1)
        ];
        var method = Method(Int(), parameters, Arguments(argumentCount));

        AssertResult(expression, method, expected);
    }

    [Theory]
    [InlineData(false, 2, MatchStatus.Matched)]
    [InlineData(true, 2, MatchStatus.Matched)]
    public void Match_RegexTypeArguments_RequiresCompleteConstruction(bool nested, int argumentCount, MatchStatus expected)
    {
        var outer = new TypeIdentity(new AssemblyIdentity("Example"), "Example", "Outer", 1, TypeKind.Class);
        var identity = nested
            ? new TypeIdentity(outer.Assembly, "", "Inner", 1, TypeKind.Class, outer)
            : new TypeIdentity(outer.Assembly, "Example", "Box", 2, TypeKind.Class);
        var type = new NamedTypeSignature(identity, Arguments(argumentCount));

        AssertResult("regex(.*)", Method(type), expected);
    }

    [Fact]
    public void Match_TypeDefinition_PreservesFormalArguments()
    {
        var type = new NamedTypeSignature(
            new TypeIdentity(new AssemblyIdentity("Example"), "Example", "Box", 2, TypeKind.Class));
        var engine = new AspectNEngine();
        Assert.True(engine.CompileTypePattern("Example.Box<,>").TryGetTypePattern(out var arity));
        Assert.True(engine.CompileTypePattern("Example.Box<int,string>").TryGetTypePattern(out var arguments));

        Assert.Equal(MatchStatus.Matched, arity.Match(type, new StubTypeSystem()).Status);
        Assert.Equal(MatchStatus.NotMatched, arguments.Match(type, new StubTypeSystem()).Status);
    }

    [Fact]
    public void Match_OriginalDeclarationWithCompleteCurrentSlots_FormatsDefinition()
    {
        var assembly = new AssemblyIdentity("Example");
        GenericParameterSignature[] parameters =
        [
            new("T", GenericParameterOwnerKind.Method, "Example.Owner::M", assembly, 0),
            new("U", GenericParameterOwnerKind.Method, "Example.Owner::M", assembly, 1)
        ];
        var definition = Method(Int(), parameters);
        var candidate = new MethodSignature("M", MethodKind.OrdinaryMethod, Accessibility.Public, true, false,
            definition.DeclaringType, definition.ReturnType, [], parameters, [], [], [],
            originalDefinition: definition);

        AssertResult("regex(.*)", candidate, MatchStatus.Matched);
        AssertResult("method(* M<,>(..))", candidate, MatchStatus.Matched);
    }

    [Fact]
    public void Match_CompleteArgumentsWithoutDeclarationSlots_PreservesExternalRepresentation()
    {
        AssertResult("method(* M<int,string>(..))", Method(Int(), [], Arguments(2)), MatchStatus.Matched);
        AssertResult("regex(.*)", Method(Int(), [], Arguments(2)), MatchStatus.Matched);
    }

    [Theory]
    [InlineData(false, MatchStatus.Matched)]
    [InlineData(true, MatchStatus.NotMatched)]
    public void Match_RepeatedVariable_UsesCompleteGenericArguments(bool different, MatchStatus expected)
    {
        var box = new TypeIdentity(new AssemblyIdentity("Example"), "Example", "Box", 2, TypeKind.Class);
        var first = new NamedTypeSignature(box, [Int(), String()]);
        var second = new NamedTypeSignature(box, [Int(), different ? Int() : String()]);
        var carrier = new NamedTypeSignature(
            new TypeIdentity(box.Assembly, "Example", "Carrier", 2, TypeKind.Class), [first, second]);
        Assert.True(new AspectNEngine().CompileTypePattern("Example.Carrier<$T,$T>", new AspectNCompilationOptions(null, GenericVariableMode.Capture)).TryGetTypePattern(out var pattern));
        Assert.Equal(expected, pattern.Match(carrier, new StubTypeSystem()).Status);
    }


    private static void AssertResult(string expression, MethodSignature method, MatchStatus expected)
    {
        Assert.True(new AspectNEngine().CompileMemberExpression(expression).TryGetMemberPattern(out var pattern));
        var result = pattern.Match(method, new StubTypeSystem());

        Assert.Equal(expected, result.Status);
        if (expected == MatchStatus.Unknown)
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "AN3001");
        else
            Assert.Empty(result.Diagnostics);
    }

    private static TypeSignature[] Arguments(int count) => count switch
    {
        0 => [],
        1 => [Int()],
        2 => [Int(), String()],
        _ => [Int(), String(), Int()]
    };

    private static NamedTypeSignature Int() => Named("System", "Int32", TypeKind.Struct);
    private static NamedTypeSignature String() => Named("System", "String");
    private static NamedTypeSignature Named(string space, string name, TypeKind kind = TypeKind.Class) =>
        new(new TypeIdentity(new AssemblyIdentity("Example"), space, name, 0, kind));

    private static MethodSignature Method(TypeSignature result, GenericParameterSignature[]? parameters = null,
        TypeSignature[]? arguments = null) => new(
        "M", MethodKind.OrdinaryMethod, Accessibility.Public, true, false, Named("Example", "Owner"),
        new TypeUseSignature(result, RefKind.None, TypeUseKind.Return), [], parameters ?? [],
        arguments ?? [], [], []);
}
