using AspectN.Cecil.Tests.Fixtures;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Mono.Cecil;

namespace AspectN.Cecil.Tests;

public sealed class CecilUnqualifiedTypeTests
{
    [Fact]
    public void IsAssignableTo_UnqualifiedTarget_IgnoresAssemblyAndNamespace()
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
        var implementation = new TypeDefinition(
            "Example",
            "Implementation",
            TypeAttributes.Public | TypeAttributes.Class,
            module.TypeSystem.Object);
        implementation.Interfaces.Add(new InterfaceImplementation(contract));
        module.Types.Add(implementation);

        var result = typeSystem.IsAssignableTo(
            typeSystem.Mapper.MapType(implementation),
            Unqualified(
                typeSystem.Mapper.MapType(contract),
                omitNamespace: true));

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Match_UnqualifiedAssignableDeclaringType_MatchesInterfaceImplementation()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var contract = new TypeDefinition(
            "PatternUsage",
            "InterfaceA",
            TypeAttributes.Public |
            TypeAttributes.Interface |
            TypeAttributes.Abstract,
            module.TypeSystem.Object);
        module.Types.Add(contract);
        var implementation = new TypeDefinition(
            "PatternUsage",
            "ClsA",
            TypeAttributes.Public | TypeAttributes.Class,
            module.TypeSystem.Object);
        implementation.Interfaces.Add(new InterfaceImplementation(contract));
        var constructor = new MethodDefinition(
            ".ctor",
            MethodAttributes.Public |
            MethodAttributes.HideBySig |
            MethodAttributes.SpecialName |
            MethodAttributes.RTSpecialName,
            module.TypeSystem.Void);
        implementation.Methods.Add(constructor);
        module.Types.Add(implementation);

        var compilation = new AspectNEngine().CompileMemberExpression(
            "execution(* InterfaceA+.*(..))");
        Assert.True(compilation.TryGetMemberPattern(out var pattern));

        var result = pattern!.Match(
            typeSystem.Mapper.MapMethod(constructor),
            typeSystem);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void HasDirectAttribute_UnqualifiedType_IgnoresAssemblyIdentity()
    {
        using var typeSystem = EmittedFixture.Open();
        var fixture = EmittedFixture.FindType(
            typeSystem,
            typeof(CallableFixture<>).Name);
        var method = Assert.Single(
            fixture.Methods,
            candidate => candidate.Name == "Marked");
        var callable = typeSystem.Mapper.MapMethod(method);
        var actualAttribute = Assert.Single(
            callable.Attributes,
            attribute => Name(attribute.AttributeType).EndsWith(
                "CallableMarkerAttribute",
                StringComparison.Ordinal));

        var result = typeSystem.HasDirectAttribute(
            AttributeTarget.ForMethod(callable),
            Unqualified(actualAttribute.AttributeType));

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void HasDirectAttribute_UnknownThenMismatch_ReturnsNotMatched()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var identity = new TypeIdentity(
            new AssemblyIdentity("Example.Assembly"),
            "Example",
            "MarkerAttribute",
            2,
            TypeKind.Class);
        var definition = new NamedTypeSignature(identity);
        var actual = new NamedTypeSignature(
            identity,
            new TypeSignature[]
            {
                new UnsupportedTypeSignature("missing", "test"),
                Named("String")
            },
            definition);
        var expected = new NamedTypeSignature(
            identity,
            new TypeSignature[]
            {
                new UnsupportedTypeSignature("missing", "test"),
                Named("Int32", TypeKind.Struct)
            },
            definition);
        var callable = new MethodSignature(
            "Run",
            MethodKind.OrdinaryMethod,
            Accessibility.Public,
            isStatic: false,
            isAsync: false,
            Named("Service"),
            new TypeUseSignature(
                Named("Void", TypeKind.Struct),
                RefKind.None,
                TypeUseKind.Return),
            Array.Empty<ParameterSignature>(),
            Array.Empty<GenericParameterSignature>(),
            Array.Empty<TypeSignature>(),
            new[]
            {
                new AttributeSignature(
                    actual,
                    AttributeTargetKind.Method)
            },
            Array.Empty<AttributeSignature>());

        var result = typeSystem.HasDirectAttribute(
            AttributeTarget.ForMethod(callable),
            expected);

        Assert.Equal(MatchStatus.NotMatched, result.Status);
    }

    private static NamedTypeSignature Unqualified(
        TypeSignature type,
        bool omitNamespace = false)
    {
        var named = Assert.IsType<NamedTypeSignature>(type);
        var identity = named.Identity;
        return new NamedTypeSignature(
            new TypeIdentity(
                new AssemblyIdentity(string.Empty),
                omitNamespace ? string.Empty : identity.Namespace,
                identity.MetadataName,
                identity.GenericArity,
                identity.Kind,
                identity.ContainingType));
    }

    private static string Name(TypeSignature type)
    {
        return Assert.IsType<NamedTypeSignature>(type)
            .Identity.FullMetadataName;
    }

    private static NamedTypeSignature Named(
        string name,
        TypeKind kind = TypeKind.Class)
    {
        return new NamedTypeSignature(
            new TypeIdentity(
                new AssemblyIdentity("System.Private.CoreLib"),
                "System",
                name,
                0,
                kind));
    }
}
