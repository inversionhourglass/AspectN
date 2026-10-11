using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Microsoft.CodeAnalysis;

namespace AspectN.Roslyn.Tests;

/// <summary>Forwards real Roslyn symbols while injecting an otherwise inaccessible metadata read failure.</summary>
public class FaultingNamedTypeSymbol : DispatchProxy
{
    private INamedTypeSymbol _inner = null!;
    private INamedTypeSymbol _proxy = null!;
    private bool _failArity;
    private ImmutableArray<INamedTypeSymbol>? _interfaces;

    /// <summary>Wraps a real symbol with optional interface replacement and arity-read failure.</summary>
    /// <param name="inner">The real declaration symbol.</param>
    /// <param name="failArity">Whether reading arity throws.</param>
    /// <param name="interfaces">The optional declared interface sequence.</param>
    /// <returns>The forwarded symbol.</returns>
    public static INamedTypeSymbol Wrap(INamedTypeSymbol inner, bool failArity = false,
        ImmutableArray<INamedTypeSymbol>? interfaces = null)
    {
        var proxy = Create<INamedTypeSymbol, FaultingNamedTypeSymbol>();
        var forwarding = (FaultingNamedTypeSymbol)(object)proxy;
        forwarding._inner = inner;
        forwarding._proxy = proxy;
        forwarding._failArity = failArity;
        forwarding._interfaces = interfaces;
        return proxy;
    }

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod!.Name == "get_OriginalDefinition") return _proxy;
        if (targetMethod.Name == "get_Interfaces" && _interfaces is { } interfaces) return interfaces;
        if (targetMethod.Name == "get_Arity" && _failArity)
            throw new InvalidOperationException("Injected unreadable sibling metadata.");
        if (targetMethod.Name == "Equals") return ReferenceEquals(args![0], _proxy);
        try
        {
            return targetMethod.Invoke(_inner, args);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    /// <inheritdoc />
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);

    /// <inheritdoc />
    public override bool Equals(object? obj) => ReferenceEquals(this, obj);

    /// <inheritdoc />
    public override string ToString() => _inner.ToDisplayString();
}
