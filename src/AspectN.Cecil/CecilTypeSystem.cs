using AspectN.Diagnostics;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Mono.Cecil;

namespace AspectN.Cecil;

public sealed class CecilTypeSystem : ITypeSystem, IDisposable
{
    private ModuleDefinition? _module;

    public CecilTypeSystem(
        ModuleDefinition module,
        CecilMappingOptions? options = null)
    {
        _module = module ?? throw new ArgumentNullException(nameof(module));
        Options = options ?? new CecilMappingOptions();
        Mapper = new CecilSignatureMapper(this);
    }

    public ModuleDefinition Module
    {
        get
        {
            ThrowIfDisposed();
            return _module!;
        }
    }

    public CecilMappingOptions Options { get; }

    public CecilSignatureMapper Mapper { get; }

    public TypeSystemCapabilities Capabilities =>
        TypeSystemCapabilities.TypeEquality;

    public TypeRelationResult AreEqual(
        TypeSignature left,
        TypeSignature right)
    {
        ThrowIfDisposed();
        return StructuralTypeComparer.AreEqual(left, right);
    }

    public TypeRelationResult IsAssignableTo(
        TypeSignature candidate,
        TypeSignature target,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        var equality = StructuralTypeComparer.AreEqual(candidate, target);
        return equality.Status == Evaluation.MatchStatus.Matched
            ? equality
            : MissingCapability("Assignability is not implemented yet.");
    }

    public TypeRelationResult HasDirectAttribute(
        AttributeTarget target,
        TypeSignature attributeType,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        return MissingCapability("Direct attribute lookup is not implemented yet.");
    }

    public void Dispose()
    {
        var module = Interlocked.Exchange(ref _module, null);
        Mapper.ClearCache();
        module?.Dispose();
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
}
