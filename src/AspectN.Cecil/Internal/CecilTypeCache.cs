using System.Runtime.CompilerServices;
using System.Text;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Mono.Cecil;

namespace AspectN.Cecil.Internal;

internal sealed class CecilTypeCache
{
    private readonly Dictionary<TypeReference, TypeSignature> _byReference =
        new(TypeReferenceIdentityComparer.Instance);
    private readonly Dictionary<int, List<(CecilTypeCacheKey Key, TypeSignature Signature)>> _byStableIdentity =
        new();
    private readonly Dictionary<TypeSignature, TypeReference> _referenceBySignature =
        new(TypeSignatureIdentityComparer.Instance);
    private readonly Dictionary<TypeReference, CecilTypeCacheKey> _keys = new(TypeReferenceIdentityComparer.Instance);
    private readonly Dictionary<TypeReference, string> _names = new(TypeReferenceIdentityComparer.Instance);
    private readonly Dictionary<string, string> _nameIds = new(StringComparer.Ordinal);
    private readonly Dictionary<(IMetadataScope Scope, ModuleDefinition? Fallback), string> _scopes = new(ScopeComparer.Instance);
    private readonly Dictionary<string, string> _scopeIds = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public TypeSignature? Find(TypeReference type, MappingContext resources)
    {
        lock (_gate)
        {
            if (_byReference.TryGetValue(type, out var mapped)) return mapped;
            var key = GetKey(type, resources);
            if (!TryFind(key, resources, out mapped)) return null;
            _byReference.Add(type, mapped);
            return mapped;
        }
    }

    public TypeSignature GetOrAdd(
        TypeReference typeReference,
        Func<TypeSignature> factory, MappingContext resources)
    {
        lock (_gate)
        {
            if (_byReference.TryGetValue(typeReference, out var cached))
            {
                return cached;
            }

            var key = GetKey(typeReference, resources);
            if (TryFind(key, resources, out cached))
            {
                _byReference.Add(typeReference, cached);
                return cached;
            }

            var created = factory();
            resources.SpendWork();
            _byReference.Add(typeReference, created);
            if (!_byStableIdentity.TryGetValue(key.GetHashCode(), out var bucket))
            {
                bucket = [];
                _byStableIdentity.Add(key.GetHashCode(), bucket);
            }
            bucket.Add((key, created));
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

    private bool TryFind(CecilTypeCacheKey key, MappingContext resources, out TypeSignature signature)
    {
        if (_byStableIdentity.TryGetValue(key.GetHashCode(), out var bucket))
            foreach (var item in bucket)
            {
                resources.SpendWork();
                if (!key.Equals(item.Key, resources)) continue;
                signature = item.Signature;
                return true;
            }
        signature = null!;
        return false;
    }

    private string GetNameId(TypeReference type, MappingContext resources)
    {
        resources.SpendWork();
        if (_names.TryGetValue(type, out var id)) return id;
        var text = resources.Concat(type.GetType().FullName!, ":", GetScopeId(type, resources), ":",
            CecilNativeTypeMetadata.FullName(type, resources), ":", type.IsValueType.ToString());
        resources.SpendWork(Math.Max(1L, text.Length * 3L));
        if (!_nameIds.TryGetValue(text, out id))
        {
            id = _nameIds.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _nameIds.Add(text, id);
        }
        _names.Add(type, id);
        return id;
    }

    private string GetScopeId(TypeReference type, MappingContext resources)
    {
        resources.SpendWork();
        var scope = CecilNativeTypeMetadata.GetScope(type, resources);
        var fallback = scope is AssemblyNameReference or ModuleDefinition ? null : CecilNativeTypeMetadata.GetModule(type, resources);
        var scopeKey = (scope!, fallback);
        if (scope is not null && _scopes.TryGetValue(scopeKey, out var id)) return id;
        var text = CecilTypeCacheKey.FormatScope(type, resources);
        resources.SpendWork(Math.Max(1L, text.Length * 3L));
        if (!_scopeIds.TryGetValue(text, out id))
        {
            id = _scopeIds.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _scopeIds.Add(text, id);
        }
        if (scope is not null) _scopes.Add(scopeKey, id);
        return id;
    }

    private sealed class ScopeComparer : IEqualityComparer<(IMetadataScope Scope, ModuleDefinition? Fallback)>
    {
        public static readonly ScopeComparer Instance = new();
        public bool Equals((IMetadataScope Scope, ModuleDefinition? Fallback) left, (IMetadataScope Scope, ModuleDefinition? Fallback) right) =>
            ReferenceEquals(left.Scope, right.Scope) && ReferenceEquals(left.Fallback, right.Fallback);
        public int GetHashCode((IMetadataScope Scope, ModuleDefinition? Fallback) value) =>
            unchecked(RuntimeHelpers.GetHashCode(value.Scope) * 397 ^ (value.Fallback is null ? 0 : RuntimeHelpers.GetHashCode(value.Fallback)));
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

    public CecilTypeCacheKey GetKey(TypeReference type, MappingContext resources)
    {
        resources.SpendWork();
        lock (_gate)
        {
            if (!_keys.TryGetValue(type, out var key))
            {
                key = CecilTypeCacheKey.Create(type, resources, GetScopeId, GetNameId);
                _keys.Add(type, key);
            }
            return key;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _byReference.Clear();
            _byStableIdentity.Clear();
            _referenceBySignature.Clear();
            _keys.Clear();
            _names.Clear();
            _nameIds.Clear();
            _scopes.Clear();
            _scopeIds.Clear();
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
        string shapeIdentity, int hashCode)
    {
        MetadataType = metadataType;
        ScopeIdentity = scopeIdentity;
        ShapeIdentity = shapeIdentity;
        _hashCode = hashCode;
    }

    private readonly int _hashCode;

    public MetadataType MetadataType { get; }

    public string ScopeIdentity { get; }

    public string ShapeIdentity { get; }

    public static CecilTypeCacheKey Create(TypeReference typeReference, MappingContext resources, Func<TypeReference, MappingContext, string> scopeId, Func<TypeReference, MappingContext, string> nameId)
    {
        resources.SpendWork();
        var scope = scopeId(typeReference, resources);
        var shape = BuildShapeIdentity(typeReference, resources, scopeId, nameId);
        resources.SpendWork(Math.Max(1L, scope.Length + (long)shape.Length));
        var hash = unchecked(((int)typeReference.MetadataType * 397 ^ StringComparer.Ordinal.GetHashCode(scope)) * 397 ^ StringComparer.Ordinal.GetHashCode(shape));
        return new CecilTypeCacheKey(typeReference.MetadataType, scope, shape, hash);
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

    public override int GetHashCode() => _hashCode;

    public bool Equals(CecilTypeCacheKey other, MappingContext resources) => MetadataType == other.MetadataType &&
        resources.StringsEqual(ScopeIdentity, other.ScopeIdentity) && resources.StringsEqual(ShapeIdentity, other.ShapeIdentity);


    private static string BuildShapeIdentity(TypeReference typeReference, MappingContext resources, Func<TypeReference, MappingContext, string> scopeId, Func<TypeReference, MappingContext, string> nameId)
    {
        var builder = new StringBuilder();
        AppendShape(builder, typeReference, resources, scopeId, nameId);
        return builder.ToString();
    }

    private static void AppendShape(
        StringBuilder builder,
        TypeReference typeReference, MappingContext resources, Func<TypeReference, MappingContext, string> scopeId, Func<TypeReference, MappingContext, string> nameId)
    {
        var pending = new Stack<(TypeReference? Type, string? Text)>();
        pending.Push((typeReference, null));
        while (pending.Count > 0)
        {
            resources.SpendWork();
            var item = pending.Pop();
            if (item.Text is not null)
            {
                Append(builder, item.Text, resources);
                continue;
            }
            typeReference = item.Type!;
            switch (typeReference)
            {
                case GenericInstanceType generic:
                    Append(builder, "generic(", resources);
                    pending.Push((null, ">)"));
                    if (generic.GenericArguments.Count > 0) resources.SpendWork(2L * generic.GenericArguments.Count);
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
                    Append(builder, "array:", resources);
                    Append(builder, array.Rank, resources);
                    Append(builder, ':', resources);
                    Append(builder, array.IsVector, resources);
                    Append(builder, '(', resources);
                    pending.Push((null, ")"));
                    pending.Push((array.ElementType, null));
                    break;
                case GenericParameter parameter:
                    Append(builder, "parameter:", resources);
                    // 声明形参与引用占位的可用事实不同，不能共用反向原生关联。
                    Append(builder, parameter.Owner is TypeDefinition or MethodDefinition, resources);
                    Append(builder, ':', resources);
                    Append(builder, parameter.Type, resources);
                    Append(builder, ':', resources);
                    Append(builder, GetOwnerIdentity(parameter, resources, scopeId, nameId), resources);
                    Append(builder, ':', resources);
                    Append(builder, parameter.Position, resources);
                    break;
                case FunctionPointerType functionPointer:
                    // 函数指针派生自 TypeSpecification 但 ElementType 恒为 null，不能递归；
                    // FullName 携带返回类型与参数签名，但不含调用约定，需一并进入键。
                    Append(builder, "function-pointer:", resources);
                    Append(builder, (int)functionPointer.CallingConvention, resources);
                    Append(builder, ':', resources);
                    Append(builder, new CecilTypeMapper(resources).CanonicalTypeText(functionPointer), resources);
                    break;
                case TypeSpecification specification:
                    Append(builder, typeReference.GetType().FullName, resources);
                    Append(builder, '(', resources);
                    pending.Push((null, ")"));
                    if (specification.ElementType is null)
                    {
                        // 其他无元素类型的规格类型同样短路为字面量标记，
                        // 映射阶段统一产出 UnsupportedTypeSignature。
                        Append(builder, "null-element", resources);
                    }
                    else
                    {
                        pending.Push((specification.ElementType, null));
                    }
                    break;
                default:
                    Append(builder, "named:", resources);
                    Append(builder, nameId(typeReference, resources), resources);
                    break;
            }
        }
    }

    private static void Append(StringBuilder builder, object? value, MappingContext resources)
    {
        var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        resources.CheckTextLength(builder.Length, text.Length);
        builder.Append(text);
    }

    internal static string FormatScope(TypeReference typeReference, MappingContext resources)
    {
        var scope = CecilNativeTypeMetadata.GetScope(typeReference, resources);
        var assembly = scope as AssemblyNameReference ?? (scope as ModuleDefinition)?.Assembly?.Name ??
            CecilNativeTypeMetadata.GetModule(typeReference, resources)?.Assembly?.Name;
        if (assembly is null) return resources.Text(scope?.Name ?? string.Empty);
        var token = assembly.PublicKeyToken ?? [];
        if (token.Length > 0) resources.CheckTextLength(0, token.Length * 2L);
        var hex = string.Concat(token.Select(value => value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));
        return AspectN.AdapterInternals.GenericOwnerIdentity.Qualify(resources, string.Empty,
            new AssemblyIdentity(assembly.Name, assembly.Version, assembly.Culture, hex));
    }

    private static string GetOwnerIdentity(GenericParameter parameter, MappingContext resources, Func<TypeReference, MappingContext, string> scopeId, Func<TypeReference, MappingContext, string> nameId)
    {
        // 与 CecilTypeMapper 的 owner ID 使用同一公式：参数形状参与身份，避免重载碰撞导致缓存串用。
        var owner = new CecilTypeMapper(resources).GetGenericParameterOwner(parameter);
        // 声明身份相同的形参在不同闭合类型下仍有不同约束；缓存必须保留查询上下文。
        return parameter.Owner is MethodReference { DeclaringType: GenericInstanceType } method
            ? resources.Concat(owner, "#context:", BuildShapeIdentity(method.DeclaringType, resources, scopeId, nameId))
            : owner;
    }
}
