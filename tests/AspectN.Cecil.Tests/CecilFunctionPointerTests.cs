using static AspectN.Tests.Shared.OwnerIdentityFixture;
using AspectN.Cecil.Tests.Fixtures;
using AspectN.Evaluation;
using AspectN.Metadata;
using Mono.Cecil;

namespace AspectN.Cecil.Tests;

public sealed class CecilFunctionPointerTests
{
    [Fact]
    public void MapMethod_FunctionPointerParameter_MapsToUnsupportedSignature()
    {
        using var typeSystem = EmittedFixture.Open();
        var invoke = FindMethod(typeSystem, "Invoke");

        var callable = typeSystem.Mapper.MapMethod(invoke);

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
    public void MapMethod_FunctionPointerReturn_MapsToUnsupportedSignature()
    {
        using var typeSystem = EmittedFixture.Open();
        var nullAction = FindMethod(typeSystem, "NullAction");

        var callable = typeSystem.Mapper.MapMethod(nullAction);

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
        Assert.Equal(5, overloads.Length);

        var mapped = overloads
            .Select(method => Assert.IsType<GenericParameterTypeSignature>(
                typeSystem.Mapper.MapType(method.GenericParameters[0])))
            .ToArray();
        var ownerIds = mapped
            .Select(signature => signature.Parameter.OwnerId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var intForm = FrameworkType("System.Int32");
        var stringForm = FrameworkType("System.String");

        // 函数指针参数在 owner ID 中以统一文本标记（与 Reflection/Roslyn 逐字节一致），
        // 不递归空的 ElementType，且不同函数指针形状必须区分同名同元数的泛型重载；
        // 仅调用约定（managed vs cdecl）或仅返回只读性（T vs ref readonly T）不同的
        // 重载也必须区分；ref readonly 的 modreq 不得使泛型参数退化为按名编码。
        Assert.Equal(
            "AspectN.Cecil.Tests.Fixtures.FunctionPointerFixture.Convert`1(" +
            $"delegate*[cdecl]<{intForm},!!0>,{intForm})",
            ownerIds[0]);
        Assert.Equal(
            "AspectN.Cecil.Tests.Fixtures.FunctionPointerFixture.Convert`1(" +
            $"delegate*[managed]<{intForm}&,!!0&>,{intForm})",
            ownerIds[1]);
        Assert.Equal(
            "AspectN.Cecil.Tests.Fixtures.FunctionPointerFixture.Convert`1(" +
            $"delegate*[managed]<{intForm}&,!!0>,{intForm})",
            ownerIds[2]);
        Assert.Equal(
            "AspectN.Cecil.Tests.Fixtures.FunctionPointerFixture.Convert`1(" +
            $"delegate*[managed]<{intForm},!!0>,{intForm})",
            ownerIds[3]);
        Assert.Equal(
            "AspectN.Cecil.Tests.Fixtures.FunctionPointerFixture.Convert`1(" +
            $"delegate*[managed]<{stringForm},!!0>,{stringForm})",
            ownerIds[4]);
        Assert.All(
            mapped,
            signature => Assert.Equal(
                GenericParameterOwnerKind.Method,
                signature.Parameter.OwnerKind));
        var cdeclIndex = Array.FindIndex(
            mapped,
            signature => signature.Parameter.OwnerId.Contains(
                "delegate*[cdecl]",
                StringComparison.Ordinal));
        var managedIndex = Array.FindIndex(
            mapped,
            signature => signature.Parameter.OwnerId.Contains(
                $"delegate*[managed]<{intForm},",
                StringComparison.Ordinal));
        var byValueReturnIndex = Array.FindIndex(
            mapped,
            signature => signature.Parameter.OwnerId.Contains(
                $"delegate*[managed]<{intForm}&,!!0>,",
                StringComparison.Ordinal));
        var refReadonlyReturnIndex = Array.FindIndex(
            mapped,
            signature => signature.Parameter.OwnerId.Contains(
                $"delegate*[managed]<{intForm}&,!!0&>,",
                StringComparison.Ordinal));
        Assert.True(
            cdeclIndex >= 0 &&
            managedIndex >= 0 &&
            byValueReturnIndex >= 0 &&
            refReadonlyReturnIndex >= 0);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.AreEqual(mapped[cdeclIndex], mapped[managedIndex]).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.AreEqual(
                mapped[byValueReturnIndex],
                mapped[refReadonlyReturnIndex]).Status);
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
