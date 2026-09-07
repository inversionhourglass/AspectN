using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Mono.Cecil;

namespace AspectN.Cecil.Internal;

internal sealed class CecilResolver(CecilSignatureMapper mapper)
{
    private const string RESOLUTION_FAILURE_CODE = "AN3002";

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

    // 数组只能赋值给同秩数组（引用协变）、System.Array 与 System.Object，不能继承元素类型的关系。
    private TypeRelationResult EvaluateArrayAssignment(
        ArrayType candidate,
        TypeSignature target,
        CancellationToken cancellationToken)
    {
        if (target is ArrayTypeSignature targetArray)
        {
            if (targetArray.Rank != candidate.Rank)
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

        return IsArrayRootTarget(target)
            ? TypeRelationResult.Matched
            : TypeRelationResult.NotMatched;
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
                return new ArrayType(
                    Substitute(array.ElementType, constructed),
                    array.Rank);
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
