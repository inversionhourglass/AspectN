using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;

namespace AspectN.Tests.Evaluation;

public sealed class AccessorSelectorTests
{
    [Theory]
    [InlineData(CallableKind.PropertyGetter, MatchStatus.Matched)]
    [InlineData(CallableKind.PropertySetter, MatchStatus.Matched)]
    [InlineData(CallableKind.OrdinaryMethod, MatchStatus.NotMatched)]
    public void Match_Accessor_AcceptsPropertyAccessorsOnly(
        CallableKind kind,
        MatchStatus expected)
    {
        var property = Property("Name", Named("System", "String"));
        var candidate = Callable(kind, property);

        var result = Match("accessor(string Service.Name)", candidate);

        Assert.Equal(expected, result.Status);
    }

    [Theory]
    [InlineData("getter(string Service.Name)", CallableKind.PropertyGetter, MatchStatus.Matched)]
    [InlineData("getter(string Service.Name)", CallableKind.PropertySetter, MatchStatus.NotMatched)]
    [InlineData("setter(string Service.Name)", CallableKind.PropertyGetter, MatchStatus.NotMatched)]
    [InlineData("setter(string Service.Name)", CallableKind.PropertySetter, MatchStatus.Matched)]
    public void Match_GetterAndSetter_NarrowAccessorKind(
        string source,
        CallableKind kind,
        MatchStatus expected)
    {
        var candidate = Callable(
            kind,
            Property("Name", Named("System", "String")));

        var result = Match(source, candidate);

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public void Match_Accessor_UsesAssociatedPropertyNameAndType()
    {
        var property = Property("Name", Named("System", "String"));
        var candidate = Callable(
            CallableKind.PropertySetter,
            property,
            name: "set_Name",
            returnType: Named("System", "Void", TypeKind.Struct));

        var result = Match("accessor(string Service.Name)", candidate);

        Assert.Equal(MatchStatus.Matched, result.Status);
        Assert.Same(property, candidate.AssociatedProperty);
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
        PropertySignature property,
        string? name = null,
        TypeSignature? returnType = null)
    {
        return new CallableSignature(
            name ?? (kind == CallableKind.PropertyGetter
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
