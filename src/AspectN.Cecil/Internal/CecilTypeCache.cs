using System.Runtime.CompilerServices;
using System.Text;
using AspectN.Metadata;
using Mono.Cecil;

namespace AspectN.Cecil.Internal;

internal sealed class CecilTypeCache
{
    private readonly Dictionary<TypeReference, TypeSignature> _byReference =
        new(TypeReferenceIdentityComparer.Instance);
    private readonly Dictionary<CecilTypeCacheKey, TypeSignature> _byStableIdentity =
        new();
    private readonly Dictionary<TypeSignature, TypeReference> _referenceBySignature =
        new(TypeSignatureIdentityComparer.Instance);
    private readonly object _gate = new();

    public TypeSignature GetOrAdd(
        TypeReference typeReference,
        Func<TypeSignature> factory)
    {
        lock (_gate)
        {
            if (_byReference.TryGetValue(typeReference, out var cached))
            {
                return cached;
            }

            var key = CecilTypeCacheKey.Create(typeReference);
            if (_byStableIdentity.TryGetValue(key, out cached))
            {
                _byReference.Add(typeReference, cached);
                return cached;
            }

            var created = factory();
            _byReference.Add(typeReference, created);
            _byStableIdentity.Add(key, created);
#if NET10_0_OR_GREATER
            _referenceBySignature.TryAdd(created, typeReference);
#else
            if (!_referenceBySignature.ContainsKey(created))
            {
                _referenceBySignature.Add(created, typeReference);
            }
#endif
            return created;
        }
    }

    public bool TryGetReference(
        TypeSignature signature,
        out TypeReference? typeReference)
    {
        lock (_gate)
        {
            return _referenceBySignature.TryGetValue(
                signature,
                out typeReference);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _byReference.Clear();
            _byStableIdentity.Clear();
            _referenceBySignature.Clear();
        }
    }

    private sealed class TypeReferenceIdentityComparer :
        IEqualityComparer<TypeReference>
    {
        private static readonly TypeReferenceIdentityComparer _Instance = new();

        private TypeReferenceIdentityComparer()
        {
        }

        public static TypeReferenceIdentityComparer Instance => _Instance;

        public bool Equals(TypeReference? x, TypeReference? y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(TypeReference obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }

    private sealed class TypeSignatureIdentityComparer :
        IEqualityComparer<TypeSignature>
    {
        private static readonly TypeSignatureIdentityComparer _Instance = new();

        private TypeSignatureIdentityComparer()
        {
        }

        public static TypeSignatureIdentityComparer Instance => _Instance;

        public bool Equals(TypeSignature? x, TypeSignature? y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(TypeSignature obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}

internal readonly struct CecilTypeCacheKey : IEquatable<CecilTypeCacheKey>
{
    private CecilTypeCacheKey(
        MetadataType metadataType,
        string scopeIdentity,
        string shapeIdentity)
    {
        MetadataType = metadataType;
        ScopeIdentity = scopeIdentity;
        ShapeIdentity = shapeIdentity;
    }

    public MetadataType MetadataType { get; }

    public string ScopeIdentity { get; }

    public string ShapeIdentity { get; }

    public static CecilTypeCacheKey Create(TypeReference typeReference)
    {
        return new CecilTypeCacheKey(
            typeReference.MetadataType,
            GetScopeIdentity(typeReference),
            BuildShapeIdentity(typeReference));
    }

    public bool Equals(CecilTypeCacheKey other)
    {
        return MetadataType == other.MetadataType &&
               StringComparer.Ordinal.Equals(
                   ScopeIdentity,
                   other.ScopeIdentity) &&
               StringComparer.Ordinal.Equals(
                   ShapeIdentity,
                   other.ShapeIdentity);
    }

    public override bool Equals(object? obj)
    {
        return obj is CecilTypeCacheKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = (int)MetadataType;
            hash = (hash * 397) ^
                   StringComparer.Ordinal.GetHashCode(ScopeIdentity);
            hash = (hash * 397) ^
                   StringComparer.Ordinal.GetHashCode(ShapeIdentity);
            return hash;
        }
    }

    private static string BuildShapeIdentity(TypeReference typeReference)
    {
        var builder = new StringBuilder();
        AppendShape(builder, typeReference);
        return builder.ToString();
    }

    private static void AppendShape(
        StringBuilder builder,
        TypeReference typeReference)
    {
        switch (typeReference)
        {
            case GenericInstanceType generic:
                builder.Append("generic(");
                AppendShape(builder, generic.ElementType);
                builder.Append('<');
                for (var index = 0;
                     index < generic.GenericArguments.Count;
                     index++)
                {
                    if (index > 0) builder.Append(',');
                    AppendShape(builder, generic.GenericArguments[index]);
                }

                builder.Append(">)");
                return;
            case ArrayType array:
                builder.Append("array:");
                builder.Append(array.Rank);
                builder.Append('(');
                AppendShape(builder, array.ElementType);
                builder.Append(')');
                return;
            case GenericParameter parameter:
                builder.Append("parameter:");
                builder.Append(parameter.Type);
                builder.Append(':');
                builder.Append(GetOwnerIdentity(parameter));
                builder.Append(':');
                builder.Append(parameter.Position);
                return;
            case TypeSpecification specification:
                builder.Append(typeReference.GetType().FullName);
                builder.Append('(');
                AppendShape(builder, specification.ElementType);
                builder.Append(')');
                return;
            default:
                builder.Append("named:");
                builder.Append(typeReference.GetType().FullName);
                builder.Append(':');
                builder.Append(GetScopeIdentity(typeReference));
                builder.Append(':');
                builder.Append(typeReference.FullName);
                builder.Append(':');
                builder.Append(typeReference.IsValueType);
                return;
        }
    }

    private static string GetScopeIdentity(TypeReference typeReference)
    {
        if (typeReference.Scope is AssemblyNameReference assembly)
        {
            return assembly.FullName;
        }

        if (typeReference.Scope is ModuleDefinition module)
        {
            return module.Assembly?.Name.FullName ?? module.Name;
        }

        if (typeReference.Module?.Assembly is not null)
        {
            return typeReference.Module.Assembly.Name.FullName;
        }

        return typeReference.Scope?.Name ?? string.Empty;
    }

    private static string GetOwnerIdentity(GenericParameter parameter)
    {
        return parameter.Owner switch
        {
            TypeReference type => type.FullName,
            MethodReference method => GetMethodOwnerIdentity(method),
            _ => parameter.Owner?.GetType().FullName ?? string.Empty
        };
    }

    private static string GetMethodOwnerIdentity(MethodReference method)
    {
        return $"{method.DeclaringType.FullName}.{method.Name}`" +
               method.GenericParameters.Count;
    }
}
