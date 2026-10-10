using AspectN.Metadata;

namespace AspectN.Tests.Metadata;

public sealed class SignatureTests
{
    [Fact]
    public void TypeIdentity_DifferentAssembly_IsNotEqual()
    {
        var first = Identity("Contracts", "1.0.0.0", "Example", "Service");
        var second = Identity("Contracts", "2.0.0.0", "Example", "Service");

        Assert.NotEqual(first, second);
        Assert.NotEqual(first.Assembly, second.Assembly);
    }

    [Fact]
    public void TypeIdentity_NestedGenericType_PreservesContainingChainAndArity()
    {
        var outer = Identity("Contracts", "1.0.0.0", "Example", "Outer", 1);
        var inner = new TypeIdentity(
            outer.Assembly,
            string.Empty,
            "Inner",
            2,
            TypeKind.Class,
            outer);

        Assert.Same(outer, inner.ContainingType);
        Assert.Equal(2, inner.GenericArity);
        Assert.Equal("Example.Outer`1/Inner`2", inner.FullMetadataName);
    }

    [Fact]
    public void NamedTypeSignature_ConstructedType_CopiesArgumentsAndRetainsDefinition()
    {
        var listIdentity = Identity(
            "System.Private.CoreLib",
            "10.0.0.0",
            "System.Collections.Generic",
            "List",
            1);
        var definition = new NamedTypeSignature(listIdentity);
        var arguments = new List<TypeSignature> { SystemType("String") };
        var constructed = new NamedTypeSignature(
            listIdentity,
            arguments,
            definition);

        arguments.Clear();

        Assert.Single(constructed.TypeArguments);
        Assert.Same(definition, constructed.OriginalDefinition);
        Assert.True(constructed.IsConstructed);
    }

    [Fact]
    public void TypeSignature_Algebra_DistinguishesArrayGenericParameterAndUnsupported()
    {
        var element = SystemType("Int32", TypeKind.Struct);
        var array = new ArrayTypeSignature(element, 2);
        var parameter = new GenericParameterTypeSignature(
            new GenericParameterSignature(
                "T",
                GenericParameterOwnerKind.Type,
                "Example.Container`1",
                new AssemblyIdentity("Example.Assembly"),
                0));
        var unsupported = new UnsupportedTypeSignature(
            "delegate*<void>",
            "Function pointers are outside AspectN 1.0.");

        Assert.Equal(2, array.Rank);
        Assert.Same(element, array.ElementType);
        Assert.Equal(0, parameter.Parameter.Position);
        Assert.Equal(GenericParameterOwnerKind.Type, parameter.Parameter.OwnerKind);
        Assert.Equal("delegate*<void>", unsupported.DisplayName);
        Assert.IsNotType<ArrayTypeSignature>(parameter);
        Assert.IsNotType<GenericParameterTypeSignature>(unsupported);
    }

    [Fact]
    public void TypeUseSignature_RefKind_IsSeparateFromReferencedType()
    {
        var type = SystemType("Int32", TypeKind.Struct);
        var use = new TypeUseSignature(
            type,
            RefKind.Out,
            TypeUseKind.Parameter);

        Assert.Same(type, use.Type);
        Assert.Equal(RefKind.Out, use.RefKind);
        Assert.Equal(TypeUseKind.Parameter, use.UseKind);
    }

    [Fact]
    public void MethodSignature_PropertyAccessor_IsSeparateAndLinked()
    {
        var declaringType = SystemType("String");
        var property = new PropertySignature(
            "Length",
            Accessibility.Public,
            false,
            declaringType,
            new TypeUseSignature(
                SystemType("Int32", TypeKind.Struct),
                RefKind.None,
                TypeUseKind.Property),
            Array.Empty<ParameterSignature>(),
            Array.Empty<AttributeSignature>());
        var getter = new MethodSignature(
            "get_Length",
            MethodKind.PropertyGetter,
            Accessibility.Public,
            false,
            false,
            declaringType,
            property.PropertyType,
            Array.Empty<ParameterSignature>(),
            Array.Empty<GenericParameterSignature>(),
            Array.Empty<TypeSignature>(),
            Array.Empty<AttributeSignature>(),
            Array.Empty<AttributeSignature>(),
            typeAttributes: null,
            null,
            property);

        Assert.Same(property, getter.AssociatedProperty);
        Assert.Equal(MethodKind.PropertyGetter, getter.Kind);
        Assert.Equal("Length", property.Name);
        Assert.NotEqual(property.GetType(), getter.GetType());
    }

    [Fact]
    public void MethodKind_Constructors_AreDistinctFromOrdinaryMethod()
    {
        Assert.NotEqual(MethodKind.OrdinaryMethod, MethodKind.InstanceConstructor);
        Assert.NotEqual(MethodKind.OrdinaryMethod, MethodKind.StaticConstructor);
        Assert.NotEqual(MethodKind.InstanceConstructor, MethodKind.StaticConstructor);
    }

    [Fact]
    public void CorePublicApi_DoesNotExposeNativeBackendTypes()
    {
        string[] forbiddenPrefixes =
        {
            "Mono.Cecil",
            "Microsoft.CodeAnalysis",
            "System.Reflection"
        };
        var publicProperties = typeof(TypeSignature).Assembly
            .GetExportedTypes()
            .SelectMany(type => type.GetProperties(System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.DeclaredOnly));

        foreach (var property in publicProperties)
        {
            var exposedTypes = Flatten(property.PropertyType);
            Assert.DoesNotContain(
                exposedTypes,
                type => forbiddenPrefixes.Any(
                    prefix => type.FullName?.StartsWith(
                        prefix,
                        StringComparison.Ordinal) == true));
        }
    }

    private static IEnumerable<Type> Flatten(Type type)
    {
        yield return type;
        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var nested in Flatten(argument)) yield return nested;
        }
    }

    private static NamedTypeSignature SystemType(
        string name,
        TypeKind kind = TypeKind.Class)
    {
        return new NamedTypeSignature(
            Identity(
                "System.Private.CoreLib",
                "10.0.0.0",
                "System",
                name,
                kind: kind));
    }

    private static TypeIdentity Identity(
        string assemblyName,
        string assemblyVersion,
        string @namespace,
        string metadataName,
        int genericArity = 0,
        TypeKind kind = TypeKind.Class)
    {
        return new TypeIdentity(
            new AssemblyIdentity(
                assemblyName,
                new Version(assemblyVersion),
                "neutral",
                string.Empty),
            @namespace,
            metadataName,
            genericArity,
            kind);
    }
}
