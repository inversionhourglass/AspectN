using AspectN.Metadata;

namespace AspectN.Tests.Metadata;

public sealed class ParameterPositionTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 0)]
    [InlineData(0, 2)]
    public void MethodSignature_InvalidParameterPositions_RejectsInput(int first, int second)
    {
        var exception = Assert.Throws<ArgumentException>(() => Method([Parameter(first), Parameter(second)]));

        Assert.Equal("parameters", exception.ParamName);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 0)]
    [InlineData(0, 2)]
    public void PropertySignature_InvalidParameterPositions_RejectsInput(int first, int second)
    {
        var exception = Assert.Throws<ArgumentException>(() => new PropertySignature(
            "Item", Accessibility.Public, false, Type(), Use(TypeUseKind.Property),
            [Parameter(first), Parameter(second)], []));

        Assert.Equal("parameters", exception.ParamName);
    }

    [Fact]
    public void MethodSignature_OrderedParameters_CopiesWithoutReordering()
    {
        var parameters = new[] { Parameter(0), Parameter(1) };
        var method = Method(parameters);
        Array.Reverse(parameters);

        Assert.Collection(method.Parameters,
            parameter => Assert.Equal(0, parameter.Position),
            parameter => Assert.Equal(1, parameter.Position));
    }

    private static MethodSignature Method(IEnumerable<ParameterSignature> parameters) => new(
        "M", MethodKind.OrdinaryMethod, Accessibility.Public, false, false,
        Type(), Use(TypeUseKind.Return), parameters, [], [], [], []);

    private static ParameterSignature Parameter(int position) => new(
        $"p{position}", position, Use(TypeUseKind.Parameter), [], false);

    private static NamedTypeSignature Type() => new(
        new TypeIdentity(new AssemblyIdentity("Example"), "Example", "Item", 0, TypeKind.Class));

    private static TypeUseSignature Use(TypeUseKind kind) => new(Type(), RefKind.None, kind);
}
