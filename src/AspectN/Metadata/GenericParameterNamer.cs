using AspectN.TypeSystem;

namespace AspectN.Metadata;

// 定义签名的形式泛型参数按扁平 ECMA 槽位合成 T1..Tn：外层声明类型从左到右，
// 再内层类型，最后方法泛型；名字随槽位编号，跨层级不重复。槽位模型必须与三个
// 适配器映射真实泛型参数的 (OwnerId, Position) 逐字节一致，否则使用位置的
// 重命名查表会落空（历史缺陷：内层形参按层级独立编号导致同一形参双名渲染、
// 变量重绑定静默 NotMatched）。匹配和规范签名共用此合成逻辑。
internal static class GenericParameterNamer
{
    // 元数来自原始定义或当前声明；省略两者的外部构造签名按完整实参推定。
    public static int GetMethodArity(MethodSignature candidate) =>
        candidate.OriginalDefinition is { GenericParameters.Count: > 0 } definition
            ? definition.GenericParameters.Count
            : candidate.GenericParameters.Count > 0 ? candidate.GenericParameters.Count : candidate.GenericArguments.Count;

    public static TypeSignature[] GetEffectiveMethodArguments(MethodSignature candidate, TypeSystemQueryContext context)
    {
        var count = candidate.GenericArguments.Count > 0 ? candidate.GenericArguments.Count : candidate.GenericParameters.Count;
        if (count > 0) context.SpendWork(count);
        if (candidate.GenericArguments.Count > 0)
            return candidate.GenericArguments.Count == GetMethodArity(candidate)
                ? candidate.GenericArguments.ToArray()
                : Array.Empty<TypeSignature>();

        return candidate.GenericParameters
            .Select(parameter => (TypeSignature)new GenericParameterTypeSignature(parameter))
            .ToArray();
    }

    public static TypeSignature[] GetEffectiveTypeArguments(
        NamedTypeSignature candidate, TypeSystemQueryContext? context = null)
    {
        var identities = GetTypeChain(candidate.Identity, context);
        var totalArity = 0;
        foreach (var identity in identities)
        {
            totalArity = checked(totalArity + identity.GenericArity);
        }
        if (totalArity > 0) context?.SpendWork(totalArity);
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
            var ownerId = GetOwnerId(identity, context);
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
        MethodSignature candidate,
        Action<NamedTypeSignature> validateEffectiveArity,
        TypeSystemQueryContext? context, CancellationToken cancellationToken)
    {
        var declaring = candidate.DeclaringType as NamedTypeSignature;
        var typeParameters = GetDeclaringTypeParameters(declaring, validateEffectiveArity, context, cancellationToken);
        var comparer = context is null ? null : new BudgetedGenericParameterKeyComparer(context);
        var seen = new HashSet<GenericParameterKey>(typeParameters.Select(parameter => new GenericParameterKey(parameter)), comparer);
        var methodParameters = candidate.GenericParameters
            .Where(parameter => seen.Add(new GenericParameterKey(parameter)))
            .ToArray();
        var names = new Dictionary<GenericParameterKey, string>(comparer);
        foreach (var (parameter, name) in EnumeratePositionalNames(
                     typeParameters,
                     methodParameters))
        {
            cancellationToken.ThrowIfCancellationRequested();
            context?.SpendWork();
            names[new GenericParameterKey(parameter)] = name;
        }

        return names;
    }

    private static GenericParameterSignature[] GetDeclaringTypeParameters(
        NamedTypeSignature? declaring,
        Action<NamedTypeSignature> validateEffectiveArity,
        TypeSystemQueryContext? context, CancellationToken cancellationToken)
    {
        if (declaring is null) return Array.Empty<GenericParameterSignature>();

        // 开放构造类型的形参可能藏在 List<T> 或 T[] 中；按实参从左到右首次出现
        // 的顺序编号，并按声明身份去重，避免同一个 T 占据多个位置名。
        var parameters = new List<GenericParameterSignature>();
        var seen = new HashSet<GenericParameterKey>(context is null ? null : new BudgetedGenericParameterKeyComparer(context));
        var visited = new HashSet<TypeSignature>();
        var pending = new Stack<TypeSignature>();
        pending.Push(declaring);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context?.SpendWork();
            var current = pending.Pop();
            if (!visited.Add(current)) continue;

            switch (current)
            {
                case GenericParameterTypeSignature parameter:
                    if (seen.Add(new GenericParameterKey(parameter.Parameter)))
                        parameters.Add(parameter.Parameter);
                    break;
                case NamedTypeSignature named:
                    validateEffectiveArity(named);
                    var arguments = GetEffectiveTypeArguments(named, context);
                    for (var index = arguments.Length - 1; index >= 0; index--)
                        pending.Push(arguments[index]);
                    break;
                case ArrayTypeSignature array:
                    pending.Push(array.ElementType);
                    break;
            }
        }

        return parameters.ToArray();
    }

    internal static string GetOwnerId(TypeIdentity identity, TypeSystemQueryContext? context)
    {
        if (context is null) return identity.FullMetadataName;
        long length = 0;
        for (var current = identity; current is not null; current = current.ContainingType)
        {
            context.SpendWork();
            length += current.MetadataName.Length;
            if (current.GenericArity > 0) length += 1 + current.GenericArity.ToString(System.Globalization.CultureInfo.InvariantCulture).Length;
            if (current.ContainingType is not null) length++;
            else if (current.Namespace.Length > 0) length += 1L + current.Namespace.Length;
        }
        // StringBuilder 生成和最终快照各处理一次文本；在任何大文本分配前扣费。
        if (length > 0) context.SpendWork(checked(length * 2));
        return identity.FullMetadataName;
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

    internal static List<TypeIdentity> GetTypeChain(TypeIdentity identity, TypeSystemQueryContext? context = null)
    {
        var identities = new List<TypeIdentity>();
        for (var current = identity;
             current is not null;
             current = current.ContainingType)
        {
            context?.SpendWork();
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

    internal bool Equals(GenericParameterKey other, TypeSystemQueryContext context) =>
        _ownerKind == other._ownerKind && _position == other._position &&
        context.AssembliesEqual(_ownerAssembly, other._ownerAssembly) && context.StringsEqual(_ownerId, other._ownerId);

    internal int GetHashCode(TypeSystemQueryContext context)
    {
        context.SpendWork(Math.Max(1L, (long)_ownerId.Length + _ownerAssembly.Name.Length +
            _ownerAssembly.CultureName.Length + _ownerAssembly.PublicKeyToken.Length));
        return GetHashCode();
    }

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
