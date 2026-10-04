using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Mono.Cecil;

namespace AspectN.Cecil.Internal;

internal sealed class CecilResolver(CecilSignatureMapper mapper)
{

    private readonly CecilSignatureMapper _mapper = mapper;

    // 解析继承链时遇到损坏元数据不能直接让匹配崩溃，而是转换为可诊断的 Unknown 结果。
    // + 只匹配声明的继承/实现关系：基类与接口沿名义闭包遍历；
    // 数组协变与泛型变型不属于名义关系，经类型实参/元素位置的 + 组合表达（如 Base+[]、IEnumerable<object+>）。
    public TypeRelationResult IsAssignableTo(
        TypeSignature candidate,
        TypeSignature target,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var structural = UnqualifiedTypeComparer.AreEqual(candidate, target);
        if (structural.Status != MatchStatus.NotMatched)
        {
            // 结构相等不足以证明可赋值：同元素的一维非向量数组不能赋给向量数组。
            return IsArrayIdentityAdmissible(candidate, target)
                ? structural
                : TypeRelationResult.NotMatched;
        }

        if (!_mapper.TryGetTypeReference(candidate, out var candidateReference) ||
            candidateReference is null)
        {
            return MissingNativeReference(candidate);
        }

        var pending = new Queue<TypeReference>();
        var visited = new HashSet<CecilTypeCacheKey>();
        pending.Enqueue(candidateReference);
        TypeRelationResult? unresolved = null;

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Dequeue();
            if (!visited.Add(CecilTypeCacheKey.Create(current))) continue;

            var relation = UnqualifiedTypeComparer.AreEqual(
                _mapper.MapType(current),
                target);
            if (relation.Status == MatchStatus.Matched) return relation;
            if (relation.Status == MatchStatus.Unknown) return relation;

            // 数组、引用与指针的 Resolve() 会退化为元素类型定义，关系必须按包装类型单独判断；
            // 数组的合成关系以签名形式产出并走统一非限定比较（与非限定目标名兼容）。
            if (current is ArrayType array)
            {
                return CompareArrayRelations(array, target);
            }

            if (current is ByReferenceType or PointerType)
            {
                return TypeRelationResult.NotMatched;
            }

            var definition = TryResolve(current, out var failure);
            if (definition is null)
            {
                // 解析失败只影响该节点的进一步展开：其余队列分支仍可能给出确定匹配。
                unresolved ??= failure;
                continue;
            }

            var constructed = current as GenericInstanceType;
            if (definition.BaseType is not null)
            {
                pending.Enqueue(Substitute(definition.BaseType, constructed));
            }

            foreach (var implementation in definition.Interfaces)
            {
                pending.Enqueue(
                    Substitute(implementation.InterfaceType, constructed));
            }
        }

        // 无确定匹配且存在未解析分支时返回 Unknown，不能静默判为不匹配（规范 12.4）。
        return unresolved ?? TypeRelationResult.NotMatched;
    }

    // 获取声明的继承/实现闭包（不含候选自身）；数组按运行时固定事实产出合成关系。
    // 祖先引用先产出再解析展开（与具体目标路径的顺序一致）——匹配只需要签名，不需要定义；
    // 解析失败时该祖先已产出，但其上层关系不可知，闭包标记为部分并携带失败诊断。
    public InheritanceClosure GetInheritanceClosure(
        TypeSignature candidate,
        CancellationToken cancellationToken)
    {
        if (!_mapper.TryGetTypeReference(candidate, out var reference) ||
            reference is null)
        {
            return new InheritanceClosure(
                Array.Empty<TypeSignature>(),
                MissingNativeReference(candidate));
        }

        var ancestors = new List<TypeSignature>();
        var pending = new Queue<TypeReference>();
        var visited = new HashSet<CecilTypeCacheKey>();
        pending.Enqueue(reference);
        TypeRelationResult? unresolved = null;
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Dequeue();
            if (!visited.Add(CecilTypeCacheKey.Create(current))) continue;

            if (current is ArrayType array)
            {
                ancestors.AddRange(EnumerateArrayRelations(array));
                continue;
            }

            if (current is ByReferenceType or PointerType) continue;

            if (!ReferenceEquals(current, reference))
            {
                ancestors.Add(_mapper.MapType(current));
            }

            var definition = TryResolve(current, out var failure);
            if (definition is null)
            {
                // 解析失败只影响该节点的进一步展开：其余队列中的已知分支仍然保留。
                unresolved ??= failure;
                continue;
            }

            var constructed = current as GenericInstanceType;
            if (definition.BaseType is not null)
            {
                pending.Enqueue(Substitute(definition.BaseType, constructed));
            }

            foreach (var implementation in definition.Interfaces)
            {
                pending.Enqueue(
                    Substitute(implementation.InterfaceType, constructed));
            }
        }

        return new InheritanceClosure(ancestors, unresolved);
    }

    private TypeRelationResult CompareArrayRelations(
        ArrayType candidate,
        TypeSignature target)
    {
        foreach (var relation in EnumerateArrayRelations(candidate))
        {
            var comparison = UnqualifiedTypeComparer.AreEqual(relation, target);
            if (comparison.Status != MatchStatus.NotMatched) return comparison;
        }

        return TypeRelationResult.NotMatched;
    }

    // 数组自身的关系由运行时固定提供：System.Array/Object、非泛型集合接口（任意秩），
    // 以及 SZ 向量数组的五个泛型集合接口（实参与元素精确一致）；多维数组不实现泛型集合接口。
    private IEnumerable<TypeSignature> EnumerateArrayRelations(ArrayType array)
    {
        var module = array.Module;
        yield return _mapper.MapType(
            SyntheticReference(module, "System", "Array"));
        yield return _mapper.MapType(
            SyntheticReference(module, "System", "Object"));
        foreach (var (namespaceName, name) in NonGenericCollectionInterfaces())
        {
            yield return _mapper.MapType(
                SyntheticReference(module, namespaceName, name));
        }

        if (!array.IsVector) yield break;
        foreach (var name in GenericCollectionInterfaceNames())
        {
            var definition = SyntheticReference(
                module,
                "System.Collections.Generic",
                name + "`1");
            var instance = new GenericInstanceType(definition);
            instance.GenericArguments.Add(array.ElementType);
            yield return _mapper.MapType(instance);
        }
    }

    private static TypeReference SyntheticReference(
        ModuleDefinition module,
        string @namespace,
        string name)
    {
        return new TypeReference(
            @namespace,
            name,
            module,
            module.TypeSystem.CoreLibrary);
    }

    private static IEnumerable<(string Namespace, string Name)>
        NonGenericCollectionInterfaces()
    {
        yield return ("System.Collections", "IEnumerable");
        yield return ("System.Collections", "ICollection");
        yield return ("System.Collections", "IList");
        yield return ("System", "ICloneable");
        yield return ("System.Collections", "IStructuralComparable");
        yield return ("System.Collections", "IStructuralEquatable");
    }

    private static IEnumerable<string> GenericCollectionInterfaceNames()
    {
        yield return "IEnumerable";
        yield return "ICollection";
        yield return "IList";
        yield return "IReadOnlyList";
        yield return "IReadOnlyCollection";
    }

    // 同元素数组的恒等还要求形状方向兼容：非向量（非零下界）一维数组不能赋给向量数组。
    private bool IsArrayIdentityAdmissible(
        TypeSignature candidate,
        TypeSignature target)
    {
        if (candidate is not ArrayTypeSignature ||
            target is not ArrayTypeSignature ||
            !_mapper.TryGetTypeReference(candidate, out var reference) ||
            reference is not ArrayType array)
        {
            return true;
        }

        return array.IsVector || !IsVectorTarget(target);
    }

    // 判定数组目标是否为向量：模式构造的秩 1 目标无法表达下界，按向量处理。
    private bool IsVectorTarget(TypeSignature target)
    {
        return !_mapper.TryGetTypeReference(target, out var reference) ||
               reference is not ArrayType array ||
               array.IsVector;
    }

    private static TypeDefinition? TryResolve(
        TypeReference reference,
        out TypeRelationResult? failure)
    {
        try
        {
            var definition = reference.Resolve();
            failure = definition is null
                ? ResolutionFailure(reference, exception: null)
                : null;
            return definition;
        }
        catch (Exception exception) when (CecilMetadataReader.IsRecoverable(exception))
        {
            failure = ResolutionFailure(reference, exception);
        }

        return null;
    }

    private static TypeReference Substitute(
        TypeReference typeReference,
        GenericInstanceType? constructed)
    {
        if (constructed is null) return typeReference;

        switch (typeReference)
        {
            case GenericParameter parameter
                when parameter.Type == GenericParameterType.Type &&
                     parameter.Position >= 0 &&
                     parameter.Position < constructed.GenericArguments.Count:
                return constructed.GenericArguments[parameter.Position];
            case GenericInstanceType generic:
                {
                    var substituted = new GenericInstanceType(
                        Substitute(generic.ElementType, constructed));
                    foreach (var argument in generic.GenericArguments)
                    {
                        substituted.GenericArguments.Add(
                            Substitute(argument, constructed));
                    }

                    return substituted;
                }
            case ArrayType array:
                var substitutedElement = Substitute(
                    array.ElementType,
                    constructed);
                if (ReferenceEquals(substitutedElement, array.ElementType))
                {
                    return array;
                }

                var substitutedArray = new ArrayType(
                    substitutedElement,
                    array.Rank);
                for (var index = 0;
                     index < array.Dimensions.Count;
                     index++)
                {
                    substitutedArray.Dimensions[index] = array.Dimensions[index];
                }

                return substitutedArray;
            case ByReferenceType byReference:
                return new ByReferenceType(
                    Substitute(byReference.ElementType, constructed));
            case PointerType pointer:
                return new PointerType(
                    Substitute(pointer.ElementType, constructed));
            case OptionalModifierType optional:
                return new OptionalModifierType(
                    Substitute(optional.ModifierType, constructed),
                    Substitute(optional.ElementType, constructed));
            case RequiredModifierType required:
                return new RequiredModifierType(
                    Substitute(required.ModifierType, constructed),
                    Substitute(required.ElementType, constructed));
            case PinnedType pinned:
                return new PinnedType(
                    Substitute(pinned.ElementType, constructed));
            case SentinelType sentinel:
                return new SentinelType(
                    Substitute(sentinel.ElementType, constructed));
            default:
                return typeReference;
        }
    }

    private static TypeRelationResult MissingNativeReference(
        TypeSignature candidate)
    {
        return TypeRelationResult.Unknown(
            new AspectNDiagnostic(
                DiagnosticCodes.MissingBackendCapability,
                DiagnosticSeverity.Warning,
                $"No Cecil reference is available for '{candidate.GetType().Name}'."));
    }

    private static TypeRelationResult ResolutionFailure(
        TypeReference typeReference,
        Exception? exception)
    {
        var detail = exception is null
            ? "The resolver returned no definition."
            : $"{exception.GetType().Name}: {exception.Message}";
        return TypeRelationResult.Unknown(
            new AspectNDiagnostic(
                DiagnosticCodes.ResolutionFailure,
                DiagnosticSeverity.Warning,
                $"Could not resolve '{typeReference.FullName}'. {detail}"));
    }
}
