using Mono.Cecil;

#pragma warning disable CA1822

namespace AspectN.Cecil.Tests.Fixtures;

[AttributeUsage(AttributeTargets.Class)]
internal sealed class TypeMarkerAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method)]
internal sealed class CallableMarkerAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.ReturnValue)]
internal sealed class ReturnMarkerAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class ParameterMarkerAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property)]
internal sealed class PropertyMarkerAttribute : Attribute
{
}

[TypeMarker]
internal sealed class CallableFixture<T>
{
    private int _value;
    private string _writeOnly = string.Empty;

    static CallableFixture()
    {
    }

    [CallableMarker]
    public CallableFixture()
    {
    }

    public string ReadOnly => string.Empty;

    public string WriteOnly
    {
        set => _writeOnly = value;
    }

    public string this[int index]
    {
        get => index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        set => _writeOnly = value;
    }

    [PropertyMarker]
    public string MarkedProperty
    {
        [CallableMarker]
        get => _writeOnly;
    }

    public event EventHandler? Changed
    {
        add { }
        remove { }
    }

    public void Ordinary()
    {
    }

    public async Task<string> LoadAsync(string value)
    {
        await Task.Yield();
        return value;
    }

    public Task TaskReturning()
    {
        return Task.CompletedTask;
    }

    [CallableMarker]
    [return: ReturnMarker]
    public string Marked([ParameterMarker] string value)
    {
        return value;
    }

    public TMethod Echo<TMethod>(TMethod value)
    {
        return value;
    }

    public void RefKinds(
        ref int byReference,
        out string output,
        in long input,
        ref readonly decimal readOnly)
    {
        output = string.Empty;
        _ = byReference;
        _ = input;
        _ = readOnly;
    }

    public ref readonly int RefReadonlyReturn()
    {
        return ref _value;
    }

    public ref int RefValue => ref _value;

    public ref readonly int RefReadonlyValue => ref _value;

    public static CallableFixture<T> operator +(
        CallableFixture<T> left,
        CallableFixture<T> right)
    {
        _ = right;
        return left;
    }

    public static implicit operator string(CallableFixture<T> value)
    {
        _ = value;
        return string.Empty;
    }
}

internal sealed class DeclaredAccessibilityFixture
{
    public void PublicMethod()
    {
    }
}

internal static class EmittedFixture
{
    public static CecilTypeSystem Open()
    {
        return new CecilTypeSystem(
            ModuleDefinition.ReadModule(
                typeof(EmittedFixture).Assembly.Location));
    }

    public static CecilTypeSystem Open(CecilMappingOptions options)
    {
        return new CecilTypeSystem(
            ModuleDefinition.ReadModule(
                typeof(EmittedFixture).Assembly.Location),
            options);
    }

    public static TypeDefinition FindType(
        CecilTypeSystem typeSystem,
        string metadataName)
    {
        return Assert.Single(
            typeSystem.Module.Types,
            type => type.Namespace == typeof(EmittedFixture).Namespace &&
                    type.Name == metadataName);
    }
}
