using AspectN.AdapterInternals;
using AspectN.Cecil.Internal;
using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Mono.Cecil;

namespace AspectN.Cecil;

/// <summary>Provides Mono.Cecil-backed type and metadata operations for AspectN matching.</summary>
public sealed class CecilTypeSystem : ITypeSystem, IBudgetAwareTypeSystem, IDisposable
{
    private ModuleDefinition? _module;

    /// <summary>Creates a module-scoped Cecil backend that borrows the supplied module by default.</summary>
    /// <param name="module">The module to query. Keep it alive while this backend is in use.</param>
    /// <param name="options">
    /// Mapping and ownership options. Null uses defaults, including <see cref="CecilMappingOptions.LeaveOpen"/>
    /// set to <see langword="true"/>. Set LeaveOpen to false explicitly to transfer module disposal to this instance.
    /// </param>
    /// <param name="queryOptions">Immutable limits for independent public queries; null uses match defaults. Internal compiled matching uses its plan options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="module"/> is null.</exception>
    /// <remarks>
    /// By default the caller or host owns the module, so disposing this backend does not prevent later module use.
    /// This backend and its mapper cannot be used after disposal, regardless of module ownership.
    /// </remarks>
    /// <example>
    /// <code>
    /// using var module = Mono.Cecil.ModuleDefinition.ReadModule("Example.dll");
    /// using (var backend = new AspectN.Cecil.CecilTypeSystem(module))
    /// {
    ///     var signature = backend.Mapper.MapType(module.Types[0]);
    /// }
    /// module.Write("Example.processed.dll");
    /// </code>
    /// </example>
    public CecilTypeSystem(
        ModuleDefinition module,
        CecilMappingOptions? options = null, AspectNMatchOptions? queryOptions = null)
    {
        _module = module ?? throw new ArgumentNullException(nameof(module));
        Options = options ?? new CecilMappingOptions();
        Mapper = new CecilSignatureMapper(this);
        QueryOptions = queryOptions ?? new AspectNMatchOptions();
    }

    /// <summary>Gets the module supplied to this backend.</summary>
    /// <exception cref="ObjectDisposedException">This backend has been disposed.</exception>
    public ModuleDefinition Module
    {
        get
        {
            ThrowIfDisposed();
            return _module!;
        }
    }

    /// <summary>Gets the mapping limits and module ownership policy used by this backend.</summary>
    public CecilMappingOptions Options { get; }

    /// <summary>Gets immutable standalone-query defaults; compiled matches supply their own match options.</summary>
    /// <remarks>Each direct query has independent counters. All nested mappings share one mapping sub-budget and consume the active query budget.</remarks>
    public AspectNMatchOptions QueryOptions { get; }

    /// <summary>Gets the mapper associated with this module-scoped backend.</summary>
    public CecilSignatureMapper Mapper { get; }

    /// <inheritdoc cref="ITypeSystem.Capabilities"/>
    public TypeSystemCapabilities Capabilities =>
        TypeSystemCapabilities.TypeEquality |
        TypeSystemCapabilities.BaseTypeTraversal |
        TypeSystemCapabilities.InterfaceTraversal |
        TypeSystemCapabilities.Assignability |
        TypeSystemCapabilities.GenericSubstitution |
        TypeSystemCapabilities.DirectAttributeLookup |
        TypeSystemCapabilities.OriginalDefinitionMapping;

    /// <inheritdoc cref="ITypeSystem.AreEqual"/>
    /// <exception cref="ObjectDisposedException">This backend has been disposed.</exception>
    public TypeRelationResult AreEqual(TypeSignature left, TypeSignature right, CancellationToken cancellationToken = default) =>
        ((IBudgetAwareTypeSystem)this).AreEqual(left, right, new TypeSystemQueryContext(QueryOptions, cancellationToken));

    TypeRelationResult IBudgetAwareTypeSystem.AreEqual(TypeSignature left, TypeSignature right, TypeSystemQueryContext context)
    {
        ThrowIfDisposed();
        try
        {
            context.ThrowIfExhausted();
            var result = StructuralTypeComparer.AreEqual(left, right, context);
            context.ThrowIfExhausted();
            return result;
        }
        catch (AspectNLimitException limit)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var diagnostic = context.LimitDiagnostic ?? limit.Diagnostic;
            return TypeRelationResult.Unknown(diagnostic);
        }
    }


    /// <inheritdoc cref="ITypeSystem.IsAssignableTo"/>
    /// <exception cref="ObjectDisposedException">This backend has been disposed.</exception>
    public TypeRelationResult IsAssignableTo(TypeSignature candidate, TypeSignature target, CancellationToken cancellationToken = default) =>
        ((IBudgetAwareTypeSystem)this).IsAssignableTo(candidate, target, new TypeSystemQueryContext(QueryOptions, cancellationToken));

    TypeRelationResult IBudgetAwareTypeSystem.IsAssignableTo(TypeSignature candidate, TypeSignature target, TypeSystemQueryContext context)
    {
        ThrowIfDisposed();
        try
        {
            context.ThrowIfExhausted();
            var resources = new MappingContext(Mapper.Options, context.CancellationToken, context);
            var operation = Mapper.CreateOperation(resources);
            var result = new CecilResolver(operation, context, resources).IsAssignableTo(candidate, target, context.CancellationToken);
            context.ThrowIfExhausted();
            return result;
        }
        catch (AspectNLimitException limit)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var diagnostic = context.LimitDiagnostic ?? limit.Diagnostic;
            return TypeRelationResult.Unknown(diagnostic);
        }
    }


    /// <inheritdoc cref="ITypeSystem.GetInheritanceClosure"/>
    /// <exception cref="ObjectDisposedException">This backend has been disposed.</exception>
    public InheritanceClosure GetInheritanceClosure(TypeSignature candidate, CancellationToken cancellationToken = default) =>
        ((IBudgetAwareTypeSystem)this).GetInheritanceClosure(candidate, new TypeSystemQueryContext(QueryOptions, cancellationToken));

    InheritanceClosure IBudgetAwareTypeSystem.GetInheritanceClosure(TypeSignature candidate, TypeSystemQueryContext context)
    {
        ThrowIfDisposed();
        try
        {
            context.ThrowIfExhausted();
            var resources = new MappingContext(Mapper.Options, context.CancellationToken, context);
            var operation = Mapper.CreateOperation(resources);
            var result = new CecilResolver(operation, context, resources).GetInheritanceClosure(candidate, context.CancellationToken);
            context.ThrowIfExhausted();
            return result;
        }
        catch (AspectNLimitException limit)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var diagnostic = context.LimitDiagnostic ?? limit.Diagnostic;
            return new InheritanceClosure(Array.Empty<TypeSignature>(), TypeRelationResult.Unknown(diagnostic));
        }
    }


    /// <inheritdoc cref="ITypeSystem.HasDirectAttribute"/>
    /// <exception cref="ObjectDisposedException">This backend has been disposed.</exception>
    public TypeRelationResult HasDirectAttribute(AttributeTarget target, TypeSignature attributeType, CancellationToken cancellationToken = default) =>
        ((IBudgetAwareTypeSystem)this).HasDirectAttribute(target, attributeType, new TypeSystemQueryContext(QueryOptions, cancellationToken));

    TypeRelationResult IBudgetAwareTypeSystem.HasDirectAttribute(AttributeTarget target, TypeSignature attributeType, TypeSystemQueryContext context)
    {
        ThrowIfDisposed();
        try
        {
            context.ThrowIfExhausted();
            var resources = new MappingContext(Mapper.Options, context.CancellationToken, context);
            var operation = Mapper.CreateOperation(resources);
            var result = HasDirectAttributeCore(target, attributeType, operation, resources, context);
            context.ThrowIfExhausted();
            return result;
        }
        catch (AspectNLimitException limit)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var diagnostic = context.LimitDiagnostic ?? limit.Diagnostic;
            return TypeRelationResult.Unknown(diagnostic);
        }
    }

    private TypeRelationResult HasDirectAttributeCore(AttributeTarget target, TypeSignature attributeType, CecilMappingOperation operation, MappingContext resources, TypeSystemQueryContext context)
    {
        ThrowIfDisposed();
        context.ThrowIfExhausted();
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(attributeType);
#else
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (attributeType is null)
            throw new ArgumentNullException(nameof(attributeType));
#endif

        var structuralFailure = MetadataLimitGuard.GetFailure(attributeType, context) ?? MetadataLimitGuard.GetFailure(target, context);
        if (structuralFailure is not null) return TypeRelationResult.Unknown(structuralFailure);
        var metadataFailure = AttributeAvailability.GetFailure(target);
        if (metadataFailure is not null) return metadataFailure;
        IReadOnlyList<AttributeSignature>? attributes;
        MetadataAvailability availability;
        try
        {
            attributes = GetAttributes(target, operation, resources, out availability);
        }
        catch (AspectNLimitException exception)
        {
            return TypeRelationResult.Unknown(exception.Diagnostic);
        }

        if (availability.UnavailableFacts != MetadataFacts.None)
            return TypeRelationResult.Unknown(availability.Diagnostics.Count > 0 ? availability.Diagnostics.ToArray() : new[]
            {
                new AspectNDiagnostic(DiagnosticCodes.MissingBackendCapability, DiagnosticSeverity.Warning,
                    "The attribute collection is unavailable.")
            });
        if (attributes is null)
        {
            return MissingCapability(
                "No Cecil reference is available for the attribute target.");
        }

        TypeRelationResult? unknown = null;
        foreach (var attribute in attributes)
        {
            if (!context.TryConsumeAttributeRecords(1)) context.ThrowIfExhausted();
            var relation = StructuralTypeComparer.AreEqual(attribute.AttributeType, attributeType, context, unqualified: true);
            if (relation.Status == MatchStatus.Matched) return relation;
            if (relation.Status == MatchStatus.Unknown) unknown = relation;
        }

        return unknown ?? TypeRelationResult.NotMatched;
    }


    /// <summary>Clears this backend's caches and makes its module, query, and mapping operations unavailable.</summary>
    /// <remarks>
    /// Repeated calls are harmless. The supplied module is left open by default and remains the caller's responsibility.
    /// If <see cref="CecilMappingOptions.LeaveOpen"/> is <see langword="false"/>, this method also disposes the module.
    /// Keeping the module open does not keep this backend or its mapper usable after disposal.
    /// </remarks>
    public void Dispose()
    {
        var module = Interlocked.Exchange(ref _module, null);
        Mapper.ClearCache();
        if (!Options.LeaveOpen) module?.Dispose();
        GC.SuppressFinalize(this);
    }

    internal void ThrowIfDisposed()
    {
#if NET10_0_OR_GREATER
        ObjectDisposedException.ThrowIf(_module is null, this);
#else
        if (_module is null)
        {
            throw new ObjectDisposedException(nameof(CecilTypeSystem));
        }
#endif
    }

    private static TypeRelationResult MissingCapability(string message)
    {
        return TypeRelationResult.Unknown(
            new AspectNDiagnostic(
                DiagnosticCodes.MissingBackendCapability,
                DiagnosticSeverity.Warning,
                message));
    }

    private IReadOnlyList<AttributeSignature>? GetAttributes(AttributeTarget target, CecilMappingOperation operation, MappingContext resources, out MetadataAvailability availability)
    {
        if (target.Kind == AttributeTargetKind.Type) return GetTypeAttributes(target.Type!, operation, resources, out availability);
        var source = target.Kind switch
        {
            AttributeTargetKind.Property => target.Property!.Availability,
            AttributeTargetKind.Parameter => target.Parameter!.Availability,
            _ => target.Method!.Availability
        };
        var fact = target.Kind == AttributeTargetKind.Return ? MetadataFacts.ReturnAttributes : MetadataFacts.Attributes;
        availability = source.IsKnown(fact) ? MetadataAvailability.Complete :
            new MetadataAvailability(fact, source.Diagnostics);
        return target.Kind switch
        {
            AttributeTargetKind.Method => target.Method!.Attributes,
            AttributeTargetKind.Property => target.Property!.Attributes,
            AttributeTargetKind.Parameter => target.Parameter!.Attributes,
            AttributeTargetKind.Return => target.Method!.ReturnAttributes,
            _ => null
        };
    }

    private AttributeSignature[]? GetTypeAttributes(TypeSignature type, CecilMappingOperation operation, MappingContext resources, out MetadataAvailability availability)
    {
        availability = MetadataAvailability.Complete;
        if (!Mapper.TryGetTypeReference(type, out var reference) || reference is null) return null;
        if (reference is ArrayType or PointerType or FunctionPointerType or PinnedType or SentinelType)
            return Array.Empty<AttributeSignature>();
        return CecilMetadataReader.Read(() =>
        {
            // 引用上的泛型槽位不保证携带声明记录；只有声明所属的形参可直接读取。
            var attributes = reference switch
            {
                GenericParameter { Owner: TypeDefinition or MethodDefinition } parameter => parameter.CustomAttributes,
                GenericParameter => null,
                _ => (reference as TypeDefinition ?? reference.Resolve())?.CustomAttributes
            };
            return attributes?
                .Select(attribute => { resources.ScanAttribute(); return new AttributeSignature(operation.MapType(attribute.AttributeType), AttributeTargetKind.Type); })
                .ToArray();
        }, "type attribute target", MetadataFacts.Attributes, out availability);
    }
}
