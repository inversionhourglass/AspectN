using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;

namespace AspectN.Tests.Evaluation;

public sealed class PropertySelectorTests
{
    [Theory]
    [InlineData(MethodKind.PropertyGetter, MatchStatus.Matched)]
    [InlineData(MethodKind.PropertySetter, MatchStatus.Matched)]
    [InlineData(MethodKind.OrdinaryMethod, MatchStatus.NotMatched)]
    public void Match_Property_AcceptsPropertyAccessorsOnly(
        MethodKind kind,
        MatchStatus expected)
    {
        var property = Property("Name", Named("System", "String"));
        var candidate = Callable(kind, property);

        var result = Match("property(string Service.Name)", candidate);

        Assert.Equal(expected, result.Status);
    }

    [Theory]
    [InlineData("getter(string Service.Name)", MethodKind.PropertyGetter, MatchStatus.Matched)]
    [InlineData("getter(string Service.Name)", MethodKind.PropertySetter, MatchStatus.NotMatched)]
    [InlineData("setter(string Service.Name)", MethodKind.PropertyGetter, MatchStatus.NotMatched)]
    [InlineData("setter(string Service.Name)", MethodKind.PropertySetter, MatchStatus.Matched)]
    public void Match_GetterAndSetter_NarrowAccessorKind(
        string source,
        MethodKind kind,
        MatchStatus expected)
    {
        var candidate = Callable(
            kind,
            Property("Name", Named("System", "String")));

        var result = Match(source, candidate);

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public void Match_Property_UsesAssociatedPropertyNameAndType()
    {
        var property = Property("Name", Named("System", "String"));
        var candidate = Callable(
            MethodKind.PropertySetter,
            property,
            name: "set_Name",
            returnType: Named("System", "Void", TypeKind.Struct));

        var result = Match("property(string Service.Name)", candidate);

        Assert.Equal(MatchStatus.Matched, result.Status);
        Assert.Same(property, candidate.AssociatedProperty);
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
        PropertySignature property,
        string? name = null,
        TypeSignature? returnType = null)
    {
        return new MethodSignature(
            name ?? (kind == MethodKind.PropertyGetter
                ? "get_Name"
                : "set_Name"),
            kind,
            Accessibility.Public,
            isStatic: false,
            isAsync: false,
            Named("Example", "Service"),
            new TypeUseSignature(
                returnType ?? property.PropertyType.Type,
                RefKind.None,
                TypeUseKind.Return),
            Array.Empty<ParameterSignature>(),
            Array.Empty<GenericParameterSignature>(),
            Array.Empty<TypeSignature>(),
            Array.Empty<AttributeSignature>(),
            Array.Empty<AttributeSignature>(),
            associatedProperty: property);
    }

    private static PropertySignature Property(
        string name,
        TypeSignature type)
    {
        return new PropertySignature(
            name,
            Accessibility.Public,
            isStatic: false,
            Named("Example", "Service"),
            new TypeUseSignature(type, RefKind.None, TypeUseKind.Property),
            Array.Empty<ParameterSignature>(),
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
