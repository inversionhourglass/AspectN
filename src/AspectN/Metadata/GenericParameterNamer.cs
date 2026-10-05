namespace AspectN.Metadata;

// 定义签名的形式泛型参数按扁平 ECMA 槽位合成 T1..Tn：外层声明类型从左到右，
// 再内层类型，最后方法泛型；名字随槽位编号，跨层级不重复。槽位模型必须与三个
// 适配器映射真实泛型参数的 (OwnerId, Position) 逐字节一致，否则使用位置的
// 重命名查表会落空（历史缺陷：内层形参按层级独立编号导致同一形参双名渲染、
// 变量重绑定静默 NotMatched）。匹配和规范签名共用此合成逻辑。
internal static class GenericParameterNamer
{
    public static TypeSignature[] GetEffectiveMethodArguments(MethodSignature candidate)
    {
        if (candidate.GenericArguments.Count > 0) return candidate.GenericArguments.ToArray();

        return candidate.GenericParameters
            .Select(parameter => (TypeSignature)new GenericParameterTypeSignature(parameter))
            .ToArray();
    }

    public static TypeSignature[] GetEffectiveTypeArguments(
        NamedTypeSignature candidate)
    {
        var identities = GetTypeChain(candidate.Identity);
        var totalArity = identities.Sum(identity => identity.GenericArity);
        if (candidate.TypeArguments.Count == totalArity)
        {
            return candidate.TypeArguments.ToArray();
        }

        if (candidate.TypeArguments.Count != 0)
        {
            return Array.Empty<TypeSignature>();
        }

        var arguments = new List<TypeSignature>(totalArity);
        var slot = 0;
        foreach (var identity in identities)
        {
            var ownerId = identity.FullMetadataName;
            for (var localPosition = 0;
                 localPosition < identity.GenericArity;
                 localPosition++,
                 slot++)
            {
                arguments.Add(
                    new GenericParameterTypeSignature(
                        new GenericParameterSignature(
                            $"T{slot + 1}",
                            GenericParameterOwnerKind.Type,
                            ownerId,
                            identity.Assembly,
                            slot)));
            }
        }

        return arguments.ToArray();
    }

    // 泛型形参的位置名：类型形参先占 T1..Tm（外层从左到右、再内层），
    // 方法形参接续 T{m+1}..T{m+n}；总数（类型+方法合并计数）为 1 时渲染为
    // 裸 T。规范签名格式化器使用此规则（§15）。
    public static Dictionary<GenericParameterKey, string> CreatePositionalNames(
        MethodSignature candidate)
    {
        var declaring = candidate.DeclaringType as NamedTypeSignature;
        var typeParameters = (declaring is null
                ? Array.Empty<TypeSignature>()
                : GetEffectiveTypeArguments(declaring))
            .OfType<GenericParameterTypeSignature>()
            .Select(parameter => parameter.Parameter)
            .Where(parameter =>
                parameter.OwnerKind == GenericParameterOwnerKind.Type)
            .ToArray();
        var methodParameters = candidate.GenericParameters.ToArray();
        var names = new Dictionary<GenericParameterKey, string>();
        foreach (var (parameter, name) in EnumeratePositionalNames(
                     typeParameters,
                     methodParameters))
        {
            names[new GenericParameterKey(parameter)] = name;
        }

        return names;
    }

    internal static IEnumerable<(GenericParameterSignature Parameter, string Name)>
        EnumeratePositionalNames(
            GenericParameterSignature[] typeParameters,
            GenericParameterSignature[] methodParameters)
    {
        if (typeParameters.Length + methodParameters.Length == 1)
        {
            var single = typeParameters.Length == 1
                ? typeParameters[0]
                : methodParameters[0];
            yield return (single, "T");
            yield break;
        }

        var slot = 0;
        foreach (var parameter in typeParameters)
        {
            yield return (parameter, $"T{++slot}");
        }

        foreach (var parameter in methodParameters)
        {
            yield return (parameter, $"T{++slot}");
        }
    }

    internal static List<TypeIdentity> GetTypeChain(TypeIdentity identity)
    {
        var identities = new List<TypeIdentity>();
        for (var current = identity;
             current is not null;
             current = current.ContainingType)
        {
            identities.Add(current);
        }

        identities.Reverse();
        return identities;
    }
}

internal readonly struct GenericParameterKey(GenericParameterSignature parameter) :
    IEquatable<GenericParameterKey>
{
    private readonly GenericParameterOwnerKind _ownerKind = parameter.OwnerKind;
    private readonly string _ownerId = parameter.OwnerId;
    private readonly AssemblyIdentity _ownerAssembly = parameter.OwnerAssembly;
    private readonly int _position = parameter.Position;

    public bool Equals(GenericParameterKey other)
    {
        return _ownerKind == other._ownerKind &&
               _ownerAssembly.Equals(other._ownerAssembly) &&
               _position == other._position &&
               StringComparer.Ordinal.Equals(_ownerId, other._ownerId);
    }

    public override bool Equals(object? obj)
    {
        return obj is GenericParameterKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = (int)_ownerKind;
            hash = (hash * 397) ^ _ownerAssembly.GetHashCode();
            hash = (hash * 397) ^ _position;
            hash = (hash * 397) ^
                   StringComparer.Ordinal.GetHashCode(_ownerId);
            return hash;
        }
    }
}
