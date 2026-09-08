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
    public TypeRelationResult IsAssignableTo(
        TypeSignature candidate,
        TypeSignature target,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var structural = UnqualifiedTypeComparer.AreEqual(candidate, target);
        if (structural.Status != MatchStatus.NotMatched) return structural;

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

            var variance = TryEvaluateVarianceAssignment(
                current,
                target,
                cancellationToken);
            if (variance is not null) return variance;

            // 数组、引用与指针的 Resolve() 会退化为元素类型定义，继承链必须按包装类型单独判断。
            if (current is ArrayType array)
            {
                return EvaluateArrayAssignment(
                    array,
                    target,
                    cancellationToken);
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

    // 数组可赋值给同秩且元素引用协变的数组（多维数组支持协变；一维时仅“非向量→向量”被拒）、
    // System.Array、System.Object 以及数组自身实现的接口；泛型集合接口仅对 SZ 向量数组成立。
    private TypeRelationResult EvaluateArrayAssignment(
        ArrayType candidate,
        TypeSignature target,
        CancellationToken cancellationToken)
    {
        if (target is ArrayTypeSignature targetArray)
        {
            if (targetArray.Rank != candidate.Rank ||
                (!candidate.IsVector && IsVectorTarget(target)))
            {
                return TypeRelationResult.NotMatched;
            }

            if (!IsCovariantElement(candidate.ElementType, out var failure))
            {
                return failure ?? TypeRelationResult.NotMatched;
            }

            return IsAssignableTo(
                _mapper.MapType(candidate.ElementType),
                targetArray.ElementType,
                cancellationToken);
        }

        if (IsArrayRootTarget(target) || IsArrayOwnedInterfaceTarget(target))
        {
            return TypeRelationResult.Matched;
        }

        if (candidate.IsVector &&
            target is NamedTypeSignature targetNamed &&
            TryGetSzGenericCollectionTarget(targetNamed, out var elementTarget))
        {
            return EvaluateArrayInterfaceAssignment(
                candidate,
                elementTarget,
                cancellationToken);
        }

        return TypeRelationResult.NotMatched;
    }

    // 判定数组目标是否为向量：模式构造的秩 1 目标无法表达下界，按向量处理；
    // 非向量候选不能赋给向量数组，向量候选可以赋给非向量一维数组。
    private bool IsVectorTarget(TypeSignature target)
    {
        return !_mapper.TryGetTypeReference(target, out var reference) ||
               reference is not ArrayType array ||
               array.IsVector;
    }

    // 泛型集合接口实参兼容：元素相等，或引用协变后元素可赋值（IList<object>←string[] 经 object[] 成立）。
    private TypeRelationResult EvaluateArrayInterfaceAssignment(
        ArrayType candidate,
        TypeSignature targetElement,
        CancellationToken cancellationToken)
    {
        var elementCandidate = _mapper.MapType(candidate.ElementType);
        var structural = UnqualifiedTypeComparer.AreEqual(
            elementCandidate,
            targetElement);
        if (structural.Status != MatchStatus.NotMatched) return structural;

        if (!IsCovariantElement(candidate.ElementType, out var failure))
        {
            return failure ?? TypeRelationResult.NotMatched;
        }

        return IsAssignableTo(elementCandidate, targetElement, cancellationToken);
    }

    // 数组协变仅对引用类型元素成立；数组与泛型参数元素按引用处理，其余元素解析后判断。
    private static bool IsCovariantElement(
        TypeReference element,
        out TypeRelationResult? failure)
    {
        if (element is ArrayType or GenericParameter)
        {
            failure = null;
            return true;
        }

        var definition = TryResolve(element, out failure);
        return definition is not null && !definition.IsValueType;
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

    // 一维零下界（SZ）数组实现的泛型集合接口；多维数组不实现任何泛型集合接口。
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

    // 同一泛型定义的两个构造按形参的协变、逆变或不变性判断实参兼容性；
    // 变型只能通过引用转换成立，装箱与拆箱不算。
    private TypeRelationResult? TryEvaluateVarianceAssignment(
        TypeReference current,
        TypeSignature target,
        CancellationToken cancellationToken)
    {
        if (current is not GenericInstanceType instance ||
            target is not NamedTypeSignature targetNamed ||
            targetNamed.TypeArguments.Count == 0 ||
            _mapper.MapType(current) is not NamedTypeSignature candidateNamed ||
            candidateNamed.TypeArguments.Count !=
                targetNamed.TypeArguments.Count ||
            !IsSameGenericDefinition(
                candidateNamed.Identity,
                targetNamed.Identity))
        {
            return null;
        }

        var definition = TryResolve(instance.ElementType, out var failure);
        if (definition is null)
        {
            return failure;
        }

        if (definition.GenericParameters.Count !=
            candidateNamed.TypeArguments.Count)
        {
            return null;
        }

        List<AspectNDiagnostic>? unknownDiagnostics = null;
        for (var index = 0;
             index < candidateNamed.TypeArguments.Count;
             index++)
        {
            var parameter = definition.GenericParameters[index];
            if (parameter.IsCovariant || parameter.IsContravariant)
            {
                var relation = EvaluateVarianceArgument(
                    instance.GenericArguments[index],
                    candidateNamed.TypeArguments[index],
                    targetNamed.TypeArguments[index],
                    parameter.IsCovariant,
                    cancellationToken);
                if (relation.Status == MatchStatus.NotMatched)
                {
                    return relation;
                }

                if (relation.Status == MatchStatus.Unknown)
                {
                    unknownDiagnostics ??= [];
                    unknownDiagnostics.AddRange(relation.Diagnostics);
                }

                continue;
            }

            var invariant = UnqualifiedTypeComparer.AreEqual(
                candidateNamed.TypeArguments[index],
                targetNamed.TypeArguments[index]);
            if (invariant.Status == MatchStatus.NotMatched)
            {
                return invariant;
            }

            if (invariant.Status == MatchStatus.Unknown)
            {
                unknownDiagnostics ??= [];
                unknownDiagnostics.AddRange(invariant.Diagnostics);
            }
        }

        return unknownDiagnostics is null
            ? TypeRelationResult.Matched
            : TypeRelationResult.Unknown(unknownDiagnostics.ToArray());
    }

    // 变型实参兼容：实参相等，或两端均为引用类型且按方向可赋值。
    private TypeRelationResult EvaluateVarianceArgument(
        TypeReference candidateReference,
        TypeSignature candidateArgument,
        TypeSignature targetArgument,
        bool isCovariant,
        CancellationToken cancellationToken)
    {
        var structural = UnqualifiedTypeComparer.AreEqual(
            candidateArgument,
            targetArgument);
        if (structural.Status != MatchStatus.NotMatched) return structural;

        if (!IsCovariantElement(candidateReference, out var failure) ||
            IsValueKind(targetArgument))
        {
            return failure ?? TypeRelationResult.NotMatched;
        }

        return isCovariant
            ? IsAssignableTo(
                candidateArgument,
                targetArgument,
                cancellationToken)
            : IsAssignableTo(
                targetArgument,
                candidateArgument,
                cancellationToken);
    }

    private static bool IsValueKind(TypeSignature signature)
    {
        return signature is NamedTypeSignature named &&
               named.Identity.Kind is TypeKind.Struct or TypeKind.Enum;
    }

    private static bool IsSameGenericDefinition(
        TypeIdentity candidate,
        TypeIdentity target)
    {
        return candidate.GenericArity == target.GenericArity &&
               StringComparer.Ordinal.Equals(
                   candidate.MetadataName,
                   target.MetadataName) &&
               (string.IsNullOrEmpty(target.Namespace) ||
                StringComparer.Ordinal.Equals(
                    candidate.Namespace,
                    target.Namespace)) &&
               ((candidate.ContainingType is null &&
                 target.ContainingType is null) ||
                (candidate.ContainingType is not null &&
                 target.ContainingType is not null &&
                 IsSameGenericDefinition(
                     candidate.ContainingType,
                     target.ContainingType)));
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
