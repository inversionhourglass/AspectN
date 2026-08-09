using System.Globalization;
using System.Reflection;
using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Xunit;

namespace AspectN.Reflection.Tests;

public sealed class ReflectionMappingTests
{
    [Fact]
    public void MapType_NestedConstructedArrayAndGenericParameters_PreserveShape()
    {
        var typeSystem = new ReflectionTypeSystem();
        var definition = Assert.IsType<NamedTypeSignature>(
            typeSystem.Mapper.MapType(
                typeof(ReflectionOuter<>.Inner<,>)));
        var constructed = Assert.IsType<NamedTypeSignature>(
            typeSystem.Mapper.MapType(
                typeof(ReflectionOuter<int>.Inner<string, double>)));
        var outerArray = Assert.IsType<ArrayTypeSignature>(
            typeSystem.Mapper.MapType(typeof(int[,][])));
        var innerArray = Assert.IsType<ArrayTypeSignature>(
            outerArray.ElementType);
        var typeParameter = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(
                typeof(ReflectionFixture<>).GetGenericArguments()[0]));
        var method = RequiredMethod(
            typeof(ReflectionFixture<>),
            nameof(ReflectionFixture<object>.Echo));
        var methodParameter = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(method.GetGenericArguments()[0]));

        Assert.Equal(
            $"{typeof(ReflectionOuter<>).Namespace}." +
            "ReflectionOuter`1/Inner`2",
            definition.Identity.FullMetadataName);
        Assert.Equal(3, constructed.TypeArguments.Count);
        Assert.NotNull(constructed.OriginalDefinition);
        Assert.Equal(2, outerArray.Rank);
        Assert.Equal(1, innerArray.Rank);
        Assert.Equal(GenericParameterOwnerKind.Type, typeParameter.Parameter.OwnerKind);
        Assert.Equal(0, typeParameter.Parameter.Position);
        Assert.Equal(
            GenericParameterOwnerKind.Method,
            methodParameter.Parameter.OwnerKind);
        Assert.EndsWith(
            ".Echo`1",
            methodParameter.Parameter.OwnerId,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(typeof(string), TypeKind.Class)]
    [InlineData(typeof(int), TypeKind.Struct)]
    [InlineData(typeof(IDisposable), TypeKind.Interface)]
    [InlineData(typeof(DayOfWeek), TypeKind.Enum)]
    [InlineData(typeof(Action), TypeKind.Delegate)]
    public void MapType_RuntimeKindsAndCache_ReturnExpected(
        Type type,
        TypeKind expectedKind)
    {
        var typeSystem = new ReflectionTypeSystem();

        var first = Assert.IsType<NamedTypeSignature>(
            typeSystem.Mapper.MapType(type));
        var second = typeSystem.Mapper.MapType(type);

        Assert.Equal(expectedKind, first.Identity.Kind);
        Assert.Same(first, second);
    }

    [Fact]
    public void MapCallable_DefinitionConstructedMethodAndConstructors_MapShape()
    {
        var typeSystem = new ReflectionTypeSystem();
        var definition = RequiredMethod(
            typeof(ReflectionFixture<>),
            nameof(ReflectionFixture<object>.Echo));
        var constructed = RequiredMethod(
                typeof(ReflectionFixture<string>),
                nameof(ReflectionFixture<object>.Echo))
            .MakeGenericMethod(typeof(int));
        var definitionSignature = typeSystem.Mapper.MapCallable(definition);
        var signature = typeSystem.Mapper.MapCallable(constructed);
        var constructor = typeSystem.Mapper.MapCallable(
            Assert.Single(
                typeof(ReflectionFixture<string>).GetConstructors(
                    BindingFlags.Public |
                    BindingFlags.Instance)));
        var typeInitializer = typeSystem.Mapper.MapCallable(
            Assert.IsAssignableFrom<ConstructorInfo>(
                typeof(ReflectionFixture<string>).TypeInitializer));

        Assert.Single(definitionSignature.GenericParameters);
        Assert.Empty(definitionSignature.GenericArguments);
        Assert.Empty(signature.GenericParameters);
        Assert.Equal("System.Int32", Name(Assert.Single(signature.GenericArguments)));
        Assert.Equal("System.Int32", Name(signature.ReturnType.Type));
        Assert.Collection(
            signature.Parameters,
            parameter => Assert.Equal("System.String", Name(parameter.Type.Type)),
            parameter => Assert.Equal("System.Int32", Name(parameter.Type.Type)));
        Assert.NotNull(signature.OriginalDefinition);
        Assert.Equal(CallableKind.InstanceConstructor, constructor.Kind);
        Assert.Equal(CallableKind.StaticConstructor, typeInitializer.Kind);
    }

    [Fact]
    public void MapCallable_RefKindsAsyncAndSpecialMethods_MapSemantics()
    {
        var typeSystem = new ReflectionTypeSystem();
        var type = typeof(ReflectionFixture<int>);
        var refKinds = typeSystem.Mapper.MapCallable(
            RequiredMethod(type, nameof(ReflectionFixture<int>.RefKinds)));
        var refReturn = typeSystem.Mapper.MapCallable(
            RequiredMethod(type, nameof(ReflectionFixture<int>.RefReadonlyReturn)));
        var asyncMethod = typeSystem.Mapper.MapCallable(
            RequiredMethod(type, nameof(ReflectionFixture<int>.LoadAsync)));
        var taskReturning = typeSystem.Mapper.MapCallable(
            RequiredMethod(type, nameof(ReflectionFixture<int>.TaskReturning)));
        var addition = typeSystem.Mapper.MapCallable(
            RequiredMethod(type, "op_Addition"));
        var conversion = typeSystem.Mapper.MapCallable(
            RequiredMethod(type, "op_Implicit"));

        Assert.Collection(
            refKinds.Parameters,
            parameter => Assert.Equal(RefKind.Ref, parameter.Type.RefKind),
            parameter => Assert.Equal(RefKind.Out, parameter.Type.RefKind),
            parameter => Assert.Equal(RefKind.In, parameter.Type.RefKind),
            parameter => Assert.Equal(RefKind.RefReadOnly, parameter.Type.RefKind));
        Assert.Equal(RefKind.RefReadOnly, refReturn.ReturnType.RefKind);
        Assert.True(asyncMethod.IsAsync);
        Assert.False(taskReturning.IsAsync);
        Assert.Equal(CallableKind.Operator, addition.Kind);
        Assert.Equal(CallableKind.Conversion, conversion.Kind);
    }

    [Fact]
    public void MapProperty_AccessorsIndexerAndEvents_MapShape()
    {
        var typeSystem = new ReflectionTypeSystem();
        var type = typeof(ReflectionFixture<int>);
        var name = Assert.IsAssignableFrom<PropertyInfo>(
            type.GetProperty(nameof(ReflectionFixture<int>.Name)));
        var indexer = Assert.IsAssignableFrom<PropertyInfo>(
            type.GetProperty("Item"));
        var property = typeSystem.Mapper.MapProperty(name);
        var mappedIndexer = typeSystem.Mapper.MapProperty(indexer);
        var getter = typeSystem.Mapper.MapCallable(
            Assert.IsAssignableFrom<MethodInfo>(name.GetMethod));
        var setter = typeSystem.Mapper.MapCallable(
            Assert.IsAssignableFrom<MethodInfo>(name.SetMethod));
        var @event = Assert.IsAssignableFrom<EventInfo>(
            type.GetEvent(nameof(ReflectionFixture<int>.Changed)));
        var adder = typeSystem.Mapper.MapCallable(
            Assert.IsAssignableFrom<MethodInfo>(@event.AddMethod));
        var remover = typeSystem.Mapper.MapCallable(
            Assert.IsAssignableFrom<MethodInfo>(@event.RemoveMethod));

        Assert.Equal("System.String", Name(property.PropertyType.Type));
        Assert.Equal("System.Int32", Name(Assert.Single(mappedIndexer.Parameters).Type.Type));
        Assert.Equal(CallableKind.PropertyGetter, getter.Kind);
        Assert.Equal("Name", getter.AssociatedProperty?.Name);
        Assert.Equal(CallableKind.PropertySetter, setter.Kind);
        Assert.Equal("Name", setter.AssociatedProperty?.Name);
        Assert.Equal(CallableKind.EventAdder, adder.Kind);
        Assert.Equal(CallableKind.EventRemover, remover.Kind);
    }

    [Fact]
    public void MapAndLookupAttributes_UsesCustomAttributeDataWithoutInstantiation()
    {
        var typeSystem = new ReflectionTypeSystem();
        var type = typeof(ReflectionFixture<>);
        var method = RequiredMethod(type, nameof(ReflectionFixture<object>.Echo));
        var property = Assert.IsAssignableFrom<PropertyInfo>(
            type.GetProperty(nameof(ReflectionFixture<object>.Name)));
        var callable = typeSystem.Mapper.MapCallable(method);
        var mappedProperty = typeSystem.Mapper.MapProperty(property);
        var getter = typeSystem.Mapper.MapCallable(
            Assert.IsAssignableFrom<MethodInfo>(property.GetMethod));
        var attributeType = typeSystem.Mapper.MapType(
            typeof(ExplosiveMarkerAttribute));

        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.HasDirectAttribute(
                AttributeTarget.ForType(typeSystem.Mapper.MapType(type)),
                attributeType).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.HasDirectAttribute(
                AttributeTarget.ForCallable(callable),
                attributeType).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.HasDirectAttribute(
                AttributeTarget.ForReturn(callable),
                attributeType).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.HasDirectAttribute(
                AttributeTarget.ForParameter(callable.Parameters[0]),
                attributeType).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.HasDirectAttribute(
                AttributeTarget.ForProperty(mappedProperty),
                attributeType).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.HasDirectAttribute(
                AttributeTarget.ForCallable(getter),
                attributeType).Status);
    }

    [Fact]
    public void TypeSystem_AssignabilityAndUnqualifiedPattern_ReturnExpected()
    {
        var typeSystem = new ReflectionTypeSystem();
        var candidate = typeSystem.Mapper.MapType(typeof(List<string>));
        var target = typeSystem.Mapper.MapType(typeof(IEnumerable<string>));
        var reverse = typeSystem.IsAssignableTo(target, candidate);
        var direct = typeSystem.IsAssignableTo(candidate, target);
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseTypePattern(
                "System.Collections.Generic.IEnumerable<string>+"));
        Assert.True(compilation.TryGetTypePattern(out var pattern));

        var patternResult = pattern!.Match(candidate, typeSystem);

        Assert.Equal(MatchStatus.Matched, direct.Status);
        Assert.Equal(MatchStatus.NotMatched, reverse.Status);
        Assert.Equal(MatchStatus.Matched, patternResult.Status);
        Assert.Equal(TypeSystemCapabilities.All, typeSystem.Capabilities);
    }

    [Fact]
    public void IsAssignableTo_NativeCovariantTarget_ReturnsMatched()
    {
        var typeSystem = new ReflectionTypeSystem();
        var candidate = typeSystem.Mapper.MapType(typeof(List<string>));
        var target = typeSystem.Mapper.MapType(typeof(IEnumerable<object>));

        var result = typeSystem.IsAssignableTo(candidate, target);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void IsAssignableTo_MetadataFailure_ReturnsCapabilityDiagnostic()
    {
        var typeSystem = new ReflectionTypeSystem();
        var nativeType = new FailingMetadataType(
            typeof(ReflectionFixture<int>));
        var candidate = typeSystem.Mapper.MapType(nativeType);
        var target = new NamedTypeSignature(
            new TypeIdentity(
                new AssemblyIdentity(string.Empty),
                "Missing",
                "Target",
                0,
                TypeKind.Class));

        var result = typeSystem.IsAssignableTo(candidate, target);

        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code ==
                DiagnosticCodes.MissingBackendCapability);
    }

    private static MethodInfo RequiredMethod(Type type, string name)
    {
        return Assert.IsAssignableFrom<MethodInfo>(
            type.GetMethod(
                name,
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.Static));
    }

    private static string Name(TypeSignature type)
    {
        return Assert.IsType<NamedTypeSignature>(type)
            .Identity.FullMetadataName;
    }

    private sealed class FailingMetadataType : TypeDelegator
    {
        public FailingMetadataType(Type delegatingType)
            : base(delegatingType)
        {
        }

        public override Type[] GetInterfaces()
        {
            throw new TypeLoadException("Synthetic metadata failure.");
        }
    }
}
[AttributeUsage(AttributeTargets.All)]
internal sealed class ExplosiveMarkerAttribute : Attribute
{
    public ExplosiveMarkerAttribute()
    {
        throw new InvalidOperationException("Attributes must not be instantiated.");
    }
}

[ExplosiveMarker]
internal sealed class ReflectionFixture<T>
{
    private int _value;

    static ReflectionFixture()
    {
    }

    [ExplosiveMarker]
    public ReflectionFixture()
    {
    }

    [ExplosiveMarker]
    [return: ExplosiveMarker]
    public TMethod Echo<TMethod>(
        [ExplosiveMarker] T value,
        TMethod other)
    {
        _value += value is null ? 0 : 1;
        return other;
    }

    public async Task<T> LoadAsync(T value)
    {
        await Task.Yield();
        _value++;
        return value;
    }

    public Task<T> TaskReturning(T value)
    {
        _value++;
        return Task.FromResult(value);
    }

    public void RefKinds(
        ref int first,
        out string second,
        in double third,
        ref readonly long fourth)
    {
        first += _value + (int)third + (int)fourth;
        second = first.ToString(CultureInfo.InvariantCulture);
    }

    public ref readonly int RefReadonlyReturn()
    {
        return ref _value;
    }

    [ExplosiveMarker]
    public string Name
    {
        [ExplosiveMarker]
        get;
        set;
    } = string.Empty;

    public string this[int index] => index.ToString(CultureInfo.InvariantCulture);

    public event EventHandler? Changed;

    public void RaiseChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public static ReflectionFixture<T> operator +(
        ReflectionFixture<T> left,
        ReflectionFixture<T> right)
    {
        _ = right;
        return left;
    }

    public static implicit operator string(ReflectionFixture<T> value)
    {
        _ = value;
        return string.Empty;
    }
}

internal sealed class ReflectionOuter<T>
{
    internal sealed class Inner<TKey, TValue>
    {
    }
}
