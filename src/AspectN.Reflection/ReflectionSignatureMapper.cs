using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using AspectN.AdapterInternals;
using AspectN.TypeSystem;
using AspectN.Metadata;
using AspectN.Reflection.Internal;

namespace AspectN.Reflection;

/// <summary>Maps Reflection metadata to backend-neutral signatures with independent per-operation budgets.</summary>
/// <remarks>Concurrent mapper use requires the native metadata objects to satisfy their concurrency requirements. An outer MapType, MapTypeUse, MapMethod or MapProperty call creates a fresh budget; its nested mappings share that budget. Mapping limits do not replace compiled match limits. Completed sub-cache entries can survive a later failure, but incomplete roots are not returned.</remarks>
public sealed class ReflectionSignatureMapper
{
    private readonly ConditionalWeakTable<MethodBase, MethodSignature>
        _callables = new();
    private readonly ConditionalWeakTable<PropertyInfo, PropertySignature>
        _properties = new();
    private readonly ReflectionTypeCache _types = new();
    private readonly ConditionalWeakTable<Type, ReflectionTypeCache> _contextTypes = new();

    /// <summary>Creates a reusable mapper with immutable native-mapping quotas.</summary>
    /// <param name="options">Limits for each outer mapping and all its nested mappings; null uses AspectNMappingOptions defaults.</param>
    public ReflectionSignatureMapper(AspectNMappingOptions? options = null) => Options = options ?? new AspectNMappingOptions();

    /// <summary>Gets the immutable native-mapping limits retained by this mapper.</summary>
    /// <value>Defaults include depth 64, 100,000 logical work units, 1,024 scanned attribute records, and 16,384 UTF-16 code units per generated text.</value>
    /// <remarks>Each operation has independent counters. ResourceLimit.Unlimited disables only the corresponding quota. Structural validity, cancellation, other quotas, and native runtime constraints remain effective.</remarks>
    public AspectNMappingOptions Options { get; }

    /// <summary>Maps native type structure within one independent mapping operation.</summary>
    /// <param name="type">The native metadata to map. Native associations are retained according to this backend's lifetime contract.</param>
    /// <param name="cancellationToken">Cancels this operation and all nested mapping work; cancellation propagates as OperationCanceledException.</param>
    /// <returns>A complete neutral signature. Resource exhaustion does not return a partial root.</returns>
    /// <exception cref="AspectNLimitException">Depth, type or method arity, total work, scanned attribute records, or a generated text exceeds the configured mapping limit.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    /// <remarks>Uses Options: root depth is one; type arity includes enclosing types, while method arity is independent. Limits permit exactly the finite quota. This call does not consume another independent Map call's budget.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> is null.</exception>
    public TypeSignature MapType(Type type, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = CreateOperation(new MappingContext(Options, cancellationToken)).MapType(type);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    /// <summary>Maps native type use within one independent mapping operation.</summary>
    /// <param name="type">The native metadata to map. Native associations are retained according to this backend's lifetime contract.</param>
    /// <param name="useKind">The value-use position. Member mapping supplies exact ref classification where available.</param>
    /// <param name="cancellationToken">Cancels this operation and all nested mapping work; cancellation propagates as OperationCanceledException.</param>
    /// <returns>A complete neutral signature. Resource exhaustion does not return a partial root.</returns>
    /// <exception cref="AspectNLimitException">Depth, type or method arity, total work, scanned attribute records, or a generated text exceeds the configured mapping limit.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    /// <remarks>Uses Options: root depth is one; type arity includes enclosing types, while method arity is independent. Limits permit exactly the finite quota. This call does not consume another independent Map call's budget.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> is null.</exception>
    public TypeUseSignature MapTypeUse(Type type, TypeUseKind useKind, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = CreateOperation(new MappingContext(Options, cancellationToken)).MapTypeUse(type, useKind);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    /// <summary>Maps native callable metadata within one independent mapping operation.</summary>
    /// <param name="method">The native metadata to map. Native associations are retained according to this backend's lifetime contract.</param>
    /// <param name="cancellationToken">Cancels this operation and all nested mapping work; cancellation propagates as OperationCanceledException.</param>
    /// <returns>A complete neutral signature. Resource exhaustion does not return a partial root.</returns>
    /// <exception cref="AspectNLimitException">Depth, type or method arity, total work, scanned attribute records, or a generated text exceeds the configured mapping limit.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    /// <remarks>Uses Options: root depth is one; type arity includes enclosing types, while method arity is independent. Limits permit exactly the finite quota. This call does not consume another independent Map call's budget.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="method"/> is null.</exception>
    public MethodSignature MapMethod(MethodBase method, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = CreateOperation(new MappingContext(Options, cancellationToken)).MapMethod(method);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    /// <summary>Maps native property metadata within one independent mapping operation.</summary>
    /// <param name="property">The native metadata to map. Native associations are retained according to this backend's lifetime contract.</param>
    /// <param name="cancellationToken">Cancels this operation and all nested mapping work; cancellation propagates as OperationCanceledException.</param>
    /// <returns>A complete neutral signature. Resource exhaustion does not return a partial root.</returns>
    /// <exception cref="AspectNLimitException">Depth, type or method arity, total work, scanned attribute records, or a generated text exceeds the configured mapping limit.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    /// <remarks>Uses Options: root depth is one; type arity includes enclosing types, while method arity is independent. Limits permit exactly the finite quota. This call does not consume another independent Map call's budget.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="property"/> is null.</exception>
    public PropertySignature MapProperty(PropertyInfo property, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = CreateOperation(new MappingContext(Options, cancellationToken)).MapProperty(property);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    internal ConditionalWeakTable<MethodBase, MethodSignature> Callables => _callables;
    internal ConditionalWeakTable<PropertyInfo, PropertySignature> Properties => _properties;
    internal ReflectionTypeCache Types => _types;
    internal ConditionalWeakTable<Type, ReflectionTypeCache> ContextTypes => _contextTypes;

    internal ReflectionMappingOperation CreateOperation(MappingContext resources) => new(this, resources);

    internal bool TryGetType(TypeSignature signature, out Type? type) => _types.TryGetType(signature, out type);
    internal bool TryGetType(TypeSignature signature, out Type? type, out Type? context) => _types.TryGetType(signature, out type, out context);
}
