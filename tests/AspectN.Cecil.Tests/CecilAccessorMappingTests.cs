using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using AspectN.Cecil.Tests.Fixtures;
using Mono.Cecil;

namespace AspectN.Cecil.Tests;

public sealed class CecilAccessorMappingTests
{
    [Fact]
    public void MapProperty_GetterSetterAndIndexer_MapShape()
    {
        using var typeSystem = EmittedFixture.Open();
        var type = FixtureType(typeSystem);
        var readOnly = typeSystem.Mapper.MapProperty(
            FindProperty(type, "ReadOnly"));
        var writeOnly = typeSystem.Mapper.MapProperty(
            FindProperty(type, "WriteOnly"));
        var indexer = typeSystem.Mapper.MapProperty(FindProperty(type, "Item"));

        Assert.Equal("System.String", NamedName(readOnly.PropertyType.Type));
        Assert.Equal("System.String", NamedName(writeOnly.PropertyType.Type));
        var indexParameter = Assert.Single(indexer.Parameters);
        Assert.Equal(0, indexParameter.Position);
        Assert.Equal("System.Int32", NamedName(indexParameter.Type.Type));
    }

    [Fact]
    public void MapProperty_RefReturningProperties_PreserveRefKind()
    {
        using var typeSystem = EmittedFixture.Open();
        var type = FixtureType(typeSystem);
        var refValue = typeSystem.Mapper.MapProperty(
            FindProperty(type, "RefValue"));
        var refReadonly = typeSystem.Mapper.MapProperty(
            FindProperty(type, "RefReadonlyValue"));

        Assert.Equal(RefKind.Ref, refValue.PropertyType.RefKind);
        Assert.Equal(RefKind.RefReadOnly, refReadonly.PropertyType.RefKind);
        Assert.Equal("System.Int32", NamedName(refValue.PropertyType.Type));
    }

    [Fact]
    public void MapMethod_PropertyAccessors_LinkAssociatedProperty()
    {
        using var typeSystem = EmittedFixture.Open();
        var type = FixtureType(typeSystem);
        var readOnly = FindProperty(type, "ReadOnly");
        var writeOnly = FindProperty(type, "WriteOnly");

        var getter = typeSystem.Mapper.MapMethod(readOnly.GetMethod);
        var setter = typeSystem.Mapper.MapMethod(writeOnly.SetMethod);

        Assert.Equal(MethodKind.PropertyGetter, getter.Kind);
        Assert.Equal("ReadOnly", getter.AssociatedProperty?.Name);
        Assert.Equal(MethodKind.PropertySetter, setter.Kind);
        Assert.Equal("WriteOnly", setter.AssociatedProperty?.Name);
    }

    [Fact]
    public void MapMethod_EventAccessors_MapEventKinds()
    {
        using var typeSystem = EmittedFixture.Open();
        var @event = Assert.Single(
            FixtureType(typeSystem).Events,
            candidate => candidate.Name == "Changed");

        var adder = typeSystem.Mapper.MapMethod(@event.AddMethod);
        var remover = typeSystem.Mapper.MapMethod(@event.RemoveMethod);

        Assert.Equal(MethodKind.EventAdder, adder.Kind);
        Assert.Equal(MethodKind.EventRemover, remover.Kind);
    }

    [Fact]
    public void MapMethod_AccessorAndPropertyAttributes_RemainSeparate()
    {
        using var typeSystem = EmittedFixture.Open();
        var property = FindProperty(FixtureType(typeSystem), "MarkedProperty");
        var getter = typeSystem.Mapper.MapMethod(property.GetMethod);
        var associated = Assert.IsType<PropertySignature>(
            getter.AssociatedProperty);

        Assert.Contains(
            getter.Attributes,
            attribute => NamedName(attribute.AttributeType).EndsWith(
                nameof(CallableMarkerAttribute),
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            getter.Attributes,
            attribute => NamedName(attribute.AttributeType).EndsWith(
                nameof(PropertyMarkerAttribute),
                StringComparison.Ordinal));
        Assert.Contains(
            associated.Attributes,
            attribute => NamedName(attribute.AttributeType).EndsWith(
                nameof(PropertyMarkerAttribute),
                StringComparison.Ordinal));

        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.HasDirectAttribute(
                AttributeTarget.ForMethod(getter),
                AttributeType(typeSystem, typeof(CallableMarkerAttribute)))
                .Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.HasDirectAttribute(
                AttributeTarget.ForMethod(getter),
                AttributeType(typeSystem, typeof(PropertyMarkerAttribute)))
                .Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.HasDirectAttribute(
                AttributeTarget.ForProperty(associated),
                AttributeType(typeSystem, typeof(PropertyMarkerAttribute)))
                .Status);
    }

    private static TypeSignature AttributeType(
        CecilTypeSystem typeSystem,
        Type type)
    {
        var definition = Assert.Single(
            typeSystem.Module.Types,
            candidate => candidate.FullName == type.FullName);
        return typeSystem.Mapper.MapType(definition);
    }

    private static TypeDefinition FixtureType(CecilTypeSystem typeSystem)
    {
        return EmittedFixture.FindType(typeSystem, "CallableFixture`1");
    }

    private static PropertyDefinition FindProperty(
        TypeDefinition type,
        string name)
    {
        return Assert.Single(type.Properties, property => property.Name == name);
    }

    private static string NamedName(TypeSignature signature)
    {
        return Assert.IsType<NamedTypeSignature>(signature)
            .Identity.FullMetadataName;
    }
}
