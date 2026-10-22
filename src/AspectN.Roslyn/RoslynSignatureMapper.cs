using System.Reflection.Metadata;
using AspectN.AdapterInternals;
using AspectN.TypeSystem;
using AspectN.Metadata;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CoreAccessibility = AspectN.Metadata.Accessibility;
using CoreAssemblyIdentity = AspectN.Metadata.AssemblyIdentity;
using CoreMethodKind = AspectN.Metadata.MethodKind;
using CoreRefKind = AspectN.Metadata.RefKind;
using CoreTypeKind = AspectN.Metadata.TypeKind;
using RoslynMethodKind = Microsoft.CodeAnalysis.MethodKind;

namespace AspectN.Roslyn;

/// <summary>Maps Roslyn metadata to backend-neutral signatures with independent per-operation budgets.</summary>
/// <remarks>Concurrent mapper use requires the native metadata objects to satisfy their concurrency requirements. An outer MapType, MapTypeUse, MapMethod or MapProperty call creates a fresh budget; its nested mappings share that budget. Mapping limits do not replace compiled match limits. Completed sub-cache entries can survive a later failure, but incomplete roots are not returned.</remarks>
public sealed class RoslynSignatureMapper
{
    private readonly Dictionary<IMethodSymbol, MethodSignature> _callables =
        new(SymbolEqualityComparer.Default);
    private readonly Compilation _compilation;
    private readonly object _gate = new();
    private readonly Dictionary<IPropertySymbol, PropertySignature> _properties =
        new(SymbolEqualityComparer.Default);
    private readonly Dictionary<ITypeSymbol, TypeSignature> _types =
        new(SymbolEqualityComparer.Default);
    private readonly Dictionary<TypeSignature, ITypeSymbol> _typeSymbols = new();

    /// <summary>Creates a reusable symbol mapper bound to one compilation and immutable mapping quotas.</summary>
    /// <param name="compilation">The compilation supplying symbol context and native associations.</param>
    /// <param name="options">Limits for each outer mapping; null uses AspectNMappingOptions defaults.</param>
    /// <exception cref="ArgumentNullException">The compilation is null.</exception>
    public RoslynSignatureMapper(Compilation compilation, AspectNMappingOptions? options = null)
    {
        _compilation = compilation ?? throw new ArgumentNullException(nameof(compilation));
        Options = options ?? new AspectNMappingOptions();
    }

    /// <summary>Gets the immutable native-mapping limits retained by this mapper.</summary>
    /// <value>Defaults include depth 64, 100,000 logical work units, 1,024 scanned attribute records, and 16,384 UTF-16 code units per generated text.</value>
    /// <remarks>Each operation has independent counters. ResourceLimit.Unlimited disables only the corresponding quota. Structural validity, cancellation, other quotas, and native runtime constraints remain effective.</remarks>
    public AspectNMappingOptions Options { get; }

    /// <summary>Maps native type structure within one independent mapping operation.</summary>
    /// <param name="typeSymbol">The native metadata to map. Native associations are retained according to this backend's lifetime contract.</param>
    /// <param name="cancellationToken">Cancels this operation and all nested mapping work; cancellation propagates as OperationCanceledException.</param>
    /// <returns>A complete neutral signature. Resource exhaustion does not return a partial root.</returns>
    /// <exception cref="AspectNLimitException">Depth, type or method arity, total work, scanned attribute records, or a generated text exceeds the configured mapping limit.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    /// <remarks>Uses Options: root depth is one; type arity includes enclosing types, while method arity is independent. Limits permit exactly the finite quota. This call does not consume another independent Map call's budget.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="typeSymbol"/> is null.</exception>
    public TypeSignature MapType(ITypeSymbol typeSymbol, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = CreateOperation(new MappingContext(Options, cancellationToken)).MapType(typeSymbol);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    /// <summary>Maps native type use within one independent mapping operation.</summary>
    /// <param name="typeSymbol">The native metadata to map. Native associations are retained according to this backend's lifetime contract.</param>
    /// <param name="useKind">The value-use position. Member mapping supplies exact ref classification where available.</param>
    /// <param name="cancellationToken">Cancels this operation and all nested mapping work; cancellation propagates as OperationCanceledException.</param>
    /// <returns>A complete neutral signature. Resource exhaustion does not return a partial root.</returns>
    /// <exception cref="AspectNLimitException">Depth, type or method arity, total work, scanned attribute records, or a generated text exceeds the configured mapping limit.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    /// <remarks>Uses Options: root depth is one; type arity includes enclosing types, while method arity is independent. Limits permit exactly the finite quota. This call does not consume another independent Map call's budget.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="typeSymbol"/> is null.</exception>
    public TypeUseSignature MapTypeUse(ITypeSymbol typeSymbol, TypeUseKind useKind, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = CreateOperation(new MappingContext(Options, cancellationToken)).MapTypeUse(typeSymbol, useKind);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    /// <summary>Maps native callable metadata within one independent mapping operation.</summary>
    /// <param name="methodSymbol">The native metadata to map. Native associations are retained according to this backend's lifetime contract.</param>
    /// <param name="cancellationToken">Cancels this operation and all nested mapping work; cancellation propagates as OperationCanceledException.</param>
    /// <returns>A complete neutral signature. Resource exhaustion does not return a partial root.</returns>
    /// <exception cref="AspectNLimitException">Depth, type or method arity, total work, scanned attribute records, or a generated text exceeds the configured mapping limit.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    /// <remarks>Uses Options: root depth is one; type arity includes enclosing types, while method arity is independent. Limits permit exactly the finite quota. This call does not consume another independent Map call's budget.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="methodSymbol"/> is null.</exception>
    public MethodSignature MapMethod(IMethodSymbol methodSymbol, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = CreateOperation(new MappingContext(Options, cancellationToken)).MapMethod(methodSymbol);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    /// <summary>Maps native property metadata within one independent mapping operation.</summary>
    /// <param name="propertySymbol">The native metadata to map. Native associations are retained according to this backend's lifetime contract.</param>
    /// <param name="cancellationToken">Cancels this operation and all nested mapping work; cancellation propagates as OperationCanceledException.</param>
    /// <returns>A complete neutral signature. Resource exhaustion does not return a partial root.</returns>
    /// <exception cref="AspectNLimitException">Depth, type or method arity, total work, scanned attribute records, or a generated text exceeds the configured mapping limit.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
    /// <remarks>Uses Options: root depth is one; type arity includes enclosing types, while method arity is independent. Limits permit exactly the finite quota. This call does not consume another independent Map call's budget.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="propertySymbol"/> is null.</exception>
    public PropertySignature MapProperty(IPropertySymbol propertySymbol, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = CreateOperation(new MappingContext(Options, cancellationToken)).MapProperty(propertySymbol);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    internal Dictionary<IMethodSymbol, MethodSignature> Callables => _callables;
    internal Compilation Compilation => _compilation;
    internal object Gate => _gate;
    internal Dictionary<IPropertySymbol, PropertySignature> Properties => _properties;
    internal Dictionary<ITypeSymbol, TypeSignature> Types => _types;
    internal Dictionary<TypeSignature, ITypeSymbol> TypeSymbols => _typeSymbols;

    internal RoslynMappingOperation CreateOperation(MappingContext resources) => new(this, resources);

    internal bool TryGetTypeSymbol(TypeSignature signature, out ITypeSymbol? typeSymbol)
    {
        lock (_gate) return _typeSymbols.TryGetValue(signature, out typeSymbol);
    }
}
