using AspectN.Execution;
using AspectN.AdapterInternals;
using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Mono.Cecil;

namespace AspectN.Cecil.Internal;

internal sealed class CecilResolver(CecilMappingOperation mapper, TypeSystemQueryContext query, MappingContext resources)
{

    private readonly CecilMappingOperation _mapper = mapper;
    private readonly TypeSystemQueryContext _query = query;
    private readonly MappingContext _resources = resources;
    private readonly CecilNativeTypeLimitGuard _guard = new(resources);
    private readonly CecilTypeMapper _native = new(resources);

    // 解析继承链时遇到损坏元数据不能直接让匹配崩溃，而是转换为可诊断的 Unknown 结果。
    // + 只匹配声明的继承/实现关系：基类与接口沿名义闭包遍历；
    // 数组协变与泛型变型不属于名义关系，经类型实参/元素位置的 + 组合表达（如 Base+[]、IEnumerable<object+>）。
    public TypeRelationResult IsAssignableTo(
        TypeSignature candidate,
        TypeSignature target,
        CancellationToken cancellationToken)
    {
        _query.ThrowIfExhausted();
        var arrayRelation = ArrayRelations.Compare(candidate, target, CompareUnqualified, _query);
        if (arrayRelation is not null) return arrayRelation;
        var structural = CompareUnqualified(candidate, target);
        if (structural.Status == MatchStatus.Matched) return structural;

        if (!_mapper.TryGetTypeReference(candidate, out var candidateReference) ||
            candidateReference is null)
        {
            return MissingNativeReference(candidate);
        }

        var pending = new Queue<(TypeReference Type, GenericInstanceType? Context)>();
        var visited = new HashSet<(CecilTypeCacheKey Type, CecilTypeCacheKey? Context)>(new KeyComparer(_resources));
        pending.Enqueue((candidateReference, null));
        TypeRelationResult? unresolved = structural.Status == MatchStatus.Unknown ? structural : null;

        while (pending.Count > 0)
        {
            _query.ThrowIfExhausted();
            var (current, constraintContext) = pending.Dequeue();
            try
            {
                _guard.ValidateType(current);
                if (constraintContext is not null) _guard.ValidateType(constraintContext);
                if (!visited.Add((_mapper.GetKey(current), constraintContext is null ? null : _mapper.GetKey(constraintContext)))) continue;
                if (!_query.TryConsumeRelationNodes(1)) _query.ThrowIfExhausted();

                var relation = CompareUnqualified(
                    _mapper.MapType(current),
                    target);
                if (relation.Status == MatchStatus.Matched) return relation;
                if (relation.Status == MatchStatus.Unknown) unresolved ??= relation;

                // 数组、引用与指针的 Resolve() 会退化为元素类型定义，关系必须按包装类型单独判断；
                // 数组的合成关系以签名形式产出并走统一非限定比较（与非限定目标名兼容）。
                if (current is ArrayType array)
                {
                    var arrayComparison = CompareArrayRelations(array, target);
                    if (arrayComparison.Status == MatchStatus.Matched) return arrayComparison;
                    if (arrayComparison.Status == MatchStatus.Unknown) unresolved ??= arrayComparison;
                    continue;
                }

                if (current is ByReferenceType or PointerType)
                {
                    continue;
                }

                if (current is GenericParameter parameter)
                {
                    var constraintFailure = EnqueueConstraints(parameter, pending, constraintContext);
                    unresolved ??= constraintFailure;
                    continue;
                }

                var definition = TryResolve(current, out var failure);
                if (definition is null)
                {
                    // 解析失败只影响该节点的进一步展开：其余队列分支仍可能给出确定匹配。
                    unresolved ??= failure;
                    continue;
                }

                var expansionFailure = EnqueueAncestors(current, definition, pending);
                unresolved ??= expansionFailure;
            }
            catch (Exception exception) when (IsQueryFailure(exception))
            {
                unresolved ??= ResolutionFailure(current, exception);
            }
        }

        // 无确定匹配且存在未解析分支时返回 Unknown，不能静默判为不匹配（规范 12.4）。
        return unresolved ?? TypeRelationResult.NotMatched;
    }

    // 获取声明的继承/实现闭包（不含候选自身）；数组按运行时固定事实产出合成关系。
    // 祖先引用先产出再解析展开（与具体目标路径的顺序一致）——匹配只需要签名，不需要定义；
    // 解析失败时该祖先已产出，但其上层关系不可知，闭包标记为部分并携带失败诊断。
    private void AddAncestor(List<TypeSignature> ancestors, TypeSignature signature, ref TypeRelationResult? failure)
    {
        var diagnostic = MetadataLimitGuard.GetFailure(signature, _query);
        if (diagnostic is not null) failure ??= TypeRelationResult.Unknown(diagnostic);
        else { _query.SpendWork(); ancestors.Add(signature); }
    }

    public InheritanceClosure GetInheritanceClosure(
        TypeSignature candidate,
        CancellationToken cancellationToken)
    {
        _query.ThrowIfExhausted();
        var candidateFailure = MetadataLimitGuard.GetFailure(candidate, _query);
        if (!_mapper.TryGetTypeReference(candidate, out var reference) ||
            reference is null)
        {
            return new InheritanceClosure(
                Array.Empty<TypeSignature>(),
                candidateFailure is null ? MissingNativeReference(candidate) : TypeRelationResult.Unknown(candidateFailure));
        }

        var ancestors = new List<TypeSignature>();
        var pending = new Queue<(TypeReference Type, GenericInstanceType? Context)>();
        var visited = new HashSet<(CecilTypeCacheKey Type, CecilTypeCacheKey? Context)>(new KeyComparer(_resources));
        pending.Enqueue((reference, null));
        TypeRelationResult? unresolved = candidateFailure is null ? null : TypeRelationResult.Unknown(candidateFailure);
        while (pending.Count > 0)
        {
            _query.ThrowIfExhausted();
            var (current, constraintContext) = pending.Dequeue();
            try
            {
                _guard.ValidateType(current);
                if (constraintContext is not null) _guard.ValidateType(constraintContext);
                if (!visited.Add((_mapper.GetKey(current), constraintContext is null ? null : _mapper.GetKey(constraintContext)))) continue;
                if (!_query.TryConsumeRelationNodes(1)) _query.ThrowIfExhausted();

                if (current is ArrayType array)
                {
                    foreach (var ancestor in EnumerateArrayRelations(array)) AddAncestor(ancestors, ancestor, ref unresolved);
                    continue;
                }

                if (current is ByReferenceType or PointerType) continue;

                if (!ReferenceEquals(current, reference))
                {
                    AddAncestor(ancestors, _mapper.MapType(current), ref unresolved);
                }

                if (current is GenericParameter parameter)
                {
                    var constraintFailure = EnqueueConstraints(parameter, pending, constraintContext);
                    unresolved ??= constraintFailure;
                    continue;
                }

                var definition = TryResolve(current, out var failure);
                if (definition is null)
                {
                    // 解析失败只影响该节点的进一步展开：其余队列中的已知分支仍然保留。
                    unresolved ??= failure;
                    continue;
                }

                var expansionFailure = EnqueueAncestors(current, definition, pending);
                unresolved ??= expansionFailure;
            }
            catch (Exception exception) when (IsQueryFailure(exception))
            {
                unresolved ??= ResolutionFailure(current, exception);
            }
        }

        return new InheritanceClosure(ancestors, unresolved);
    }

    private sealed class KeyComparer(MappingContext resources) : IEqualityComparer<(CecilTypeCacheKey Type, CecilTypeCacheKey? Context)>
    {
        public int GetHashCode((CecilTypeCacheKey Type, CecilTypeCacheKey? Context) item) => unchecked(item.Type.GetHashCode() * 397 ^ item.Context.GetHashCode());
        public bool Equals((CecilTypeCacheKey Type, CecilTypeCacheKey? Context) left, (CecilTypeCacheKey Type, CecilTypeCacheKey? Context) right) =>
            left.Type.Equals(right.Type, resources) && left.Context.HasValue == right.Context.HasValue &&
            (!left.Context.HasValue || left.Context.Value.Equals(right.Context!.Value, resources));
    }

    private TypeRelationResult CompareUnqualified(TypeSignature actual, TypeSignature expected) =>
        StructuralTypeComparer.AreEqual(actual, expected, _query, unqualified: true);

    private void Enqueue(Queue<(TypeReference Type, GenericInstanceType? Context)> pending, (TypeReference Type, GenericInstanceType? Context) item)
    {
        if (!_query.TryConsumeRelationEdges(1)) _query.ThrowIfExhausted();
        pending.Enqueue(item);
    }

    private TypeRelationResult? EnqueueAncestors(TypeReference current, TypeDefinition definition,
        Queue<(TypeReference Type, GenericInstanceType? Context)> pending)
    {
        try
        {
            var constructed = current as GenericInstanceType;
            if (definition.BaseType is not null)
                Enqueue(pending, (Substitute(definition.BaseType, constructed), null));
            foreach (var implementation in definition.Interfaces)
                Enqueue(pending, (Substitute(implementation.InterfaceType, constructed), null));
            return null;
        }
        catch (Exception exception) when (CecilMetadataReader.IsRecoverable(exception))
        {
            // 延迟读取可能在 Resolve 成功后失败；保留已入队的基类和其他分支。
            return ResolutionFailure(current, exception);
        }
    }

    private TypeRelationResult? EnqueueConstraints(GenericParameter parameter, Queue<(TypeReference Type, GenericInstanceType? Context)> pending, GenericInstanceType? constraintContext)
    {
        // 泛型形参不能 Resolve 为类型定义；约束及隐含基类本身构成可遍历的名义关系。
        var module = parameter.Module ?? _mapper.Module;
        Enqueue(pending, (module.TypeSystem.Object, null));
        var valueTypeQueued = parameter.HasNotNullableValueTypeConstraint;
        if (valueTypeQueued) Enqueue(pending, (SyntheticReference(module, "System", "ValueType"), null));
        try
        {
            // 游离引用的声明不可解析，但已经知道的隐含基类仍应保留在队列中。
            if (parameter.Module is null &&
                parameter.Owner is MethodReference { IsDefinition: false } or TypeReference { IsDefinition: false })
                return ResolutionFailure(parameter, null);
            // 约束链中的中间方法形参仍需沿用最初封闭声明类型的替换上下文。
            var constructed = constraintContext ?? (parameter.Owner is MethodReference methodOwner
                ? methodOwner.DeclaringType as GenericInstanceType
                : parameter.Owner as GenericInstanceType);
            var declaration = parameter.Owner switch
            {
                MethodReference method when !method.IsDefinition => _native.ResolveMethodDeclaration(method)?.GenericParameters,
                TypeReference type when !type.IsDefinition => type.Resolve()?.GenericParameters,
                _ => null
            };
            if (parameter.Owner is MethodReference { IsDefinition: false } or TypeReference { IsDefinition: false })
            {
                if (declaration is null || parameter.Position >= declaration.Count)
                    return ResolutionFailure(parameter, null);
                parameter = declaration[parameter.Position];
            }
            if (!valueTypeQueued && parameter.HasNotNullableValueTypeConstraint)
                Enqueue(pending, (SyntheticReference(parameter.Module ?? module, "System", "ValueType"), null));
            foreach (var constraint in parameter.Constraints)
                Enqueue(pending, (Substitute(constraint.ConstraintType, constructed), constructed));
            return null;
        }
        catch (Exception exception) when (CecilMetadataReader.IsRecoverable(exception))
        {
            return ResolutionFailure(parameter, exception);
        }
    }

    private TypeRelationResult CompareArrayRelations(
        ArrayType candidate,
        TypeSignature target)
    {
        TypeRelationResult? unknown = null;
        foreach (var relation in EnumerateArrayRelations(candidate))
        {
            var comparison = CompareUnqualified(relation, target);
            if (comparison.Status == MatchStatus.Matched) return comparison;
            if (comparison.Status == MatchStatus.Unknown) unknown ??= comparison;
        }

        return unknown ?? TypeRelationResult.NotMatched;
    }

    // 数组自身的关系由运行时固定提供：System.Array/Object、非泛型集合接口（任意秩），
    // 以及 SZ 向量数组的五个泛型集合接口（实参与元素精确一致）；多维数组不实现泛型集合接口。
    private IEnumerable<TypeSignature> EnumerateArrayRelations(ArrayType array)
    {
        var module = array.Module ?? _mapper.Module;
        yield return MapSyntheticRelation(module, "System", "Array");
        yield return MapSyntheticRelation(module, "System", "Object");
        foreach (var (namespaceName, name) in NonGenericCollectionInterfaces())
            yield return MapSyntheticRelation(module, namespaceName, name);
        if (!array.IsVector) yield break;
        foreach (var name in GenericCollectionInterfaceNames())
            yield return MapSyntheticRelation(module, "System.Collections.Generic", name + ((char)96) + "1", array.ElementType);
    }

    private TypeSignature MapSyntheticRelation(ModuleDefinition module, string namespaceName, string name, TypeReference? argument = null)
    {
        if (!_query.TryConsumeRelationEdges(1)) _query.ThrowIfExhausted();
        if (!_query.TryConsumeRelationNodes(1)) _query.ThrowIfExhausted();
        var definition = SyntheticReference(module, namespaceName, name);
        if (argument is null) return _mapper.MapType(definition);
        var instance = new GenericInstanceType(definition);
        instance.GenericArguments.Add(argument);
        return _mapper.MapType(instance);
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

    private TypeReference Substitute(TypeReference typeReference, GenericInstanceType? constructed) => WorkStack.Run(SubstituteWork(typeReference, constructed), _resources.CancellationToken);

    private async StackWork<TypeReference> SubstituteWork(TypeReference typeReference, GenericInstanceType? constructed)
    {
        _resources.SpendWork();
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
                        await SubstituteWork(generic.ElementType, constructed));
                    foreach (var argument in generic.GenericArguments)
                    {
                        substituted.GenericArguments.Add(
                            await SubstituteWork(argument, constructed));
                    }

                    return substituted;
                }
            case ArrayType array:
                var substitutedElement = await SubstituteWork(
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
                    await SubstituteWork(byReference.ElementType, constructed));
            case PointerType pointer:
                return new PointerType(
                    await SubstituteWork(pointer.ElementType, constructed));
            case OptionalModifierType optional:
                return new OptionalModifierType(
                    await SubstituteWork(optional.ModifierType, constructed),
                    await SubstituteWork(optional.ElementType, constructed));
            case RequiredModifierType required:
                return new RequiredModifierType(
                    await SubstituteWork(required.ModifierType, constructed),
                    await SubstituteWork(required.ElementType, constructed));
            case PinnedType pinned:
                return new PinnedType(
                    await SubstituteWork(pinned.ElementType, constructed));
            case SentinelType sentinel:
                return new SentinelType(
                    await SubstituteWork(sentinel.ElementType, constructed));
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
        if (exception is AspectNLimitException limit) return TypeRelationResult.Unknown(limit.Diagnostic);
        var detail = exception is null
            ? "The resolver returned no definition."
            : $"{exception.GetType().Name}: {exception.Message}";
        return TypeRelationResult.Unknown(
            new AspectNDiagnostic(
                DiagnosticCodes.ResolutionFailure,
                DiagnosticSeverity.Warning,
                $"Could not read Cecil {typeReference.GetType().Name} metadata. {detail}"));
    }

    private static bool IsQueryFailure(Exception exception) =>
        exception is AspectNLimitException || CecilMetadataReader.IsRecoverable(exception);
}
