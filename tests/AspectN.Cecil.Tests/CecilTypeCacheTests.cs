using AspectN.Metadata;
using Mono.Cecil;

namespace AspectN.Cecil.Tests;

public sealed class CecilTypeCacheTests
{
    [Fact]
    public void MapType_LessInformativeReference_DoesNotPoisonDefinitionKind()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var reference = new TypeReference(
            "Example",
            "IContract",
            module,
            module);
        var definition = new TypeDefinition(
            "Example",
            "IContract",
            TypeAttributes.Public |
            TypeAttributes.Interface |
            TypeAttributes.Abstract,
            module.TypeSystem.Object);
        module.Types.Add(definition);

        var mappedReference = Assert.IsType<NamedTypeSignature>(
            typeSystem.Mapper.MapType(reference));
        var mappedDefinition = Assert.IsType<NamedTypeSignature>(
            typeSystem.Mapper.MapType(definition));

        Assert.NotSame(mappedReference, mappedDefinition);
        Assert.Equal(TypeKind.Interface, mappedDefinition.Identity.Kind);
    }

    [Fact]
    public void MapType_FunctionPointerType_ReturnsUnsupportedSignature()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var functionPointer = CreateFunctionPointer(
            module.TypeSystem.Int32,
            module.TypeSystem.String);

        var signature = Assert.IsType<UnsupportedTypeSignature>(
            typeSystem.Mapper.MapType(functionPointer));

        Assert.Contains(
            "FunctionPointerType",
            signature.Reason,
            StringComparison.Ordinal);
        Assert.Equal(
            "delegate*[managed]<System.String,System.Int32>",
            signature.DisplayName);
    }

    [Fact]
    public void MapType_IdenticalFunctionPointerShapes_ShareCachedSignature()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);

        var first = typeSystem.Mapper.MapType(
            CreateFunctionPointer(
                module.TypeSystem.Int32,
                module.TypeSystem.String));
        var second = typeSystem.Mapper.MapType(
            CreateFunctionPointer(
                module.TypeSystem.Int32,
                module.TypeSystem.String));

        Assert.Same(first, second);
    }

    [Fact]
    public void MapType_DistinctFunctionPointerShapes_DoNotShareCache()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);

        var first = typeSystem.Mapper.MapType(
            CreateFunctionPointer(
                module.TypeSystem.Int32,
                module.TypeSystem.String));
        var second = typeSystem.Mapper.MapType(
            CreateFunctionPointer(
                module.TypeSystem.Void,
                module.TypeSystem.Int32));

        Assert.NotSame(first, second);
        Assert.IsType<UnsupportedTypeSignature>(first);
        Assert.IsType<UnsupportedTypeSignature>(second);
    }

    [Fact]
    public void MapType_FunctionPointerGenericArgument_MapsToUnsupportedArgument()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var definition = new TypeDefinition(
            "Example",
            "Box`1",
            TypeAttributes.Public | TypeAttributes.Class,
            module.TypeSystem.Object);
        definition.GenericParameters.Add(new GenericParameter("T", definition));
        module.Types.Add(definition);
        var instance = new GenericInstanceType(definition);
        instance.GenericArguments.Add(
            CreateFunctionPointer(
                module.TypeSystem.Void,
                module.TypeSystem.Int32));

        var signature = Assert.IsType<NamedTypeSignature>(
            typeSystem.Mapper.MapType(instance));

        var argument = Assert.IsType<UnsupportedTypeSignature>(
            Assert.Single(signature.TypeArguments));
        Assert.Contains(
            "FunctionPointerType",
            argument.Reason,
            StringComparison.Ordinal);
    }

    private static FunctionPointerType CreateFunctionPointer(
        TypeReference returnType,
        params TypeReference[] parameterTypes)
    {
        var functionPointer = new FunctionPointerType
        {
            ReturnType = returnType
        };
        foreach (var parameterType in parameterTypes)
        {
            functionPointer.Parameters.Add(
                new ParameterDefinition(parameterType));
        }

        return functionPointer;
    }
}
