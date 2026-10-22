using System.Runtime.CompilerServices;
using AspectN.AdapterInternals;
using AspectN.TypeSystem;
using AspectN.Cecil.Internal;
using AspectN.Metadata;
using Mono.Cecil;
using Mono.Collections.Generic;

namespace AspectN.Cecil;

/// <summary>Maps Cecil metadata to backend-neutral signatures with independent per-operation budgets.</summary>
/// <remarks>Concurrent mapper use requires the native metadata objects to satisfy their concurrency requirements. An outer MapType, MapTypeUse, MapMethod or MapProperty call creates a fresh budget; its nested mappings share that budget. Mapping limits do not replace compiled match limits. Completed sub-cache entries can survive a later failure, but incomplete roots are not returned.</remarks>
public sealed class CecilSignatureMapper
{
    private readonly Dictionary<CecilMappingOperation.CecilMemberCacheKey, MethodSignature>
        _callableCache = new();
    private readonly CecilTypeCache _cache = new();
    private readonly object _memberGate = new();
    private readonly Dictionary<CecilMappingOperation.CecilMemberCacheKey, PropertySignature>
        _propertyCache = new();
    private readonly CecilTypeSystem _typeSystem;

    internal CecilSignatureMapper(CecilTypeSystem typeSystem) => _typeSystem = typeSystem;

    /// <summary>Gets the immutable native-mapping limits retained by this mapper.</summary>
    /// <value>Defaults include depth 64, 100,000 logical work units, 1,024 scanned attribute records, and 16,384 UTF-16 code units per generated text.</value>
    /// <remarks>Each operation has independent counters. ResourceLimit.Unlimited disables only the corresponding quota. Structural validity, cancellation, other quotas, and native runtime constraints remain effective.</remarks>
    public AspectNMappingOptions Options => _typeSystem.Options.Resources;

    /// <summary>Maps native type structure within one independent mapping operation.</summary>
    /// <param name="typeReference">The native metadata to map. Native associations are retained according to this backend's lifetime contract.</param>
    /// <param name="cancellationToken">Cancels this operation and all nested mapping work; cancellation propagates as OperationCanceledException.</param>
    /// <returns>A complete neutral signature. Resource exhaustion does not return a partial root.</returns>
    /// <exception cref="AspectNLimitException">Depth, type or method arity, total work, scanned attribute records, or a generated text exceeds the configured mapping limit.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    /// <remarks>Uses Options: root depth is one; type arity includes enclosing types, while method arity is independent. Limits permit exactly the finite quota. This call does not consume another independent Map call's budget.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="typeReference"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The owning Cecil backend has been disposed.</exception>
    public TypeSignature MapType(TypeReference typeReference, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = CreateOperation(new MappingContext(Options, cancellationToken)).MapType(typeReference);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    /// <summary>Maps native type use within one independent mapping operation.</summary>
    /// <param name="typeReference">The native metadata to map. Native associations are retained according to this backend's lifetime contract.</param>
    /// <param name="useKind">The value-use position. Member mapping supplies exact ref classification where available.</param>
    /// <param name="cancellationToken">Cancels this operation and all nested mapping work; cancellation propagates as OperationCanceledException.</param>
    /// <returns>A complete neutral signature. Resource exhaustion does not return a partial root.</returns>
    /// <exception cref="AspectNLimitException">Depth, type or method arity, total work, scanned attribute records, or a generated text exceeds the configured mapping limit.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    /// <remarks>Uses Options: root depth is one; type arity includes enclosing types, while method arity is independent. Limits permit exactly the finite quota. This call does not consume another independent Map call's budget.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="typeReference"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The owning Cecil backend has been disposed.</exception>
    public TypeUseSignature MapTypeUse(TypeReference typeReference, TypeUseKind useKind, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = CreateOperation(new MappingContext(Options, cancellationToken)).MapTypeUse(typeReference, useKind);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    /// <summary>Maps native callable metadata within one independent mapping operation.</summary>
    /// <param name="methodReference">The native metadata to map. Native associations are retained according to this backend's lifetime contract.</param>
    /// <param name="cancellationToken">Cancels this operation and all nested mapping work; cancellation propagates as OperationCanceledException.</param>
    /// <returns>A complete neutral signature. Resource exhaustion does not return a partial root.</returns>
    /// <exception cref="AspectNLimitException">Depth, type or method arity, total work, scanned attribute records, or a generated text exceeds the configured mapping limit.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    /// <remarks>Uses Options: root depth is one; type arity includes enclosing types, while method arity is independent. Limits permit exactly the finite quota. This call does not consume another independent Map call's budget.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="methodReference"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The owning Cecil backend has been disposed.</exception>
    public MethodSignature MapMethod(MethodReference methodReference, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = CreateOperation(new MappingContext(Options, cancellationToken)).MapMethod(methodReference);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    /// <summary>Maps native property metadata within one independent mapping operation.</summary>
    /// <param name="propertyReference">The native metadata to map. Native associations are retained according to this backend's lifetime contract.</param>
    /// <param name="cancellationToken">Cancels this operation and all nested mapping work; cancellation propagates as OperationCanceledException.</param>
    /// <returns>A complete neutral signature. Resource exhaustion does not return a partial root.</returns>
    /// <exception cref="AspectNLimitException">Depth, type or method arity, total work, scanned attribute records, or a generated text exceeds the configured mapping limit.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    /// <remarks>Uses Options: root depth is one; type arity includes enclosing types, while method arity is independent. Limits permit exactly the finite quota. This call does not consume another independent Map call's budget.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="propertyReference"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The owning Cecil backend has been disposed.</exception>
    public PropertySignature MapProperty(PropertyReference propertyReference, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = CreateOperation(new MappingContext(Options, cancellationToken)).MapProperty(propertyReference);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    internal Dictionary<CecilMappingOperation.CecilMemberCacheKey, MethodSignature> CallableCache => _callableCache;
    internal CecilTypeCache Cache => _cache;
    internal object MemberGate => _memberGate;
    internal Dictionary<CecilMappingOperation.CecilMemberCacheKey, PropertySignature> PropertyCache => _propertyCache;
    internal CecilTypeSystem TypeSystem => _typeSystem;

    internal CecilMappingOperation CreateOperation(MappingContext resources) => new(this, resources);

    internal ModuleDefinition Module => _typeSystem.Module;
    internal bool TryGetTypeReference(TypeSignature signature, out TypeReference? reference) => _cache.TryGetReference(signature, out reference);
    internal void ClearCache()
    {
        _cache.Clear();
        lock (_memberGate)
        {
            _callableCache.Clear();
            _propertyCache.Clear();
        }
    }
}
