using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;

namespace AspectN.Tests.Fuzz;

public sealed class SignatureLimitTests
{
    [Theory]
    [InlineData(63, MatchStatus.Matched)]
    [InlineData(64, MatchStatus.Unknown)]
    public void Match_ArrayStructure_Enforces64NodeDepth(int layers, MatchStatus expected)
    {
        TypeSignature candidate = Named("Int32");
        for (var index = 0; index < layers; index++) candidate = new ArrayTypeSignature(candidate, 1);

        AssertResult(MatchType("*", candidate), expected, "AN3005");
    }

    [Theory]
    [InlineData(64, MatchStatus.Matched)]
    [InlineData(65, MatchStatus.Unknown)]
    [InlineData(int.MaxValue, MatchStatus.Unknown)]
    public void Match_TypeDefinition_Enforces64GenericParameters(int arity, MatchStatus expected)
    {
        AssertResult(MatchType("*", Named("Many", arity)), expected, "AN3006");
    }

    [Theory]
    [InlineData(32, 32, MatchStatus.Matched)]
    [InlineData(32, 33, MatchStatus.Unknown)]
    [InlineData(int.MaxValue, 1, MatchStatus.Unknown)]
    public void Match_NestedType_EnforcesEffectiveGenericParameterLimit(
        int outerArity, int innerArity, MatchStatus expected)
    {
        var outer = new TypeIdentity(new AssemblyIdentity("Example"), "Example", "Outer", outerArity, TypeKind.Class);
        var inner = new NamedTypeSignature(new TypeIdentity(outer.Assembly, "", "Inner", innerArity, TypeKind.Class, outer));

        AssertResult(MatchType("Outer<?>/Inner<?>", inner), expected, "AN3006");
    }

    [Fact]
    public void Match_ExtremeNestedArity_ReturnsDiagnosticBeforeSynthesizingArguments()
    {
        var outer = new TypeIdentity(new AssemblyIdentity("Example"), "Example", "Outer", int.MaxValue, TypeKind.Class);
        var inner = new NamedTypeSignature(new TypeIdentity(outer.Assembly, "", "Inner", 1, TypeKind.Class, outer));

        AssertResult(MatchType("Outer<?>/Inner<$T>", inner), MatchStatus.Unknown, "AN3006");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Match_TooManyActualTypeArguments_CannotBypassLimit(bool regex)
    {
        var candidate = new NamedTypeSignature(Named("Many", 65).Identity, Enumerable.Repeat(Named("Int32"), 65));
        var result = regex ? MatchMethod("regex(.)", Method([], [], candidate)) : MatchType("*", candidate);

        AssertResult(result, MatchStatus.Unknown, "AN3006");
    }

    [Theory]
    [InlineData(64, false, MatchStatus.Matched)]
    [InlineData(65, false, MatchStatus.Unknown)]
    [InlineData(64, true, MatchStatus.Matched)]
    [InlineData(65, true, MatchStatus.Unknown)]
    public void Match_Method_EnforcesOwnGenericParameterLimit(int arity, bool constructed, MatchStatus expected)
    {
        var parameters = Enumerable.Range(0, arity).Select(index => new GenericParameterSignature(
            "T" + index, GenericParameterOwnerKind.Method, "Example.Fixture.M", new AssemblyIdentity("Example"), index));
        var candidate = constructed
            ? Method([], Enumerable.Repeat(Named("Int32"), arity))
            : Method(parameters, []);

        AssertResult(MatchMethod("method(* M<?>(..))", candidate), expected, "AN3006");
    }

    [Theory]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public void Compile_GenericClause_Enforces64Slots(int arity, bool valid)
    {
        var compilation = new AspectNEngine().CompileTypePattern("Many<" + new string(',', arity - 1) + ">");

        Assert.Equal(valid, compilation.TryGetTypePattern(out _));
        if (!valid) Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == "AN2011");
    }

    [Theory]
    [InlineData(32, true)]
    [InlineData(33, false)]
    public void Compile_NestedTypeClause_EnforcesEffective64Slots(int innerArity, bool valid)
    {
        var compilation = new AspectNEngine().CompileTypePattern(
            "Outer<" + new string(',', 31) + ">/Inner<" + new string(',', innerArity - 1) + ">");

        Assert.Equal(valid, compilation.TryGetTypePattern(out _));
        if (!valid) Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == "AN2011");
    }

    [Theory]
    [InlineData(false, 63, true)]
    [InlineData(false, 64, false)]
    [InlineData(true, 63, true)]
    [InlineData(true, 64, false)]
    public void Compile_TypeStructure_Enforces64NodeDepth(bool generic, int layers, bool valid)
    {
        var source = generic ? string.Concat(Enumerable.Repeat("Box<", layers)) + "int" + new string('>', layers) :
            "int" + string.Concat(Enumerable.Repeat("[]", layers));
        var compilation = new AspectNEngine().CompileTypePattern(source);

        Assert.Equal(valid, compilation.TryGetTypePattern(out _));
        if (!valid) Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == "AN1002");
    }

    [Fact]
    public void Match_TypeAndMethodEachHave64Parameters_AllowsBothGroups()
    {
        var parameters = Enumerable.Range(0, 64).Select(index => new GenericParameterSignature(
            "T" + index, GenericParameterOwnerKind.Method, "Example.Many.M", new AssemblyIdentity("Example"), index));
        var candidate = Method(parameters, [], Named("Many", 64));

        Assert.Equal(MatchStatus.Matched, MatchMethod("method(* *<?>.M<?>(..))", candidate).Status);
    }

    private static NamedTypeSignature Named(string name, int arity = 0) => new(
        new TypeIdentity(new AssemblyIdentity("Example"), "Example", name, arity, TypeKind.Class));

    private static MethodSignature Method(IEnumerable<GenericParameterSignature> parameters,
        IEnumerable<TypeSignature> arguments, TypeSignature? declaring = null) => new(
        "M", MethodKind.OrdinaryMethod, Accessibility.Public, false, false, declaring ?? Named("Fixture"),
        new TypeUseSignature(Named("Int32"), RefKind.None, TypeUseKind.Return), [], parameters, arguments, [], []);

    private static MatchResult MatchType(string source, TypeSignature candidate)
    {
        Assert.True(new AspectNEngine().CompileTypePattern(source).TryGetTypePattern(out var pattern));
        return pattern!.Match(candidate, new StubTypeSystem());
    }

    private static MatchResult MatchMethod(string source, MethodSignature candidate)
    {
        Assert.True(new AspectNEngine().CompileMemberExpression(source).TryGetMemberPattern(out var pattern));
        return pattern!.Match(candidate, new StubTypeSystem());
    }

    private static void AssertResult(MatchResult result, MatchStatus expected, string code)
    {
        Assert.Equal(expected, result.Status);
        if (expected == MatchStatus.Unknown) Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code);
    }
}
