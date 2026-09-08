using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using AspectN.Cecil.Tests.Fixtures;
using Mono.Cecil;

namespace AspectN.Cecil.Tests;

public sealed class CecilCallableMappingTests
{
    [Fact]
    public void MapCallable_OrdinaryMethod_MapsBasicSignature()
    {
        using var typeSystem = EmittedFixture.Open();
        var type = FixtureType(typeSystem);
        var method = FindMethod(type, "Ordinary");

        var signature = typeSystem.Mapper.MapCallable(method);

        Assert.Equal("Ordinary", signature.Name);
        Assert.Equal(CallableKind.OrdinaryMethod, signature.Kind);
        Assert.Equal(Accessibility.Public, signature.Accessibility);
        Assert.False(signature.IsStatic);
        Assert.False(signature.IsAsync);
        Assert.Empty(signature.Parameters);
        Assert.Equal(
            "System.Void",
            Assert.IsType<NamedTypeSignature>(signature.ReturnType.Type)
                .Identity.FullMetadataName);
    }

    [Theory]
    [InlineData(".ctor", CallableKind.InstanceConstructor)]
    [InlineData(".cctor", CallableKind.StaticConstructor)]
    [InlineData("op_Addition", CallableKind.Operator)]
    [InlineData("op_Implicit", CallableKind.Conversion)]
    public void MapCallable_SpecialMethod_MapsCallableKind(
        string methodName,
        CallableKind expectedKind)
    {
        using var typeSystem = EmittedFixture.Open();
        var method = FindMethod(FixtureType(typeSystem), methodName);

        var signature = typeSystem.Mapper.MapCallable(method);

        Assert.Equal(expectedKind, signature.Kind);
    }

    [Fact]
    public void MapCallable_AsyncAndTaskReturning_DistinguishesStateMachine()
    {
        using var typeSystem = EmittedFixture.Open();
        var type = FixtureType(typeSystem);

        var asyncMethod = typeSystem.Mapper.MapCallable(
            FindMethod(type, "LoadAsync"));
        var taskReturning = typeSystem.Mapper.MapCallable(
            FindMethod(type, "TaskReturning"));

        Assert.True(asyncMethod.IsAsync);
        Assert.False(taskReturning.IsAsync);
    }

    [Fact]
    public void MapCallable_GenericInstance_UsesEffectiveArgumentSignature()
    {
        using var typeSystem = EmittedFixture.Open();
        var definition = FindMethod(FixtureType(typeSystem), "Echo");
        var instance = new GenericInstanceMethod(definition);
        instance.GenericArguments.Add(typeSystem.Module.TypeSystem.String);

        var signature = typeSystem.Mapper.MapCallable(instance);

        Assert.NotNull(signature.OriginalDefinition);
        Assert.Equal("TMethod", Assert.Single(
            signature.OriginalDefinition!.GenericParameters).Name);
        Assert.Equal(
            "System.String",
            Assert.IsType<NamedTypeSignature>(
                Assert.Single(signature.GenericArguments))
                .Identity.FullMetadataName);
        Assert.Equal(
            "System.String",
            Assert.IsType<NamedTypeSignature>(signature.ReturnType.Type)
                .Identity.FullMetadataName);
        Assert.Equal(
            "System.String",
            Assert.IsType<NamedTypeSignature>(
                Assert.Single(signature.Parameters).Type.Type)
                .Identity.FullMetadataName);
    }

    [Fact]
    public void MapCallable_RefKinds_MapsParametersAndReturnSeparately()
    {
        using var typeSystem = EmittedFixture.Open();
        var type = FixtureType(typeSystem);
        var parameters = typeSystem.Mapper
            .MapCallable(FindMethod(type, "RefKinds"))
            .Parameters;
        var refReturn = typeSystem.Mapper.MapCallable(
            FindMethod(type, "RefReadonlyReturn"));

        Assert.Collection(
            parameters,
            parameter => Assert.Equal(RefKind.Ref, parameter.Type.RefKind),
            parameter => Assert.Equal(RefKind.Out, parameter.Type.RefKind),
            parameter => Assert.Equal(RefKind.In, parameter.Type.RefKind),
            parameter => Assert.Equal(
                RefKind.RefReadOnly,
                parameter.Type.RefKind));
        Assert.Equal(RefKind.RefReadOnly, refReturn.ReturnType.RefKind);
    }

    [Fact]
    public void MapCallable_CompositeAccessibility_IntersectsDeclaringType()
    {
        using var rawTypeSystem = EmittedFixture.Open();
        using var compositeTypeSystem = EmittedFixture.Open(
            new CecilMappingOptions(compositeAccessibility: true));
        var rawMethod = FindMethod(
            EmittedFixture.FindType(
                rawTypeSystem,
                nameof(CompositeAccessibilityFixture)),
            "PublicMethod");
        var compositeMethod = FindMethod(
            EmittedFixture.FindType(
                compositeTypeSystem,
                nameof(CompositeAccessibilityFixture)),
            "PublicMethod");

        var raw = rawTypeSystem.Mapper.MapCallable(rawMethod);
        var composite = compositeTypeSystem.Mapper.MapCallable(compositeMethod);

        Assert.Equal(Accessibility.Public, raw.Accessibility);
        Assert.Equal(Accessibility.Internal, composite.Accessibility);
    }

    [Fact]
    public void HasDirectAttribute_TypeCallableReturnAndParameter_MatchOwnTargets()
    {
        using var typeSystem = EmittedFixture.Open();
        var type = FixtureType(typeSystem);
        var callable = typeSystem.Mapper.MapCallable(FindMethod(type, "Marked"));
        var mappedType = typeSystem.Mapper.MapType(type);

        AssertMatched(
            typeSystem,
            AttributeTarget.ForType(mappedType),
            typeof(TypeMarkerAttribute));
        AssertMatched(
            typeSystem,
            AttributeTarget.ForCallable(callable),
            typeof(CallableMarkerAttribute));
        AssertMatched(
            typeSystem,
            AttributeTarget.ForReturn(callable),
            typeof(ReturnMarkerAttribute));
        AssertMatched(
            typeSystem,
            AttributeTarget.ForParameter(Assert.Single(callable.Parameters)),
            typeof(ParameterMarkerAttribute));
    }

    private static void AssertMatched(
        CecilTypeSystem typeSystem,
        AttributeTarget target,
        Type attributeType)
    {
        var definition = Assert.Single(
            typeSystem.Module.Types,
            type => type.FullName == attributeType.FullName);
        var result = typeSystem.HasDirectAttribute(
            target,
            typeSystem.Mapper.MapType(definition));
        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    private static TypeDefinition FixtureType(CecilTypeSystem typeSystem)
    {
        return EmittedFixture.FindType(typeSystem, "CallableFixture`1");
    }

    [Fact]
    public void MapCallable_ConstructedDeclaringType_SubstitutesTypeArguments()
    {
        // 经 ImportReference 导入 List<string> 的成员后，声明类型泛型参数必须替换为实参，
        // 而不是残留开放泛型参数 T。
        var module = ModuleDefinition.ReadModule(
            typeof(List<>).Assembly.Location);
        using var typeSystem = new CecilTypeSystem(module);
        var add = module.ImportReference(
            typeof(List<string>).GetMethod("Add")!);
        var getItem = module.ImportReference(
            typeof(List<string>).GetProperty("Item")!.GetMethod!);

        var addSignature = typeSystem.Mapper.MapCallable(add);
        var parameter = Assert.IsType<NamedTypeSignature>(
            Assert.Single(addSignature.Parameters).Type.Type);
        Assert.Equal("System.String", parameter.Identity.FullMetadataName);

        var getItemSignature = typeSystem.Mapper.MapCallable(getItem);
        var returnType = Assert.IsType<NamedTypeSignature>(
            getItemSignature.ReturnType.Type);
        Assert.Equal("System.String", returnType.Identity.FullMetadataName);

        // 关联属性（索引器）也必须携带封闭声明类型上下文：属性类型不能残留开放泛型参数 T。
        Assert.NotNull(getItemSignature.AssociatedProperty);
        var propertyType = Assert.IsType<NamedTypeSignature>(
            getItemSignature.AssociatedProperty!.PropertyType.Type);
        Assert.Equal("System.String", propertyType.Identity.FullMetadataName);

        var compilation = AspectN.AspectNCompilation.Create(
            AspectN.AspectNParser.ParseMemberExpression(
                "method(void System.Collections.Generic.List<string>.Add(string))"));
        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        Assert.Equal(
            MatchStatus.Matched,
            pattern!.Match(addSignature, typeSystem).Status);
        var getterCompilation = AspectN.AspectNCompilation.Create(
            AspectN.AspectNParser.ParseMemberExpression(
                "getter(string System.Collections.Generic.List<string>.Item)"));
        Assert.True(getterCompilation.TryGetMemberPattern(out var getterPattern));
        Assert.Equal(
            MatchStatus.Matched,
            getterPattern!.Match(getItemSignature, typeSystem).Status);
    }

    private static MethodDefinition FindMethod(
        TypeDefinition type,
        string name)
    {
        return Assert.Single(type.Methods, method => method.Name == name);
    }
}
