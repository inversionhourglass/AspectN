using AspectN.Cecil.Internal;
using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Mono.Cecil;

namespace AspectN.Cecil;

/// <summary>Provides Mono.Cecil-backed type and metadata operations for AspectN matching.</summary>
public sealed class CecilTypeSystem : ITypeSystem, IDisposable
{
    private readonly CecilResolver _resolver;
    private ModuleDefinition? _module;

    /// <summary>Initializes a new instance of the CecilTypeSystem class.</summary>
    /// <param name="module">Specifies the module.</param>
    /// <param name="options">Specifies the options.</param>
    public CecilTypeSystem(
        ModuleDefinition module,
        CecilMappingOptions? options = null)
    {
        _module = module ?? throw new ArgumentNullException(nameof(module));
        Options = options ?? new CecilMappingOptions();
        Mapper = new CecilSignatureMapper(this);
        _resolver = new CecilResolver(Mapper);
    }

    /// <summary>Gets the module.</summary>
    public ModuleDefinition Module
    {
        get
        {
            ThrowIfDisposed();
            return _module!;
        }
    }

    /// <summary>Gets the options.</summary>
    public CecilMappingOptions Options { get; }

    /// <summary>Gets the mapper.</summary>
    public CecilSignatureMapper Mapper { get; }

    /// <summary>Gets the capabilities.</summary>
    public TypeSystemCapabilities Capabilities =>
        TypeSystemCapabilities.TypeEquality |
        TypeSystemCapabilities.BaseTypeTraversal |
        TypeSystemCapabilities.InterfaceTraversal |
        TypeSystemCapabilities.Assignability |
        TypeSystemCapabilities.GenericSubstitution |
        TypeSystemCapabilities.DirectAttributeLookup |
        TypeSystemCapabilities.OriginalDefinitionMapping;

    /// <summary>Compares two values for structural equality.</summary>
    /// <param name="left">Specifies the left.</param>
    /// <param name="right">Specifies the right.</param>
    /// <returns>Returns the result of the operation.</returns>
    public TypeRelationResult AreEqual(
        TypeSignature left,
        TypeSignature right)
    {
        ThrowIfDisposed();
        return StructuralTypeComparer.AreEqual(left, right);
    }

    /// <summary>Determines whether is assignable to.</summary>
    /// <param name="candidate">Specifies the candidate.</param>
    /// <param name="target">Specifies the target.</param>
    /// <param name="cancellationToken">Specifies the cancellation token.</param>
    /// <returns>Returns the result of the operation.</returns>
    public TypeRelationResult IsAssignableTo(
        TypeSignature candidate,
        TypeSignature target,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _resolver.IsAssignableTo(candidate, target, cancellationToken);
    }

    /// <summary>Determines whether has direct attribute.</summary>
    /// <param name="target">Specifies the target.</param>
    /// <param name="attributeType">Specifies the attribute type.</param>
    /// <param name="cancellationToken">Specifies the cancellation token.</param>
    /// <returns>Returns the result of the operation.</returns>
    public TypeRelationResult HasDirectAttribute(
        AttributeTarget target,
        TypeSignature attributeType,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(attributeType);
#else
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (attributeType is null)
            throw new ArgumentNullException(nameof(attributeType));
#endif

        var attributes = GetAttributes(target);
        if (attributes is null)
        {
            return MissingCapability(
                "No Cecil reference is available for the attribute target.");
        }

        TypeRelationResult? unknown = null;
        foreach (var attribute in attributes)
        {
            var relation = UnqualifiedTypeComparer.AreEqual(
                attribute.AttributeType,
                attributeType);
            if (relation.Status == MatchStatus.Matched) return relation;
            if (relation.Status == MatchStatus.Unknown) unknown = relation;
        }

        return unknown ?? TypeRelationResult.NotMatched;
    }

    /// <summary>Releases the resources owned by this instance.</summary>
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

    private IReadOnlyList<AttributeSignature>? GetAttributes(
        AttributeTarget target)
    {
        return target.Kind switch
        {
            AttributeTargetKind.Type => GetTypeAttributes(target.Type!),
            AttributeTargetKind.Callable => target.Callable!.Attributes,
            AttributeTargetKind.Property => target.Property!.Attributes,
            AttributeTargetKind.Parameter => target.Parameter!.Attributes,
            AttributeTargetKind.Return => target.Callable!.ReturnAttributes,
            _ => null
        };
    }

    private AttributeSignature[]? GetTypeAttributes(
        TypeSignature type)
    {
        if (!Mapper.TryGetTypeReference(type, out var reference) ||
            reference is null)
        {
            return null;
        }

        TypeDefinition? definition;
        try
        {
            definition = reference as TypeDefinition ?? reference.Resolve();
        }
        catch (AssemblyResolutionException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }

        return definition?.CustomAttributes
            .Select(attribute => new AttributeSignature(
                Mapper.MapType(attribute.AttributeType),
                AttributeTargetKind.Type))
            .ToArray();
    }
}
