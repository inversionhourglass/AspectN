using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Mono.Cecil;

namespace AspectN.Cecil.Internal;

internal sealed class CecilResolver(CecilSignatureMapper mapper)
{
    private const string RESOLUTION_FAILURE_CODE = "AN3002";
    private const string GENERIC_COLLECTIONS_NAMESPACE =
        "System.Collections.Generic";

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

            // 数组、引用与指针的 Resolve() 会退化为元素类型定义，关系必须按包装类型单独判断。
            if (current is ArrayType)
            {
                return EvaluateArrayAssignment(current, target);
            }

            if (current is ByReferenceType or PointerType)
            {
                return TypeRelationResult.NotMatched;
            }

            var definition = TryResolve(current, out var failure);
            if (definition is null)
            {
                return failure!;
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

        return TypeRelationResult.NotMatched;
    }

    // 数组没有名义上的数组基类型（唯一基类是 System.Array）；到达此分支时目标数组与候选元素不同，
    // 恒等已在入口处理，数组到数组不存在名义关系。数组实现的关系只有运行时固定提供的根类型与接口。
    private TypeRelationResult EvaluateArrayAssignment(
        TypeReference candidate,
        TypeSignature target)
    {
        if (IsArrayRootTarget(target) || IsArrayOwnedInterfaceTarget(target))
        {
            return TypeRelationResult.Matched;
        }

        if (candidate is ArrayType { IsVector: true } vector &&
            target is NamedTypeSignature targetNamed &&
            TryGetSzGenericCollectionTarget(targetNamed, out var elementTarget))
        {
            // SZ 向量数组实现的泛型集合接口由运行时合成（元数据中不存在）；
            // 合成实现的实参与数组元素完全一致，变型实例化不属于名义关系。
            return UnqualifiedTypeComparer.AreEqual(
                _mapper.MapType(vector.ElementType),
                elementTarget);
        }

        return TypeRelationResult.NotMatched;
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

    private static bool IsArrayRootTarget(TypeSignature target)
    {
        return target is NamedTypeSignature named &&
               named.TypeArguments.Count == 0 &&
               named.Identity.ContainingType is null &&
               named.Identity.GenericArity == 0 &&
               named.Identity.Namespace == "System" &&
               named.Identity.MetadataName is "Array" or "Object";
    }

    // 任意秩的数组都实现这些非泛型接口（System.Array 的继承链之外由运行时直接提供）。
    private static bool IsArrayOwnedInterfaceTarget(TypeSignature target)
    {
        if (target is not NamedTypeSignature named ||
            named.TypeArguments.Count != 0 ||
            named.Identity.ContainingType is not null ||
            named.Identity.GenericArity != 0)
        {
            return false;
        }

        var @namespace = named.Identity.Namespace;
        var name = named.Identity.MetadataName;
        return (@namespace == "System.Collections" &&
                name is "IEnumerable" or "ICollection" or "IList" or
                    "IStructuralComparable" or "IStructuralEquatable") ||
               (@namespace == "System" && name == "ICloneable");
    }

    // 一维零下界（SZ）向量数组实现的泛型集合接口；多维数组不实现任何泛型集合接口。
    private static bool TryGetSzGenericCollectionTarget(
        NamedTypeSignature target,
        out TypeSignature elementTarget)
    {
        if (target.TypeArguments.Count == 1 &&
            target.Identity.ContainingType is null &&
            target.Identity.Namespace == GENERIC_COLLECTIONS_NAMESPACE &&
            target.Identity.MetadataName is "IEnumerable" or "ICollection" or
                "IList" or "IReadOnlyList" or "IReadOnlyCollection")
        {
            elementTarget = target.TypeArguments[0];
            return true;
        }

        elementTarget = null!;
        return false;
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
        catch (AssemblyResolutionException exception)
        {
            failure = ResolutionFailure(reference, exception);
        }
        catch (InvalidOperationException exception)
        {
            failure = ResolutionFailure(reference, exception);
        }
        catch (IOException exception)
        {
            failure = ResolutionFailure(reference, exception);
        }
        catch (BadImageFormatException exception)
        {
            failure = ResolutionFailure(reference, exception);
        }
        catch (NotSupportedException exception)
        {
            failure = ResolutionFailure(reference, exception);
        }
        catch (ArgumentException exception)
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
                RESOLUTION_FAILURE_CODE,
                DiagnosticSeverity.Warning,
                $"Could not resolve '{typeReference.FullName}'. {detail}"));
    }
}
