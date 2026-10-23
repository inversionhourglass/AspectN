using AspectN.Metadata;

namespace AspectN.Tests.Metadata;

public sealed class SignatureTests
{
    [Fact]
    public void GetHashCode_ReversedContainingChains_AvoidsSystematicCollisions()
    {
        var assembly = new AssemblyIdentity("Example.Assembly");
        var collisions = 0;
        for (var index = 0; index < 32; index++)
        {
            var firstName = $"First{index}";
            var secondName = $"Second{index}";
            var forward = new TypeIdentity(assembly, "", secondName, 0, TypeKind.Class,
                new TypeIdentity(assembly, "", firstName, 0, TypeKind.Class));
            var reverse = new TypeIdentity(assembly, "", firstName, 0, TypeKind.Class,
                new TypeIdentity(assembly, "", secondName, 0, TypeKind.Class));

            Assert.NotEqual(forward, reverse);
            if (forward.GetHashCode() == reverse.GetHashCode()) collisions++;
        }

        Assert.InRange(collisions, 0, 1);
    }

    [Fact]
    public void GetHashCode_RepeatedNestedIdentity_DoesNotAllocate()
    {
        var assembly = new AssemblyIdentity("Example.Assembly");
        var identity = new TypeIdentity(assembly, "", "Inner", 1, TypeKind.Class,
            new TypeIdentity(assembly, "Example", "Outer", 2, TypeKind.Class));
        var equal = new TypeIdentity(assembly, "", "Inner", 1, TypeKind.Class,
            new TypeIdentity(assembly, "Example", "Outer", 2, TypeKind.Class));
        Assert.Equal(identity, equal);
        Assert.Equal(identity.GetHashCode(), equal.GetHashCode());
        for (var index = 0; index < 100; index++) _ = identity.GetHashCode();
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var index = 0; index < 1000; index++) _ = identity.GetHashCode();

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

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
