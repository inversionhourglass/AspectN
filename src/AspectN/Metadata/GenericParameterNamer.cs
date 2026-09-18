namespace AspectN.Metadata;

// 定义签名的形式泛型参数按扁平 ECMA 槽位合成 T1..Tn：外层声明类型从左到右，
// 再内层类型，最后方法泛型；名字随槽位编号，跨层级不重复。槽位模型必须与三个
// 适配器映射真实泛型参数的 (OwnerId, Position) 逐字节一致，否则使用位置的
// 重命名查表会落空（历史缺陷：内层形参按层级独立编号导致同一形参双名渲染、
// 变量重绑定静默 NotMatched）。Legacy 与 Standard 的规范签名共用此合成逻辑。
internal static class GenericParameterNamer
{
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
                            identity.FullMetadataName,
                            identity.Assembly,
                            slot)));
            }
        }

        return arguments.ToArray();
    }

    // 泛型形参的位置名：类型形参先占 T1..Tm（外层从左到右、再内层），
    // 方法形参接续 T{m+1}..T{m+n}。Standard 定义签名统一使用该编码；
    // Legacy 兼容路径另有“总数为 1 时用裸 T”的历史特例，由其自身处理。
    public static Dictionary<GenericParameterKey, string> CreatePositionalNames(
        CallableSignature candidate)
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
        var slot = 0;
        foreach (var parameter in typeParameters)
        {
            names[new GenericParameterKey(parameter)] = $"T{++slot}";
        }

        foreach (var parameter in methodParameters)
        {
            names[new GenericParameterKey(parameter)] = $"T{++slot}";
        }

        return names;
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
    private readonly int _position = parameter.Position;

    public bool Equals(GenericParameterKey other)
    {
        return _ownerKind == other._ownerKind &&
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
            hash = (hash * 397) ^ _position;
            hash = (hash * 397) ^
                   StringComparer.Ordinal.GetHashCode(_ownerId);
            return hash;
        }
    }
}
