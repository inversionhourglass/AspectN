using AspectN.Evaluation;
using AspectN.Metadata;
using Mono.Cecil;

namespace AspectN.Cecil.Tests;

public sealed class CecilTypeSystemTests
{
    [Fact]
    public void AreEqual_ExactType_ReturnsMatched()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var type = typeSystem.Mapper.MapType(module.TypeSystem.Int32);

        var result = typeSystem.AreEqual(type, type);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void IsAssignableTo_BaseClass_ReturnsMatchedOnlyInForwardDirection()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var baseType = AddClass(module, "Base", module.TypeSystem.Object);
        var derivedType = AddClass(module, "Derived", baseType);
        var mappedBase = typeSystem.Mapper.MapType(baseType);
        var mappedDerived = typeSystem.Mapper.MapType(derivedType);

        var forward = typeSystem.IsAssignableTo(mappedDerived, mappedBase);
        var reverse = typeSystem.IsAssignableTo(mappedBase, mappedDerived);

        Assert.Equal(MatchStatus.Matched, forward.Status);
        Assert.Equal(MatchStatus.NotMatched, reverse.Status);
    }

    [Fact]
    public void IsAssignableTo_Interface_ReturnsMatched()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var contract = new TypeDefinition(
            "Example",
            "IContract",
            TypeAttributes.Public |
            TypeAttributes.Interface |
            TypeAttributes.Abstract,
            module.TypeSystem.Object);
        module.Types.Add(contract);
        var implementation = AddClass(
            module,
            "Implementation",
            module.TypeSystem.Object);
        implementation.Interfaces.Add(new InterfaceImplementation(contract));

        var result = typeSystem.IsAssignableTo(
            typeSystem.Mapper.MapType(implementation),
            typeSystem.Mapper.MapType(contract));

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void IsAssignableTo_ConstructedGenericInterface_SubstitutesArguments()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var contract = AddGenericType(
            module,
            "IBox`1",
            TypeAttributes.Public |
            TypeAttributes.Interface |
            TypeAttributes.Abstract);
        var implementation = AddGenericType(
            module,
            "Box`1",
            TypeAttributes.Public | TypeAttributes.Class);
        var implementedContract = new GenericInstanceType(contract);
        implementedContract.GenericArguments.Add(
            implementation.GenericParameters[0]);
        implementation.Interfaces.Add(
            new InterfaceImplementation(implementedContract));
        var constructedImplementation = new GenericInstanceType(implementation);
        constructedImplementation.GenericArguments.Add(module.TypeSystem.String);
        var constructedContract = new GenericInstanceType(contract);
        constructedContract.GenericArguments.Add(module.TypeSystem.String);

        var result = typeSystem.IsAssignableTo(
            typeSystem.Mapper.MapType(constructedImplementation),
            typeSystem.Mapper.MapType(constructedContract));

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void IsAssignableTo_CyclicBaseGraph_TerminatesAsNotMatched()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var first = AddClass(module, "First", module.TypeSystem.Object);
        var second = AddClass(module, "Second", first);
        first.BaseType = second;
        var unrelated = AddClass(module, "Unrelated", module.TypeSystem.Object);

        var result = typeSystem.IsAssignableTo(
            typeSystem.Mapper.MapType(first),
            typeSystem.Mapper.MapType(unrelated));

        Assert.Equal(MatchStatus.NotMatched, result.Status);
    }

    [Fact]
    public void IsAssignableTo_UnresolvedAssembly_ReturnsUnknownWithResolutionDiagnostic()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var assembly = new AssemblyNameReference(
            "Missing.Contracts",
            new Version(1, 0, 0, 0));
        module.AssemblyReferences.Add(assembly);
        var unresolved = new TypeReference(
            "Missing",
            "Contract",
            module,
            assembly);

        var result = typeSystem.IsAssignableTo(
            typeSystem.Mapper.MapType(unresolved),
            typeSystem.Mapper.MapType(module.TypeSystem.Object));

        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == "AN3002");
    }

    [Fact]
    public void IsAssignableTo_ResolverException_ReturnsUnknownWithResolutionDiagnostic()
    {
        var module = ModuleDefinition.CreateModule(
            "Throwing.Resolver",
            new ModuleParameters
            {
                Kind = ModuleKind.Dll,
                AssemblyResolver = new ThrowingAssemblyResolver()
            });
        using var typeSystem = new CecilTypeSystem(module);
        var assembly = new AssemblyNameReference(
            "Broken.Contracts",
            new Version(1, 0, 0, 0));
        var unresolved = new TypeReference(
            "Broken",
            "Contract",
            module,
            assembly);

        var result = typeSystem.IsAssignableTo(
            typeSystem.Mapper.MapType(unresolved),
            typeSystem.Mapper.MapType(module.TypeSystem.Object));

        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == "AN3002");
    }

    [Fact]
    public void IsAssignableTo_CanceledToken_ThrowsOperationCanceledException()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var candidate = typeSystem.Mapper.MapType(module.TypeSystem.String);
        var target = typeSystem.Mapper.MapType(module.TypeSystem.Object);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            typeSystem.IsAssignableTo(candidate, target, cancellation.Token));
    }

    private static TypeDefinition AddClass(
        ModuleDefinition module,
        string name,
        TypeReference baseType)
    {
        var definition = new TypeDefinition(
            "Example",
            name,
            TypeAttributes.Public | TypeAttributes.Class,
            baseType);
        module.Types.Add(definition);
        return definition;
    }

    private static TypeDefinition AddGenericType(
        ModuleDefinition module,
        string name,
        TypeAttributes attributes)
    {
        var definition = new TypeDefinition(
            "Example",
            name,
            attributes,
            module.TypeSystem.Object);
        definition.GenericParameters.Add(new GenericParameter("T", definition));
        module.Types.Add(definition);
        return definition;
    }

    private sealed class ThrowingAssemblyResolver : IAssemblyResolver
    {
        public AssemblyDefinition Resolve(AssemblyNameReference name)
        {
            throw new InvalidOperationException(
                $"Resolver failed for '{name.FullName}'.");
        }

        public AssemblyDefinition Resolve(
            AssemblyNameReference name,
            ReaderParameters parameters)
        {
            throw new InvalidOperationException(
                $"Resolver failed for '{name.FullName}'.");
        }

        public void Dispose()
        {
        }
    }
}
