using AspectN.Execution;
using AspectN.Binding;
using AspectN.Diagnostics;
using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN.Evaluation;

internal static class TypePatternEvaluator
{
    public static MatchStatus Evaluate(BoundTypePattern pattern, TypeSignature candidate, EvaluationContext context) =>
        WorkStack.Run(EvaluateWork(pattern, candidate, context), context.CancellationToken);

    private static readonly HashSet<string> _KnownValueTypeNames =
        new(StringComparer.Ordinal)
        {
            "Boolean", "Byte", "SByte",
            "Int16", "UInt16", "Int32", "UInt32", "Int64", "UInt64",
            "Char", "Single", "Double", "Decimal",
        };

    private static readonly HashSet<string> _KnownReferenceTypeNames =
        new(StringComparer.Ordinal)
        {
            "String", "Object",
        };

    // Declaration 只组合固定引用的三态；Capture 隔离分支绑定并传播未知约束。
    internal static async StackWork<MatchStatus> EvaluateWork(
        BoundTypePattern pattern,
        TypeSignature candidate,
        EvaluationContext context)
    {
        context.Visit(pattern.Span);
        switch (pattern)
        {
            case BoundWildcardTypePattern:
                return MatchStatus.Matched;
            case BoundVariableTypePattern variable:
                return context.Bind(variable, candidate);
            case BoundNamedTypePattern named:
                return await EvaluateNamedWork(named, candidate, context);
            case BoundArrayTypePattern array:
                return await EvaluateArrayWork(array, candidate, context);
            case BoundNullableTypePattern nullable:
                return await EvaluateNullableWork(nullable, candidate, context);
            case BoundTupleTypePattern tuple:
                return await EvaluateTupleWork(tuple, candidate, context);
            case BoundTaskLikeTypePattern taskLike:
                return await EvaluateTaskLikeWork(taskLike, candidate, context);
            case BoundAssignableTypePattern assignable:
                return await EvaluateAssignableWork(assignable, candidate, context);
            case BoundUnaryTypePattern unary:
                return await context.EvaluateNegationWork(
                    branch => EvaluateWork(unary.Operand, candidate, branch));
            case BoundBinaryTypePattern binary
                when binary.Operator == BoundTypeLogicalOperator.And:
                {
                    var left = await EvaluateWork(binary.Left, candidate, context);
                    var right = await EvaluateWork(binary.Right, candidate, context);
                    return MatchLogic.And(left, right);
                }
            case BoundBinaryTypePattern binary:
                {
                    var leftContext = context.Fork();
                    var rightContext = context.Fork();
                    var left = await EvaluateWork(binary.Left, candidate, leftContext);
                    var right = await EvaluateWork(binary.Right, candidate, rightContext);
                    context.MergeDiagnosticsFrom(leftContext);
                    context.MergeDiagnosticsFrom(rightContext);
                    var alternatives = context.CreateAlternativeCollector();
                    alternatives?.Add(leftContext, left);
                    alternatives?.Add(rightContext, right);
                    alternatives?.Apply();

                    return MatchLogic.Or(left, right);
                }
            default:
                return Unknown(
                    pattern,
                    context,
                    "This type-pattern node requires a later semantic evaluator.");
        }
    }

    private static async StackWork<MatchStatus> EvaluateNamedWork(
        BoundNamedTypePattern pattern,
        TypeSignature candidate,
        EvaluationContext context)
    {
        if (candidate is UnsupportedTypeSignature unsupported)
        {
            context.QueryContext.SpendWork(Math.Max(1, unsupported.DisplayName.Length));
            return Unknown(
                pattern,
                context,
                $"Structural identity is unavailable for type '{unsupported.DisplayName}'.");
        }

        if (candidate is not NamedTypeSignature named)
        {
            return MatchStatus.NotMatched;
        }

        return await EvaluateNamedCandidateWork(pattern, named, context);
    }

    private static async StackWork<MatchStatus> EvaluateArrayWork(
        BoundArrayTypePattern pattern,
        TypeSignature candidate,
        EvaluationContext context)
    {
        if (candidate is UnsupportedTypeSignature unsupported)
        {
            context.QueryContext.SpendWork(Math.Max(1, unsupported.DisplayName.Length));
            return Unknown(
                pattern,
                context,
                $"Array shape is unavailable for type '{unsupported.DisplayName}'.");
        }

        return candidate is ArrayTypeSignature array && array.Rank == pattern.Rank
            ? await EvaluateWork(pattern.ElementType, array.ElementType, context)
            : MatchStatus.NotMatched;
    }

    private static async StackWork<MatchStatus> EvaluateNullableWork(
        BoundNullableTypePattern pattern,
        TypeSignature candidate,
        EvaluationContext context)
    {
        if (candidate is UnsupportedTypeSignature unsupported)
        {
            context.QueryContext.SpendWork(Math.Max(1, unsupported.DisplayName.Length));
            return Unknown(
                pattern,
                context,
                $"Nullability is unavailable for type '{unsupported.DisplayName}'.");
        }

        if (candidate is ArrayTypeSignature)
        {
            return await EvaluateWork(pattern.UnderlyingType, candidate, context);
        }

        if (candidate is not NamedTypeSignature named)
        {
            return Unknown(
                pattern,
                context,
                "The candidate's value/reference type category is unavailable.");
        }

        if (IsNamedDefinition(named.Identity, "System", "Nullable", 1))
        {
            var arguments = GenericParameterNamer.GetEffectiveTypeArguments(named, context.QueryContext);
            if (arguments.Length != 1)
            {
                return Unknown(
                    pattern,
                    context,
                    "System.Nullable<T> is missing its constructed argument.");
            }

            return await EvaluateWork(
                pattern.UnderlyingType,
                arguments[0],
                context);
        }

        return named.Identity.Kind switch
        {
            TypeKind.Class or TypeKind.Interface or TypeKind.Delegate =>
                await EvaluateWork(pattern.UnderlyingType, candidate, context),
            TypeKind.Struct or TypeKind.Enum => MatchStatus.NotMatched,
            _ => Unknown(
                pattern,
                context,
                "The candidate's value/reference type category is unavailable.")
        };
    }

    private static async StackWork<MatchStatus> EvaluateTupleWork(
        BoundTupleTypePattern pattern,
        TypeSignature candidate,
        EvaluationContext context)
    {
        if (candidate is UnsupportedTypeSignature unsupported)
        {
            context.QueryContext.SpendWork(Math.Max(1, unsupported.DisplayName.Length));
            return Unknown(
                pattern,
                context,
                $"Tuple shape is unavailable for type '{unsupported.DisplayName}'.");
        }

        if (candidate is not NamedTypeSignature named ||
            !IsTupleDefinition(named.Identity))
        {
            return MatchStatus.NotMatched;
        }

        var elements = new List<TypeSignature>();
        var shape = FlattenTuple(named, elements, context);
        if (shape == MatchStatus.NotMatched) return shape;
        if (shape == MatchStatus.Unknown)
        {
            return Unknown(
                pattern,
                context,
                "Tuple element metadata is incomplete.");
        }

        if (elements.Count != pattern.Elements.Count)
        {
            return MatchStatus.NotMatched;
        }

        var status = MatchStatus.Matched;
        for (var index = 0; index < elements.Count; index++)
        {
            status = MatchLogic.And(
                status,
                await EvaluateWork(pattern.Elements[index], elements[index], context));
        }

        return status;
    }

    private static async StackWork<MatchStatus> EvaluateTaskLikeWork(
        BoundTaskLikeTypePattern pattern,
        TypeSignature candidate,
        EvaluationContext context)
    {
        if (candidate is UnsupportedTypeSignature unsupported)
        {
            context.QueryContext.SpendWork(Math.Max(1, unsupported.DisplayName.Length));
            return Unknown(
                pattern,
                context,
                $"Task-like shape is unavailable for type '{unsupported.DisplayName}'.");
        }

        if (candidate is not NamedTypeSignature named ||
            !IsTaskLikeDefinition(named.Identity))
        {
            return MatchStatus.NotMatched;
        }

        // 裸 tasklike 与已拒绝的 tasklike<void> 同义：仅匹配非泛型 Task/ValueTask。
        var resultType = pattern.ResultType;
        if (resultType is null || IsVoidPattern(resultType))
        {
            return named.Identity.GenericArity == 0
                ? MatchStatus.Matched
                : MatchStatus.NotMatched;
        }

        if (named.Identity.GenericArity != 1)
        {
            return MatchStatus.NotMatched;
        }

        var arguments = GenericParameterNamer.GetEffectiveTypeArguments(named, context.QueryContext);
        if (arguments.Length != 1)
        {
            return Unknown(
                pattern,
                context,
                "The task-like result type is unavailable.");
        }

        return await EvaluateWork(resultType, arguments[0], context);
    }

    private static MatchStatus FlattenTuple(
        NamedTypeSignature tuple,
        List<TypeSignature> elements, EvaluationContext context)
    {
        var current = tuple;
        while (true)
        {
            context.QueryContext.SpendWork();
            var arity = current.Identity.GenericArity;
            if (arity > 8) return MatchStatus.NotMatched;
            if (current.TypeArguments.Count != 0 && current.TypeArguments.Count != arity) return MatchStatus.Unknown;
            var arguments = GenericParameterNamer.GetEffectiveTypeArguments(current, context.QueryContext);
            if (arguments.Length != arity) return MatchStatus.Unknown;
            if (arity == 0) return MatchStatus.Matched;

            var directElementCount = arity == 8 ? 7 : arity;
            for (var index = 0; index < directElementCount; index++)
            {
                elements.Add(arguments[index]);
            }

            if (arity != 8) return MatchStatus.Matched;
            // 具体非元组 TRest 是已知形状错误；缺失类型或未实例化形参才是 Unknown。
            var rest = arguments[7];
            if (rest is UnsupportedTypeSignature or GenericParameterTypeSignature) return MatchStatus.Unknown;
            if (rest is not NamedTypeSignature restTuple || !IsTupleDefinition(restTuple.Identity))
                return MatchStatus.NotMatched;
            current = restTuple;
        }
    }

    private static bool IsTupleDefinition(TypeIdentity identity)
    {
        return StringComparer.Ordinal.Equals(identity.Namespace, "System") &&
            StringComparer.Ordinal.Equals(identity.MetadataName, "ValueTuple");
    }

    private static bool IsTaskLikeDefinition(TypeIdentity identity)
    {
        return StringComparer.Ordinal.Equals(
                   identity.Namespace,
                   "System.Threading.Tasks") &&
               (StringComparer.Ordinal.Equals(identity.MetadataName, "Task") ||
                StringComparer.Ordinal.Equals(
                    identity.MetadataName,
                    "ValueTask"));
    }

    private static bool IsVoidPattern(BoundTypePattern pattern)
    {
        return pattern is BoundNamedTypePattern named &&
               StringComparer.Ordinal.Equals(
                   named.CanonicalName,
                   "System.Void");
    }

    private static bool IsNamedDefinition(
        TypeIdentity identity,
        string @namespace,
        string metadataName,
        int arity)
    {
        return identity.GenericArity == arity &&
               StringComparer.Ordinal.Equals(identity.Namespace, @namespace) &&
               StringComparer.Ordinal.Equals(
                   identity.MetadataName,
                   metadataName);
    }
    private static async StackWork<MatchStatus> EvaluateAssignableWork(
        BoundAssignableTypePattern pattern,
        TypeSignature candidate,
        EvaluationContext context)
    {
        if (TryCreateRelationshipTarget(
            pattern.TargetType,
            out var target, context.QueryContext))
        {
            context.Visit(pattern.Span);
            var result = context.TypeSystem.IsAssignableTo(
                candidate,
                target!,
                context.CancellationToken);
            context.AddDiagnostics(result.Diagnostics);
            return result.Status;
        }

        // 具体化失败的命名模式（名称通配、命名空间 .. 等）沿闭包用模式本身求值，
        // 非命名目标无法执行关系查询，使用模式求值遍历类型闭包。
        // nullable 目标（X?+）具体化失败时同样回退闭包求值：候选侧由 EvaluateNullable
        // 分派，非 Nullable<T> 的候选直接 NotMatched，而不是因目标不可具体化返回 Unknown。
        if (pattern.TargetType is BoundNamedTypePattern ||
            pattern.TargetType is BoundNullableTypePattern
            {
                UnderlyingType: BoundNamedTypePattern
            })
        {
            return await EvaluateAssignablePatternWalkWork(
                pattern,
                candidate,
                context);
        }

        return Unknown(
            pattern,
            context,
            "The assignability target is not a concrete named type.");
    }

    private static async StackWork<MatchStatus> EvaluateAssignablePatternWalkWork(
        BoundAssignableTypePattern pattern,
        TypeSignature candidate,
        EvaluationContext context)
    {
        var cancellationToken = context.CancellationToken;
        var closure = context.TypeSystem.GetInheritanceClosure(
            candidate,
            cancellationToken);
        var status = MatchStatus.NotMatched;
        var alternatives = context.CreateAlternativeCollector();
        foreach (var ancestor in AncestorsIncludingSelf(
                     candidate,
                     closure.Ancestors))
        {
            context.Visit(pattern.Span);
            var branch = context.Fork();
            // 后端返回的中立祖先也受限制；超限时保留未知备选，不求值或绑定其内部结构。
            var failure = MetadataLimitGuard.GetFailure(ancestor, context.QueryContext);
            if (failure is not null) branch.AddDiagnostic(failure);
            var branchStatus = failure is null
                ? await EvaluateWork(pattern.TargetType, ancestor, branch)
                : MatchStatus.Unknown;
            context.MergeDiagnosticsFrom(branch);
            status = MatchLogic.Or(status, branchStatus);
            alternatives?.Add(branch, branchStatus);
        }

        // 部分闭包意味着未产出的祖先仍可能匹配，不能静默判为不匹配（规范 12.4）。
        if (closure.Failure is not null)
        {
            context.AddDiagnostics(closure.Failure.Diagnostics);
            var unknownStatus = Unknown(
                pattern,
                context,
                "The inheritance closure could not be fully resolved.");
            status = MatchLogic.Or(status, unknownStatus);
            alternatives?.Add(context.Fork(), unknownStatus);
        }

        alternatives?.Apply();
        return status;
    }

    private static IEnumerable<TypeSignature> AncestorsIncludingSelf(
        TypeSignature candidate,
        IReadOnlyList<TypeSignature> ancestors)
    {
        yield return candidate;
        foreach (var ancestor in ancestors)
        {
            yield return ancestor;
        }
    }

    private static async StackWork<MatchStatus> EvaluateNamedCandidateWork(
        BoundNamedTypePattern pattern,
        NamedTypeSignature candidate,
        EvaluationContext context)
    {
        if (IsPrimitiveAlias(pattern))
        {
            if (!MatchesPrimitiveIdentity(pattern.CanonicalName, candidate.Identity, context.QueryContext) ||
                !MatchesGeneric(
                    pattern.Segments[0].Generic,
                    candidate.Identity.GenericArity))
            {
                return MatchStatus.NotMatched;
            }

            return await EvaluateGenericArgumentsWork(
                pattern,
                candidate,
                new List<TypeIdentity> { candidate.Identity },
                0,
                context);
        }

        var slashIndex = FindFirstSlash(pattern.Separators);
        var outerTypeIndex = slashIndex >= 0
            ? slashIndex
            : pattern.Segments.Count - 1;
        var candidateTypes = GenericParameterNamer.GetTypeChain(candidate.Identity, context.QueryContext);
        if (candidateTypes.Count != pattern.Segments.Count - outerTypeIndex)
        {
            return MatchStatus.NotMatched;
        }

        for (var index = 0; index < candidateTypes.Count; index++)
        {
            var patternSegment = pattern.Segments[outerTypeIndex + index];
            var identity = candidateTypes[index];
            if (!NamePatternMatcher.IsMatch(patternSegment.Pattern, identity.MetadataName, context.QueryContext) ||
                !MatchesGeneric(patternSegment.Generic, identity.GenericArity))
            {
                return MatchStatus.NotMatched;
            }
        }

        if (!MatchesNamespaceAndOuterType(
                pattern,
                outerTypeIndex,
                candidateTypes[0], context))
        {
            return MatchStatus.NotMatched;
        }

        return await EvaluateGenericArgumentsWork(
            pattern,
            candidate,
            candidateTypes,
            outerTypeIndex,
            context);
    }

    private static async StackWork<MatchStatus> EvaluateGenericArgumentsWork(
        BoundNamedTypePattern pattern,
        NamedTypeSignature candidate,
        List<TypeIdentity> candidateTypes,
        int outerTypeIndex,
        EvaluationContext context)
    {
        var requiresArguments = false;
        var totalArity = 0;
        for (var index = 0; index < candidateTypes.Count; index++)
        {
            totalArity += candidateTypes[index].GenericArity;
            requiresArguments |= pattern
                .Segments[outerTypeIndex + index]
                .Generic
                .Arguments
                .Count > 0;
        }

        if (!requiresArguments) return MatchStatus.Matched;
        // 与成员泛型子句同理：定义签名没有实参时回退到按声明链合成的形式参数。
        var candidateArguments = GenericParameterNamer.GetEffectiveTypeArguments(candidate, context.QueryContext);
        if (candidateArguments.Length != totalArity)
        {
            return Unknown(
                pattern,
                context,
                "Constructed generic arguments are unavailable for this type.");
        }

        var status = MatchStatus.Matched;
        var candidateArgumentOffset = 0;
        for (var typeIndex = 0;
             typeIndex < candidateTypes.Count;
             typeIndex++)
        {
            var generic = pattern
                .Segments[outerTypeIndex + typeIndex]
                .Generic;
            for (var argumentIndex = 0;
                 argumentIndex < generic.Arguments.Count;
                 argumentIndex++)
            {
                var argumentStatus = await EvaluateWork(
                    generic.Arguments[argumentIndex],
                    candidateArguments[
                        candidateArgumentOffset + argumentIndex],
                    context);
                status = MatchLogic.And(status, argumentStatus);
            }

            candidateArgumentOffset += candidateTypes[typeIndex].GenericArity;
        }

        return status;
    }

    private static bool MatchesPrimitiveIdentity(string canonicalName, TypeIdentity identity, TypeSystemQueryContext context)
    {
        if (identity.ContainingType is not null || identity.GenericArity != 0) return false;
        var separator = canonicalName.LastIndexOf('.');
        return context.StringsEqual(canonicalName.Substring(0, separator), identity.Namespace) &&
            context.StringsEqual(canonicalName.Substring(separator + 1), identity.MetadataName);
    }

    private static bool MatchesNamespaceAndOuterType(
        BoundNamedTypePattern pattern,
        int outerTypeIndex,
        TypeIdentity outerType, EvaluationContext context)
    {
        if (outerTypeIndex == 0) return true;

        context.QueryContext.SpendWork(Math.Max(1, outerType.Namespace.Length));
        var namespaceSegments = string.IsNullOrEmpty(outerType.Namespace)
            ? Array.Empty<string>()
            : outerType.Namespace.Split('.');
        var candidateSegments = new string[namespaceSegments.Length + 1];
        Array.Copy(
            namespaceSegments,
            candidateSegments,
            namespaceSegments.Length);
        candidateSegments[candidateSegments.Length - 1] = outerType.MetadataName;
        if (candidateSegments.Length < outerTypeIndex + 1 ||
            !NamePatternMatcher.IsMatch(pattern.Segments[0].Pattern, candidateSegments[0], context.QueryContext))
        {
            return false;
        }

        var reachable = new bool[candidateSegments.Length];
        reachable[0] = true;
        for (var patternIndex = 1;
             patternIndex <= outerTypeIndex;
             patternIndex++)
        {
            context.QueryContext.SpendWork(candidateSegments.Length);
            var next = new bool[candidateSegments.Length];
            var separator = pattern.Separators[patternIndex - 1];
            var earlierReachable = false;
            for (var candidateIndex = 1;
                 candidateIndex < candidateSegments.Length;
                 candidateIndex++)
            {
                earlierReachable |= reachable[candidateIndex - 1];
                var canReach = separator == ".."
                    ? earlierReachable
                    : reachable[candidateIndex - 1];
                next[candidateIndex] = canReach &&
                    NamePatternMatcher.IsMatch(pattern.Segments[patternIndex].Pattern, candidateSegments[candidateIndex], context.QueryContext);
            }

            reachable = next;
        }

        return reachable[reachable.Length - 1];
    }

    private static bool MatchesGeneric(BoundGenericPattern pattern, int arity)
    {
        return pattern.Kind switch
        {
            BoundGenericArityKind.Any => true,
            BoundGenericArityKind.AnyPositive => arity > 0,
            _ => pattern.Arity == arity
        };
    }

    private static bool IsPrimitiveAlias(BoundNamedTypePattern pattern)
    {
        return pattern.Segments.Count == 1 &&
               !string.Equals(
                   pattern.CanonicalName,
                   pattern.Segments[0].Pattern,
                   StringComparison.Ordinal);
    }

    private static int FindFirstSlash(IReadOnlyList<string> separators)
    {
        for (var index = 0; index < separators.Count; index++)
        {
            if (separators[index] == "/") return index;
        }

        return -1;
    }

    private static List<TypeIdentity> GetTypeChain(TypeIdentity identity)
    {
        var types = new List<TypeIdentity>();
        for (var current = identity;
             current is not null;
             current = current.ContainingType)
        {
            types.Add(current);
        }

        types.Reverse();
        return types;
    }

    private static bool TryCreateRelationshipTarget(
        BoundTypePattern pattern,
        out TypeSignature? target, TypeSystemQueryContext context)
    {
        if (!TryCreateConcreteType(
                pattern,
                out target, context) ||
            target is not NamedTypeSignature)
        {
            target = null;
            return false;
        }

        return true;
    }

    internal static bool TryCreateConcreteType(BoundTypePattern pattern, out TypeSignature? type,
        TypeSystemQueryContext? context = null)
    {
        context ??= new TypeSystemQueryContext(new AspectNMatchOptions(), default);
        type = WorkStack.Run(CreateConcreteTypeWork(pattern, context), context.CancellationToken);
        return type is not null;
    }

    private static async StackWork<TypeSignature?> CreateConcreteTypeWork(BoundTypePattern pattern, TypeSystemQueryContext context)
    {
        context.SpendWork();
        if (pattern is BoundNamedTypePattern named) return await CreateConcreteNamedTypeWork(named, context);
        // 秩一模式同时接受两种 CLI 数组身份，仍不能提前具体化。
        if (pattern is BoundArrayTypePattern { Rank: > 1 } array)
        {
            var element = await CreateConcreteTypeWork(array.ElementType, context);
            return element is null ? null : new ArrayTypeSignature(element, array.Rank);
        }
        if (pattern is BoundNullableTypePattern nullable)
        {
            var underlying = await CreateConcreteTypeWork(nullable.UnderlyingType, context);
            if (underlying is ArrayTypeSignature) return underlying;
            if (underlying is NamedTypeSignature underlyingNamed)
            {
                if (IsKnownReferenceType(underlyingNamed.Identity)) return underlying;
                if (IsKnownValueType(underlyingNamed.Identity)) return CreateNullableWrapper(underlying);
            }
        }
        return null;
    }

    private static NamedTypeSignature CreateNullableWrapper(
        TypeSignature underlying)
    {
        var identity = new TypeIdentity(
            new AssemblyIdentity(string.Empty),
            "System",
            "Nullable",
            1,
            TypeKind.Unknown);
        var definition = new NamedTypeSignature(identity);
        return new NamedTypeSignature(
            identity,
            new[] { underlying },
            definition);
    }

    private static bool IsKnownValueType(TypeIdentity identity)
    {
        return identity.GenericArity == 0 &&
            identity.ContainingType is null &&
            string.Equals(
                identity.Namespace,
                "System",
                StringComparison.Ordinal) &&
            _KnownValueTypeNames.Contains(identity.MetadataName);
    }

    private static bool IsKnownReferenceType(TypeIdentity identity)
    {
        return identity.GenericArity == 0 &&
            identity.ContainingType is null &&
            string.Equals(
                identity.Namespace,
                "System",
                StringComparison.Ordinal) &&
            _KnownReferenceTypeNames.Contains(identity.MetadataName);
    }

    private static async StackWork<TypeSignature?> CreateConcreteNamedTypeWork(BoundNamedTypePattern named,
        TypeSystemQueryContext context)
    {
        TypeSignature? type;
        foreach (var segment in named.Segments) context.SpendWork(Math.Max(1, segment.Pattern.Length));
        if (named.Separators.Count > 0) context.SpendWork(named.Separators.Count);
        if (named.Segments.Any(segment => segment.Pattern.Contains('*')) ||
            named.Separators.Contains(".."))
        {
            type = null;
            return null;
        }

        if (IsPrimitiveAlias(named))
        {
            // 别名只在子句为"零元数/任意元数且无实参"（省略、<!>、<?>）时可提前具体化为
            // 零泛型类型——别名只存在于零元数，这两类子句的约束与之相容。
            // 其余形态（<>、<..>、带实参）必须回退模式求值，否则约束被悄悄丢弃
            // （int<>+ 曾因此错误命中 int，与 System.Int32<>+ 不一致）。
            var aliasGeneric = named.Segments[0].Generic;
            if (aliasGeneric.Arguments.Count == 0 &&
                (aliasGeneric.Kind == BoundGenericArityKind.Any ||
                 (aliasGeneric.Kind == BoundGenericArityKind.Exact &&
                  aliasGeneric.Arity == 0)))
            {
                var separator = named.CanonicalName.LastIndexOf('.');
                type = new NamedTypeSignature(
                    new TypeIdentity(
                        new AssemblyIdentity(string.Empty),
                        separator < 0
                            ? string.Empty
                            : named.CanonicalName.Substring(0, separator),
                        named.CanonicalName.Substring(separator + 1),
                        0,
                        TypeKind.Unknown));
                return type;
            }

            type = null;
            return null;
        }

        var slashIndex = FindFirstSlash(named.Separators);
        var outerIndex = slashIndex >= 0
            ? slashIndex
            : named.Segments.Count - 1;
        var namespaceLength = named.Segments.Take(outerIndex).Sum(segment => (long)segment.Pattern.Length + 1);
        if (namespaceLength > 0) context.SpendWork(namespaceLength);
        var namespaceName = BuildNamespace(named, outerIndex);
        TypeIdentity? containingType = null;
        var typeArguments = new List<TypeSignature>();
        for (var index = outerIndex; index < named.Segments.Count; index++)
        {
            var segment = named.Segments[index];
            // 具体目标要求泛型完全具体化：固定元数必须显式给出全部实参（规范：Name<> 表示一个任意实参），
            // 否则回退到闭包模式求值；嵌套实参的递归具体化同样由 TryCreateConcreteType 强制。
            // 通配元数无法形成具体类型，同样回退到闭包模式求值。
            if (segment.Generic.Kind == BoundGenericArityKind.AnyPositive ||
                segment.Generic.Kind == BoundGenericArityKind.Any ||
                (segment.Generic.Arity > 0 &&
                 segment.Generic.Arguments.Count != segment.Generic.Arity))
            {
                type = null;
                return null;
            }

            foreach (var argument in segment.Generic.Arguments)
            {
                var typeArgument = await CreateConcreteTypeWork(argument, context);
                if (typeArgument is null)
                {
                    type = null;
                    return null;
                }

                context.SpendWork();
                typeArguments.Add(typeArgument);
            }

            containingType = new TypeIdentity(
                new AssemblyIdentity(string.Empty),
                index == outerIndex ? namespaceName : string.Empty,
                segment.Pattern,
                segment.Generic.Arity,
                TypeKind.Unknown,
                containingType);
        }

        context.SpendWork(Math.Max(1, typeArguments.Count));
        var definition = new NamedTypeSignature(containingType!);
        type = typeArguments.Count == 0
            ? definition
            : new NamedTypeSignature(
                containingType!,
                typeArguments,
                definition);
        return type;
    }

    private static string BuildNamespace(
        BoundNamedTypePattern pattern,
        int outerTypeIndex)
    {
        if (outerTypeIndex == 0) return string.Empty;
        return string.Join(
            ".",
            pattern.Segments
                .Take(outerTypeIndex)
                .Select(segment => segment.Pattern));
    }

    private static MatchStatus Unknown(
        BoundTypePattern pattern,
        EvaluationContext context,
        string message)
    {
        context.AddDiagnostic(
            new AspectNDiagnostic(
                DiagnosticCodes.MissingBackendCapability,
                DiagnosticSeverity.Warning,
                message,
                pattern.Span));
        return MatchStatus.Unknown;
    }
}
