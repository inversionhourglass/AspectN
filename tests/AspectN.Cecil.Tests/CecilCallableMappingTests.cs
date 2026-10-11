using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using AspectN.Cecil.Tests.Fixtures;
using Mono.Cecil;

namespace AspectN.Cecil.Tests;

public sealed class CecilCallableMappingTests
{
    [Fact]
    public void MapMethod_OrdinaryMethod_MapsBasicSignature()
    {
        using var typeSystem = EmittedFixture.Open();
        var type = FixtureType(typeSystem);
        var method = FindMethod(type, "Ordinary");

        var signature = typeSystem.Mapper.MapMethod(method);

        Assert.Equal("Ordinary", signature.Name);
        Assert.Equal(MethodKind.OrdinaryMethod, signature.Kind);
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
    [InlineData(".ctor", MethodKind.InstanceConstructor)]
    [InlineData(".cctor", MethodKind.StaticConstructor)]
    [InlineData("op_Addition", MethodKind.Operator)]
    [InlineData("op_Implicit", MethodKind.Conversion)]
    public void MapMethod_SpecialMethod_MapsMethodKind(
        string methodName,
        MethodKind expectedKind)
    {
        using var typeSystem = EmittedFixture.Open();
        var method = FindMethod(FixtureType(typeSystem), methodName);

        var signature = typeSystem.Mapper.MapMethod(method);

        Assert.Equal(expectedKind, signature.Kind);
    }

    [Fact]
    public void MapMethod_AsyncAndTaskReturning_DistinguishesStateMachine()
    {
        using var typeSystem = EmittedFixture.Open();
        var type = FixtureType(typeSystem);

        var asyncMethod = typeSystem.Mapper.MapMethod(
            FindMethod(type, "LoadAsync"));
        var taskReturning = typeSystem.Mapper.MapMethod(
            FindMethod(type, "TaskReturning"));

        Assert.True(asyncMethod.IsAsync);
        Assert.False(taskReturning.IsAsync);
    }

    [Fact]
    public void MapMethod_GenericInstance_UsesEffectiveArgumentSignature()
    {
        using var typeSystem = EmittedFixture.Open();
        var definition = FindMethod(FixtureType(typeSystem), "Echo");
        var instance = new GenericInstanceMethod(definition);
        instance.GenericArguments.Add(typeSystem.Module.TypeSystem.String);

        var signature = typeSystem.Mapper.MapMethod(instance);

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

    [Theory]
    [InlineData(false, "SentinelType")]
    [InlineData(true, "PinnedType")]
    public void MapMethod_ConstructedDeclaringType_SubstitutesWrappedTypeArguments(
        bool isPinned,
        string expectedShape)
    {
        using var module = TestModuleFactory.Create();
        var definition = new TypeDefinition(
            "Example",
            "Box`1",
            TypeAttributes.Public | TypeAttributes.Class,
            module.TypeSystem.Object);
        var genericParameter = new GenericParameter("T", definition);
        definition.GenericParameters.Add(genericParameter);
        module.Types.Add(definition);

        var declaringType = new GenericInstanceType(definition);
        declaringType.GenericArguments.Add(module.TypeSystem.Int32);
        var method = new MethodReference(
            "Use",
            module.TypeSystem.Void,
            declaringType)
        {
            HasThis = true
        };
        TypeReference parameterType = isPinned
            ? new PinnedType(genericParameter)
            : new SentinelType(genericParameter);
        method.Parameters.Add(new ParameterDefinition(parameterType));
        using var typeSystem = new CecilTypeSystem(module);

        var signature = typeSystem.Mapper.MapMethod(method);

        var unsupported = Assert.IsType<UnsupportedTypeSignature>(
            Assert.Single(signature.Parameters).Type.Type);
        Assert.Equal("System.Int32", unsupported.DisplayName);
        Assert.Contains(expectedShape, unsupported.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, "SentinelType")]
    [InlineData(true, "PinnedType")]
    public void MapMethod_GenericInstance_SubstitutesWrappedMethodArguments(
        bool isPinned,
        string expectedShape)
    {
        using var module = TestModuleFactory.Create();
        var declaringType = new TypeDefinition(
            "Example",
            "Fixture",
            TypeAttributes.Public | TypeAttributes.Class,
            module.TypeSystem.Object);
        module.Types.Add(declaringType);
        var definition = new MethodDefinition(
            "Use",
            MethodAttributes.Public,
            module.TypeSystem.Void);
        declaringType.Methods.Add(definition);
        var genericParameter = new GenericParameter("T", definition);
        definition.GenericParameters.Add(genericParameter);
        TypeReference parameterType = isPinned
            ? new PinnedType(genericParameter)
            : new SentinelType(genericParameter);
        definition.Parameters.Add(new ParameterDefinition(parameterType));
        var instance = new GenericInstanceMethod(definition);
        instance.GenericArguments.Add(module.TypeSystem.String);
        using var typeSystem = new CecilTypeSystem(module);

        var signature = typeSystem.Mapper.MapMethod(instance);

        var unsupported = Assert.IsType<UnsupportedTypeSignature>(
            Assert.Single(signature.Parameters).Type.Type);
        Assert.Equal("System.String", unsupported.DisplayName);
        Assert.Contains(expectedShape, unsupported.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void MapMethod_RefKinds_MapsParametersAndReturnSeparately()
    {
        using var typeSystem = EmittedFixture.Open();
        var type = FixtureType(typeSystem);
        var parameters = typeSystem.Mapper
            .MapMethod(FindMethod(type, "RefKinds"))
            .Parameters;
        var refReturn = typeSystem.Mapper.MapMethod(
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
    public void MapMethod_PublicMemberInInternalType_PreservesDeclaredAccessibility()
    {
        using var typeSystem = EmittedFixture.Open();
        var method = FindMethod(
            EmittedFixture.FindType(
                typeSystem,
                nameof(DeclaredAccessibilityFixture)),
            "PublicMethod");

        var signature = typeSystem.Mapper.MapMethod(method);

        Assert.Equal(Accessibility.Public, signature.Accessibility);
    }

    [Fact]
    public void HasDirectAttribute_TypeCallableReturnAndParameter_MatchOwnTargets()
    {
        using var typeSystem = EmittedFixture.Open();
        var type = FixtureType(typeSystem);
        var callable = typeSystem.Mapper.MapMethod(FindMethod(type, "Marked"));
        var mappedType = typeSystem.Mapper.MapType(type);

        AssertMatched(
            typeSystem,
            AttributeTarget.ForType(mappedType),
            typeof(TypeMarkerAttribute));
        AssertMatched(
            typeSystem,
            AttributeTarget.ForMethod(callable),
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
    public void MapMethod_ConstructedDeclaringType_SubstitutesTypeArguments()
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

        var addSignature = typeSystem.Mapper.MapMethod(add);
        var parameter = Assert.IsType<NamedTypeSignature>(
            Assert.Single(addSignature.Parameters).Type.Type);
        Assert.Equal("System.String", parameter.Identity.FullMetadataName);

        var getItemSignature = typeSystem.Mapper.MapMethod(getItem);
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
