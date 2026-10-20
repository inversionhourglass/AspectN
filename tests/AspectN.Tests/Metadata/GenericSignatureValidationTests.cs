using AspectN.Metadata;

namespace AspectN.Tests.Metadata;

public sealed class GenericSignatureValidationTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(2, 1)]
    [InlineData(2, 3)]
    public void NamedTypeSignature_ArgumentCountDiffersFromArity_Throws(int arity, int count)
    {
        var exception = Assert.Throws<ArgumentException>(() => new NamedTypeSignature(Identity(arity), Arguments(count)));
        Assert.Equal("typeArguments", exception.ParamName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void NamedTypeSignature_NestedArgumentCountDiffersFromTotalArity_Throws(int count)
    {
        var identity = new TypeIdentity(Assembly, "", "Inner", 1, TypeKind.Class, Identity(1));
        Assert.Throws<ArgumentException>(() => new NamedTypeSignature(identity, Arguments(count)));
        Assert.Equal(2, new NamedTypeSignature(identity, Arguments(2)).TypeArguments.Count);
    }

    [Fact]
    public void NamedTypeSignature_ExtremeNestedArity_RejectsArgumentsWithoutOverflow()
    {
        var identity = new TypeIdentity(Assembly, "", "Inner", 1, TypeKind.Class, Identity(int.MaxValue));
        Assert.Equal("typeArguments", Assert.Throws<ArgumentException>(() =>
            new NamedTypeSignature(identity, Arguments(2))).ParamName);
    }

    [Fact]
    public void NamedTypeSignature_NullArgument_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => new NamedTypeSignature(Identity(2), [Int(), null!]));
        Assert.Equal("typeArguments", exception.ParamName);
    }

    [Fact]
    public void NamedTypeSignature_InconsistentOriginalDefinition_Throws()
    {
        Assert.Equal("originalDefinition", Assert.Throws<ArgumentException>(() =>
            new NamedTypeSignature(Identity(2), Arguments(2), new NamedTypeSignature(Identity(1)))).ParamName);
        var constructed = new NamedTypeSignature(Identity(2), Arguments(2));
        Assert.Throws<ArgumentException>(() => new NamedTypeSignature(Identity(2), Arguments(2), constructed));
    }

    [Fact]
    public void NamedTypeSignature_DefinitionAndCompleteConstructions_AreAccepted()
    {
        var definition = new NamedTypeSignature(Identity(2));
        Assert.False(definition.IsConstructed);
        Assert.Equal(2, new NamedTypeSignature(Identity(2), Arguments(2), definition).TypeArguments.Count);
        var parameter = new GenericParameterTypeSignature(new GenericParameterSignature(
            "T", GenericParameterOwnerKind.Type, "Example.Holder`1", Assembly, 0));
        Assert.Same(parameter, new NamedTypeSignature(Identity(2), [Int(), parameter], definition).TypeArguments[1]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void MethodSignature_ArgumentCountDiffersFromDeclaration_Throws(int count)
    {
        Assert.Equal("genericArguments", Assert.Throws<ArgumentException>(() =>
            Method(Parameters(2), Arguments(count))).ParamName);
        var definition = Method(Parameters(2));
        Assert.Equal("genericArguments", Assert.Throws<ArgumentException>(() =>
            Method([], Arguments(count), definition)).ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void MethodSignature_DeclarationSlotsDifferFromOriginal_Throws(int count)
    {
        Assert.Equal("genericParameters", Assert.Throws<ArgumentException>(() =>
            Method(Parameters(count), original: Method(Parameters(2)))).ParamName);
    }

    [Fact]
    public void MethodSignature_ZeroArityOriginalDoesNotAuthorizeGenericConstruction_Throws()
    {
        Assert.Equal("genericArguments", Assert.Throws<ArgumentException>(() =>
            Method([], Arguments(2), Method([]))).ParamName);
        Assert.Equal("genericParameters", Assert.Throws<ArgumentException>(() =>
            Method(Parameters(2), original: Method([]))).ParamName);
    }

    [Fact]
    public void MethodSignature_ConstructedOriginalDefinition_Throws()
    {
        Assert.Equal("originalDefinition", Assert.Throws<ArgumentException>(() =>
            Method([], Arguments(2), Method([], Arguments(2)))).ParamName);
    }

    [Fact]
    public void MethodSignature_InvalidGenericSlots_Throws()
    {
        Assert.Equal("genericArguments", Assert.Throws<ArgumentException>(() => Method([], [null!])).ParamName);
        Assert.Equal("genericParameters", Assert.Throws<ArgumentException>(() => Method([null!])).ParamName);
        Assert.Throws<ArgumentException>(() => Method([Parameters(2)[1]]));
        Assert.Throws<ArgumentException>(() => Method([new GenericParameterSignature(
            "T", GenericParameterOwnerKind.Type, "Example.Box`1", Assembly, 0)]));
    }

    [Fact]
    public void MethodSignature_DefinitionAndCompleteConstructions_AreAccepted()
    {
        var parameters = Parameters(2);
        var definition = Method(parameters);
        Assert.Empty(definition.GenericArguments);
        Assert.Equal(2, Method([], Arguments(2), definition).GenericArguments.Count);
        Assert.Equal(2, Method(parameters, Arguments(2), definition).GenericArguments.Count);
        Assert.Equal(2, Method([], Arguments(2)).GenericArguments.Count);
        var parameter = new GenericParameterTypeSignature(parameters[1]);
        Assert.Same(parameter, Method([], [Int(), parameter], definition).GenericArguments[1]);
    }

    private static AssemblyIdentity Assembly => new("Example");
    private static TypeIdentity Identity(int arity) => new(Assembly, "Example", "Box", arity, TypeKind.Class);
    private static NamedTypeSignature Int() => new(new TypeIdentity(Assembly, "System", "Int32", 0, TypeKind.Struct));
    private static TypeSignature[] Arguments(int count) => Enumerable.Range(0, count).Select(_ => Int()).ToArray();
    private static GenericParameterSignature[] Parameters(int count) => Enumerable.Range(0, count)
        .Select(position => new GenericParameterSignature($"T{position}", GenericParameterOwnerKind.Method,
            "Example.Owner.M", Assembly, position)).ToArray();
    private static MethodSignature Method(GenericParameterSignature[] parameters, TypeSignature[]? arguments = null,
        MethodSignature? original = null) => new("M", MethodKind.OrdinaryMethod, Accessibility.Public, true, false,
        new NamedTypeSignature(new TypeIdentity(Assembly, "Example", "Owner", 0, TypeKind.Class)),
        new TypeUseSignature(Int(), RefKind.None, TypeUseKind.Return), [], parameters, arguments ?? [], [], [],
        originalDefinition: original);
}
