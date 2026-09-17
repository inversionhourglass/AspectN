using AspectN.Cecil.Tests.Fixtures;
using AspectN.Evaluation;
using AspectN.Metadata;
using Mono.Cecil;

namespace AspectN.Cecil.Tests;

public sealed class CecilFunctionPointerTests
{
    [Fact]
    public void MapCallable_FunctionPointerParameter_MapsToUnsupportedSignature()
    {
        using var typeSystem = EmittedFixture.Open();
        var invoke = FindMethod(typeSystem, "Invoke");

        var callable = typeSystem.Mapper.MapCallable(invoke);

        var parser = Assert.Single(
            callable.Parameters,
            parameter => parameter.Name == "parser");
        var unsupported = Assert.IsType<UnsupportedTypeSignature>(
            parser.Type.Type);
        Assert.Contains(
            "FunctionPointerType",
            unsupported.Reason,
            StringComparison.Ordinal);
        var value = Assert.Single(
            callable.Parameters,
            parameter => parameter.Name == "value");
        var namedValue = Assert.IsType<NamedTypeSignature>(value.Type.Type);
        Assert.Equal("System.String", namedValue.Identity.FullMetadataName);
    }

    [Fact]
    public void MapCallable_FunctionPointerReturn_MapsToUnsupportedSignature()
    {
        using var typeSystem = EmittedFixture.Open();
        var nullAction = FindMethod(typeSystem, "NullAction");

        var callable = typeSystem.Mapper.MapCallable(nullAction);

        var unsupported = Assert.IsType<UnsupportedTypeSignature>(
            callable.ReturnType.Type);
        Assert.Contains(
            "FunctionPointerType",
            unsupported.Reason,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MapProperty_FunctionPointerType_MapsToUnsupportedSignature()
    {
        using var typeSystem = EmittedFixture.Open();
        var fixture = EmittedFixture.FindType(
            typeSystem,
            "FunctionPointerFixture");
        var property = Assert.Single(
            fixture.Properties,
            candidate => candidate.Name == "CurrentAction");

        var signature = typeSystem.Mapper.MapProperty(property);

        var unsupported = Assert.IsType<UnsupportedTypeSignature>(
            signature.PropertyType.Type);
        Assert.Contains(
            "FunctionPointerType",
            unsupported.Reason,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MapType_FunctionPointerParameterOverloads_HaveDistinctOwnerIds()
    {
        using var typeSystem = EmittedFixture.Open();
        var fixture = EmittedFixture.FindType(
            typeSystem,
            "FunctionPointerFixture");
        var overloads = fixture.Methods
            .Where(method => method.Name == "Convert")
            .ToArray();
        Assert.Equal(2, overloads.Length);

        var mapped = overloads
            .Select(method => Assert.IsType<GenericParameterTypeSignature>(
                typeSystem.Mapper.MapType(method.GenericParameters[0])))
            .ToArray();
        var ownerIds = mapped
            .Select(signature => signature.Parameter.OwnerId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        // 函数指针参数在 owner ID 中以签名文本标记，不递归空的 ElementType，
        // 且不同函数指针形状必须区分同名同元数的泛型重载。
        Assert.Equal(
            "AspectN.Cecil.Tests.Fixtures.FunctionPointerFixture.Convert`1(" +
            "method TOutput *(System.Int32),System.Int32)",
            ownerIds[0]);
        Assert.Equal(
            "AspectN.Cecil.Tests.Fixtures.FunctionPointerFixture.Convert`1(" +
            "method TOutput *(System.String),System.String)",
            ownerIds[1]);
        Assert.All(
            mapped,
            signature => Assert.Equal(
                GenericParameterOwnerKind.Method,
                signature.Parameter.OwnerKind));
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.AreEqual(mapped[0], mapped[1]).Status);
    }

    private static MethodDefinition FindMethod(
        CecilTypeSystem typeSystem,
        string name)
    {
        var fixture = EmittedFixture.FindType(
            typeSystem,
            "FunctionPointerFixture");
        return Assert.Single(
            fixture.Methods,
            method => method.Name == name);
    }
}
