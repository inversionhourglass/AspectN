using AspectN.Cecil.Internal;
using AspectN.Metadata;
using Mono.Cecil;

namespace AspectN.Cecil;

public sealed class CecilSignatureMapper
{
    private readonly CecilTypeCache _cache = new();
    private readonly CecilTypeSystem _typeSystem;

    internal CecilSignatureMapper(CecilTypeSystem typeSystem)
    {
        _typeSystem = typeSystem;
    }

    public TypeSignature MapType(TypeReference typeReference)
    {
#if NET10_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(typeReference);
#else
        if (typeReference is null)
            throw new ArgumentNullException(nameof(typeReference));
#endif

        _typeSystem.ThrowIfDisposed();
        return _cache.GetOrAdd(
            typeReference,
            () => CecilTypeMapper.Map(typeReference, MapType));
    }

    public TypeUseSignature MapTypeUse(
        TypeReference typeReference,
        TypeUseKind useKind)
    {
        if (typeReference is ByReferenceType byReference)
        {
            return new TypeUseSignature(
                MapType(byReference.ElementType),
                RefKind.Ref,
                useKind);
        }

        return new TypeUseSignature(
            MapType(typeReference),
            RefKind.None,
            useKind);
    }

    internal void ClearCache()
    {
        _cache.Clear();
    }
}
