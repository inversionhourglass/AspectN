using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Mono.Cecil;

namespace AspectN.Cecil.Internal;

internal sealed class CecilResolver
{
    private const string RESOLUTION_FAILURE_CODE = "AN3002";

    private readonly CecilSignatureMapper _mapper;

    public CecilResolver(CecilSignatureMapper mapper)
    {
        _mapper = mapper;
    }

    public TypeRelationResult IsAssignableTo(
        TypeSignature candidate,
        TypeSignature target,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var structural = StructuralTypeComparer.AreEqual(candidate, target);
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

            var relation = StructuralTypeComparer.AreEqual(
                _mapper.MapType(current),
                target);
            if (relation.Status == MatchStatus.Matched) return relation;
            if (relation.Status == MatchStatus.Unknown) return relation;

            TypeDefinition? definition;
            try
            {
                definition = current as TypeDefinition ?? current.Resolve();
            }
            catch (AssemblyResolutionException exception)
            {
                return ResolutionFailure(current, exception);
            }
            catch (InvalidOperationException exception)
            {
                return ResolutionFailure(current, exception);
            }
            catch (IOException exception)
            {
                return ResolutionFailure(current, exception);
            }
            catch (BadImageFormatException exception)
            {
                return ResolutionFailure(current, exception);
            }
            catch (NotSupportedException exception)
            {
                return ResolutionFailure(current, exception);
            }
            catch (ArgumentException exception)
            {
                return ResolutionFailure(current, exception);
            }

            if (definition is null)
            {
                return ResolutionFailure(current, exception: null);
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
