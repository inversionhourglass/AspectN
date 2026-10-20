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

    public TypeSignature? Find(TypeReference type)
    {
        lock (_gate)
        {
            if (_byReference.TryGetValue(type, out var mapped)) return mapped;
            var key = CecilTypeCacheKey.Create(type);
            if (!_byStableIdentity.TryGetValue(key, out mapped)) return null;
            _byReference.Add(type, mapped);
            return mapped;
        }
    }

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
        var pending = new Stack<(TypeReference? Type, string? Text)>();
        pending.Push((typeReference, null));
        while (pending.Count > 0)
        {
            var item = pending.Pop();
            if (item.Text is not null)
            {
                builder.Append(item.Text);
                continue;
            }
            typeReference = item.Type!;
            switch (typeReference)
            {
                case GenericInstanceType generic:
                    builder.Append("generic(");
                    pending.Push((null, ">)"));
                    for (var index = generic.GenericArguments.Count - 1; index >= 0; index--)
                    {
                        pending.Push((generic.GenericArguments[index], null));
                        if (index > 0) pending.Push((null, ","));
                    }
                    pending.Push((null, "<"));
                    pending.Push((generic.ElementType, null));
                    break;
                case ArrayType array:
                    // 向量数组与非零下界的一维数组是不同形状，不能共用缓存身份。
                    builder.Append("array:");
                    builder.Append(array.Rank);
                    builder.Append(':');
                    builder.Append(array.IsVector);
                    builder.Append('(');
                    pending.Push((null, ")"));
                    pending.Push((array.ElementType, null));
                    break;
                case GenericParameter parameter:
                    builder.Append("parameter:");
                    // 声明形参与引用占位的可用事实不同，不能共用反向原生关联。
                    builder.Append(parameter.Owner is TypeDefinition or MethodDefinition);
                    builder.Append(':');
                    builder.Append(parameter.Type);
                    builder.Append(':');
                    builder.Append(GetOwnerIdentity(parameter));
                    builder.Append(':');
                    builder.Append(parameter.Position);
                    break;
                case FunctionPointerType functionPointer:
                    // 函数指针派生自 TypeSpecification 但 ElementType 恒为 null，不能递归；
                    // FullName 携带返回类型与参数签名，但不含调用约定，需一并进入键。
                    builder.Append("function-pointer:");
                    builder.Append((int)functionPointer.CallingConvention);
                    builder.Append(':');
                    builder.Append(CecilTypeMapper.CanonicalTypeText(functionPointer));
                    break;
                case TypeSpecification specification:
                    builder.Append(typeReference.GetType().FullName);
                    builder.Append('(');
                    pending.Push((null, ")"));
                    if (specification.ElementType is null)
                    {
                        // 其他无元素类型的规格类型同样短路为字面量标记，
                        // 映射阶段统一产出 UnsupportedTypeSignature。
                        builder.Append("null-element");
                    }
                    else
                    {
                        pending.Push((specification.ElementType, null));
                    }
                    break;
                default:
                    builder.Append("named:");
                    builder.Append(typeReference.GetType().FullName);
                    builder.Append(':');
                    builder.Append(GetScopeIdentity(typeReference));
                    builder.Append(':');
                    builder.Append(CecilNativeTypeMetadata.FullName(typeReference));
                    builder.Append(':');
                    builder.Append(typeReference.IsValueType);
                    break;
            }
        }
    }

    private static string GetScopeIdentity(TypeReference typeReference)
    {
        var scope = CecilNativeTypeMetadata.GetScope(typeReference);
        if (scope is AssemblyNameReference assembly)
        {
            return assembly.FullName;
        }

        if (scope is ModuleDefinition module)
        {
            return module.Assembly?.Name.FullName ?? module.Name;
        }

        var referenceModule = CecilNativeTypeMetadata.GetModule(typeReference);
        if (referenceModule?.Assembly is not null)
        {
            return referenceModule.Assembly.Name.FullName;
        }

        return scope?.Name ?? string.Empty;
    }

    private static string GetOwnerIdentity(GenericParameter parameter)
    {
        // 与 CecilTypeMapper 的 owner ID 使用同一公式：参数形状参与身份，避免重载碰撞导致缓存串用。
        var owner = CecilTypeMapper.GetGenericParameterOwner(parameter);
        // 声明身份相同的形参在不同闭合类型下仍有不同约束；缓存必须保留查询上下文。
        return parameter.Owner is MethodReference { DeclaringType: GenericInstanceType } method
            ? owner + "#context:" + BuildShapeIdentity(method.DeclaringType)
            : owner;
    }
}
