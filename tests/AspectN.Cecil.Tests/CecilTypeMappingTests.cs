using System.Runtime.CompilerServices;
using AspectN.Evaluation;
using AspectN.Metadata;
using Mono.Cecil;

namespace AspectN.Cecil.Tests;

public sealed class CecilTypeMappingTests
{
    [Fact]
    public void MapType_ResolvableDelegateReference_PreservesDelegateKind()
    {
        using var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var reference = module.ImportReference(typeof(EventHandler));

        var signature = Assert.IsType<NamedTypeSignature>(
            typeSystem.Mapper.MapType(reference));

        Assert.Equal(TypeKind.Delegate, signature.Identity.Kind);
    }

    [Fact]
    public void MapType_TopLevelAndNestedDefinitions_PreserveIdentityAndArity()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var outer = AddGenericType(module, "Example", "Outer`1", "T");
        var inner = new TypeDefinition(
            string.Empty,
            "Inner`2",
            TypeAttributes.NestedPublic | TypeAttributes.Class,
            module.TypeSystem.Object);
        inner.GenericParameters.Add(new GenericParameter("TKey", inner));
        inner.GenericParameters.Add(new GenericParameter("TValue", inner));
        outer.NestedTypes.Add(inner);

        var outerSignature = Assert.IsType<NamedTypeSignature>(
            typeSystem.Mapper.MapType(outer));
        var innerSignature = Assert.IsType<NamedTypeSignature>(
            typeSystem.Mapper.MapType(inner));

        Assert.Equal("Example", outerSignature.Identity.Namespace);
        Assert.Equal("Outer", outerSignature.Identity.MetadataName);
        Assert.Equal(1, outerSignature.Identity.GenericArity);
        Assert.Equal(TypeKind.Class, outerSignature.Identity.Kind);
        Assert.Equal(outerSignature.Identity, innerSignature.Identity.ContainingType);
        Assert.Equal("Inner", innerSignature.Identity.MetadataName);
        Assert.Equal(2, innerSignature.Identity.GenericArity);
        Assert.Equal(
            "Example.Outer`1/Inner`2",
            innerSignature.Identity.FullMetadataName);
    }

    [Fact]
    public void MapType_GenericInstance_RetainsDefinitionAndEffectiveArguments()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var definition = AddGenericType(module, "Example", "Box`1", "T");
        var instance = new GenericInstanceType(definition);
        instance.GenericArguments.Add(module.TypeSystem.String);

        var definitionSignature = Assert.IsType<NamedTypeSignature>(
            typeSystem.Mapper.MapType(definition));
        var instanceSignature = Assert.IsType<NamedTypeSignature>(
            typeSystem.Mapper.MapType(instance));

        Assert.Same(definitionSignature, instanceSignature.OriginalDefinition);
        var argument = Assert.IsType<NamedTypeSignature>(
            Assert.Single(instanceSignature.TypeArguments));
        Assert.Equal("System.String", argument.Identity.FullMetadataName);
        Assert.True(instanceSignature.IsConstructed);
    }

    [Fact]
    public void MapType_TypeAndMethodGenericParameters_PreserveOwnerAndPosition()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var definition = AddGenericType(module, "Example", "Box`1", "T");
        var method = new MethodDefinition(
            "Map",
            MethodAttributes.Public | MethodAttributes.Static,
            module.TypeSystem.Void);
        method.GenericParameters.Add(new GenericParameter("TResult", method));
        definition.Methods.Add(method);

        var typeParameter = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(definition.GenericParameters[0]));
        var methodParameter = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(method.GenericParameters[0]));

        Assert.Equal(GenericParameterOwnerKind.Type, typeParameter.Parameter.OwnerKind);
        Assert.Equal("Example.Box`1", typeParameter.Parameter.OwnerId);
        Assert.Equal(0, typeParameter.Parameter.Position);
        Assert.Equal(
            GenericParameterOwnerKind.Method,
            methodParameter.Parameter.OwnerKind);
        Assert.Equal("Example.Box`1.Map`1()", methodParameter.Parameter.OwnerId);
        Assert.Equal(0, methodParameter.Parameter.Position);
    }

    [Fact]
    public void MapType_OverloadedMethodGenericParameters_HaveDistinctOwnerIds()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var definition = AddClass(module, "Example", "Overloads");
        var singleParameter = new MethodDefinition(
            "Map",
            MethodAttributes.Public | MethodAttributes.Static,
            module.TypeSystem.Void);
        singleParameter.GenericParameters.Add(
            new GenericParameter("T", singleParameter));
        singleParameter.Parameters.Add(new ParameterDefinition(
            "value",
            ParameterAttributes.None,
            singleParameter.GenericParameters[0]));
        var doubleParameter = new MethodDefinition(
            "Map",
            MethodAttributes.Public | MethodAttributes.Static,
            module.TypeSystem.Void);
        doubleParameter.GenericParameters.Add(
            new GenericParameter("T", doubleParameter));
        doubleParameter.Parameters.Add(new ParameterDefinition(
            "value",
            ParameterAttributes.None,
            doubleParameter.GenericParameters[0]));
        doubleParameter.Parameters.Add(new ParameterDefinition(
            "extra",
            ParameterAttributes.None,
            module.TypeSystem.Int32));
        definition.Methods.Add(singleParameter);
        definition.Methods.Add(doubleParameter);

        var first = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(singleParameter.GenericParameters[0]));
        var second = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(doubleParameter.GenericParameters[0]));

        Assert.Equal("Example.Overloads.Map`1(!!0)", first.Parameter.OwnerId);
        Assert.Equal(
            "Example.Overloads.Map`1(!!0,System.Int32)",
            second.Parameter.OwnerId);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.AreEqual(first, second).Status);
    }

    [Fact]
    public void MapType_JaggedMultidimensionalArray_PreservesEveryRank()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var multidimensional = new ArrayType(module.TypeSystem.Int32, 2);
        var jagged = new ArrayType(multidimensional);

        var outer = Assert.IsType<ArrayTypeSignature>(
            typeSystem.Mapper.MapType(jagged));
        var inner = Assert.IsType<ArrayTypeSignature>(outer.ElementType);
        var element = Assert.IsType<NamedTypeSignature>(inner.ElementType);

        Assert.Equal(1, outer.Rank);
        Assert.Equal(2, inner.Rank);
        Assert.Equal("System.Int32", element.Identity.FullMetadataName);
    }

    [Fact]
    public void MapTypeUse_ByReferenceType_SeparatesRefKindFromElementType()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var byReference = new ByReferenceType(module.TypeSystem.Int32);

        var typeUse = typeSystem.Mapper.MapTypeUse(
            byReference,
            TypeUseKind.Parameter);

        Assert.Equal(RefKind.Ref, typeUse.RefKind);
        Assert.Equal(TypeUseKind.Parameter, typeUse.UseKind);
        Assert.Same(
            typeSystem.Mapper.MapType(module.TypeSystem.Int32),
            typeUse.Type);
    }

    [Fact]
    public void MapType_UnresolvedNamedReferences_RetainStructureAndStableCache()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var missingAssembly = new AssemblyNameReference(
            "Missing.Contracts",
            new Version(1, 2, 3, 4));
        module.AssemblyReferences.Add(missingAssembly);
        var firstReference = new TypeReference(
            "Missing",
            "Contract`1",
            module,
            missingAssembly);
        var secondReference = new TypeReference(
            "Missing",
            "Contract`1",
            module,
            missingAssembly);

        var first = Assert.IsType<NamedTypeSignature>(
            typeSystem.Mapper.MapType(firstReference));
        var second = typeSystem.Mapper.MapType(secondReference);

        Assert.Same(first, second);
        Assert.Equal("Missing.Contracts", first.Identity.Assembly.Name);
        Assert.Equal("Missing.Contract`1", first.Identity.FullMetadataName);
        var relation = typeSystem.IsAssignableTo(
            first,
            typeSystem.Mapper.MapType(module.TypeSystem.Object));
        Assert.Equal(MatchStatus.Unknown, relation.Status);
        Assert.NotEmpty(relation.Diagnostics);
    }

    [Fact]
    public void MapType_PointerType_ReturnsUnsupportedWithRelationDiagnostic()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var pointer = new PointerType(module.TypeSystem.Int32);

        var signature = Assert.IsType<UnsupportedTypeSignature>(
            typeSystem.Mapper.MapType(pointer));
        var relation = typeSystem.AreEqual(signature, signature);

        Assert.Contains("PointerType", signature.Reason, StringComparison.Ordinal);
        Assert.Equal(MatchStatus.Unknown, relation.Status);
        Assert.NotEmpty(relation.Diagnostics);
    }

    [Fact]
    public void Dispose_CachedTypes_DoesNotRetainModule()
    {
        var context = CreateDisposedContext(out var moduleReference);

        for (var attempt = 0; attempt < 10 && moduleReference.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Thread.Sleep(10);
        }

        Assert.False(moduleReference.IsAlive);
        GC.KeepAlive(context);
    }

    private static TypeDefinition AddClass(
        ModuleDefinition module,
        string @namespace,
        string name)
    {
        var definition = new TypeDefinition(
            @namespace,
            name,
            TypeAttributes.Public | TypeAttributes.Class,
            module.TypeSystem.Object);
        module.Types.Add(definition);
        return definition;
    }

    private static TypeDefinition AddGenericType(
        ModuleDefinition module,
        string @namespace,
        string name,
        string parameterName)
    {
        var definition = new TypeDefinition(
            @namespace,
            name,
            TypeAttributes.Public | TypeAttributes.Class,
            module.TypeSystem.Object);
        definition.GenericParameters.Add(
            new GenericParameter(parameterName, definition));
        module.Types.Add(definition);
        return definition;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static CecilTypeSystem CreateDisposedContext(
        out WeakReference moduleReference)
    {
        var module = TestModuleFactory.Create("Disposable.Module");
        var definition = AddGenericType(module, "Example", "Cached`1", "T");
        var context = new CecilTypeSystem(module);
        _ = context.Mapper.MapType(definition);
        moduleReference = new WeakReference(module);
        context.Dispose();
        return context;
    }
}
