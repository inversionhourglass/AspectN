using System.Collections.ObjectModel;
using System.Text;
using AspectN.Diagnostics;
using AspectN.Metadata;
using AspectN.Parsing;
using AspectN.Syntax;
using AspectN.Text;

namespace AspectN.Binding;

internal sealed partial class Binder(
    AspectNSyntaxTree syntaxTree)
{
    private static readonly Dictionary<string, string> _PrimitiveAliases =
        new(StringComparer.Ordinal)
        {
            ["bool"] = "System.Boolean",
            ["byte"] = "System.Byte",
            ["sbyte"] = "System.SByte",
            ["short"] = "System.Int16",
            ["ushort"] = "System.UInt16",
            ["int"] = "System.Int32",
            ["uint"] = "System.UInt32",
            ["long"] = "System.Int64",
            ["ulong"] = "System.UInt64",
            ["char"] = "System.Char",
            ["float"] = "System.Single",
            ["double"] = "System.Double",
            ["decimal"] = "System.Decimal",
            ["string"] = "System.String",
            ["object"] = "System.Object",
            ["void"] = "System.Void"
        };

    private readonly DiagnosticBag _diagnostics = new();
    private readonly AspectNSyntaxTree _syntaxTree = syntaxTree ??
            throw new ArgumentNullException(nameof(syntaxTree));
    private int _negationDepth;
    private int _referenceOnlyDepth;
    private int _genericDefinitionDepth;
    private int _genericDefinitionNegationDepth;

    public IReadOnlyList<AspectNDiagnostic> Diagnostics =>
        new ReadOnlyCollection<AspectNDiagnostic>(_diagnostics.ToArray());

    // 绑定阶段将语法树转换为带类型语义的节点，并把无法恢复的错误统一收集到诊断集合。
    public BoundNode? Bind()
    {
        var scope = new VariableScope();
        return _syntaxTree.ParseKind switch
        {
            AspectNParseKind.MemberExpression =>
                BindExpression((ExpressionSyntax)_syntaxTree.Root, scope),
            AspectNParseKind.TypePattern => BindTypeRoot(_syntaxTree.Root, scope),
            _ => null
        };
    }

    private BoundTypePattern BindTypeRoot(SyntaxNode root, VariableScope scope)
    {
        if (root is TypeUseSyntax typeUse)
        {
            _diagnostics.Add(
                DiagnosticCodes.InvalidTargetDomain,
                DiagnosticSeverity.Error,
                "A top-level type pattern cannot specify a ref kind.",
                typeUse.FullSpan);
            return BindTypePattern(typeUse.TypePattern, scope, false);
        }

        return BindTypePattern((TypePatternSyntax)root, scope, false);
    }

    // 表达式绑定需要沿着分支复制作用域，保证逻辑运算两侧的变量约束不会相互污染。
    private BoundExpression BindExpression(
        ExpressionSyntax syntax,
        VariableScope scope)
    {
        switch (syntax)
        {
            case SelectorSyntax selector:
                return BindSelector(selector, scope);
            case ParenthesizedExpressionSyntax parenthesized:
                return BindExpression(parenthesized.Expression, scope);
            case UnaryExpressionSyntax unary:
                {
                    var childScope = scope.Clone();
                    BoundExpression operand;
                    _negationDepth++;
                    try
                    {
                        operand = BindExpression(unary.Operand, childScope);
                    }
                    finally
                    {
                        _negationDepth--;
                    }

                    scope.HideNewVariablesFrom(childScope);
                    return new BoundUnaryExpression(unary.FullSpan, operand);
                }
            case BinaryExpressionSyntax binary:
                return BindBinaryExpression(binary, scope);
            default:
                throw new InvalidOperationException(
                    $"Unsupported expression syntax '{syntax.Kind}'.");
        }
    }

    private BoundBinaryExpression BindBinaryExpression(
        BinaryExpressionSyntax syntax,
        VariableScope scope)
    {
        if (syntax.OperatorToken.Kind == SyntaxKind.AmpersandAmpersandToken)
        {
            var left = BindExpression(syntax.Left, scope);
            var right = BindExpression(syntax.Right, scope);
            var domain = left.TargetDomain & right.TargetDomain;
            if (domain == SelectorTargetDomain.None)
            {
                _diagnostics.Add(
                    DiagnosticCodes.InvalidTargetDomain,
                    DiagnosticSeverity.Error,
                    "The selector domains on both sides of '&&' do not overlap.",
                    syntax.OperatorToken.Span);
            }

            return new BoundBinaryExpression(
                syntax.FullSpan,
                left,
                BoundLogicalOperator.And,
                right,
                domain);
        }

        var leftScope = scope.Clone();
        var rightScope = scope.Clone();
        var leftBranch = BindExpression(syntax.Left, leftScope);
        var rightBranch = BindExpression(syntax.Right, rightScope);
        ReportDuplicateBranchVariables(
            scope,
            leftScope,
            rightScope,
            syntax.OperatorToken);
        scope.HideNewVariablesFrom(leftScope);
        scope.HideNewVariablesFrom(rightScope);
        return new BoundBinaryExpression(
            syntax.FullSpan,
            leftBranch,
            BoundLogicalOperator.Or,
            rightBranch,
            leftBranch.TargetDomain | rightBranch.TargetDomain);
    }

    private BoundSelector BindSelector(
        SelectorSyntax syntax,
        VariableScope scope)
    {
        return syntax switch
        {
            CallableSelectorSyntax callable => new BoundSelector(
                callable.FullSpan,
                BoundSelectorKind.Callable,
                EqualsKeyword(callable.KeywordToken!.Text, "execution")
                    ? SelectorTargetDomain.All
                    : SelectorTargetDomain.Callable,
                BindMethodSignature(
                    (MethodSignatureSyntax)callable.Body!,
                    scope)),
            MethodSelectorSyntax method => new BoundSelector(
                method.FullSpan,
                BoundSelectorKind.Method,
                SelectorTargetDomain.OrdinaryMethod,
                BindMethodSignature(
                    (MethodSignatureSyntax)method.Body!,
                    scope)),
            PropertySelectorSyntax property => new BoundSelector(
                property.FullSpan,
                BoundSelectorKind.Property,
                SelectorTargetDomain.PropertyAccessor,
                BindPropertySignature(
                    (PropertySignatureSyntax)property.Body!,
                    scope)),
            GetterSelectorSyntax getter => new BoundSelector(
                getter.FullSpan,
                BoundSelectorKind.Getter,
                SelectorTargetDomain.PropertyGetter,
                BindPropertySignature(
                    (PropertySignatureSyntax)getter.Body!,
                    scope)),
            SetterSelectorSyntax setter => new BoundSelector(
                setter.FullSpan,
                BoundSelectorKind.Setter,
                SelectorTargetDomain.PropertySetter,
                BindPropertySignature(
                    (PropertySignatureSyntax)setter.Body!,
                    scope)),
            ConstructorSelectorSyntax constructor => new BoundSelector(
                constructor.FullSpan,
                BoundSelectorKind.Constructor,
                SelectorTargetDomain.InstanceConstructor,
                BindConstructorSignature(
                    (ConstructorSignatureSyntax)constructor.Body!,
                    scope)),
            StaticConstructorSelectorSyntax staticConstructor =>
                new BoundSelector(
                    staticConstructor.FullSpan,
                    BoundSelectorKind.StaticConstructor,
                    SelectorTargetDomain.StaticConstructor,
                    new BoundStaticConstructorSignature(
                        staticConstructor.Body!.FullSpan,
                        BindTypePattern(
                            (TypePatternSyntax)staticConstructor.Body,
                            scope,
                            IsUnderNegation))),
            AttributeSelectorSyntax attribute => BindAttributeSelector(
                attribute,
                scope),
            RegexSelectorSyntax regex => new BoundSelector(
                regex.FullSpan,
                BoundSelectorKind.Regex,
                SelectorTargetDomain.All,
                new BoundRegexPattern(
                    regex.RegexBodyToken.Span,
                    regex.RegexBodyToken.Text)),
            _ => throw new InvalidOperationException(
                $"Unsupported selector syntax '{syntax.Kind}'.")
        };
    }

    private BoundSelector BindAttributeSelector(
        AttributeSelectorSyntax syntax,
        VariableScope scope)
    {
        var signature = (AttributeSignatureSyntax)syntax.Body!;
        var tokens = signature.Parts.OfType<SyntaxToken>().ToArray();
        var typeSyntax = signature.Parts.OfType<TypePatternSyntax>().Last();
        var targetText = tokens.Length == 0 ? string.Empty : tokens[0].Text;
        var target = GetAttributeTarget(targetText);
        var parameterValue = tokens.FirstOrDefault(
            token => token.Kind == SyntaxKind.IntegerToken)?.Value;
        int? parameterIndex = null;
        if (parameterValue is long value &&
            value >= int.MinValue &&
            value <= int.MaxValue)
        {
            parameterIndex = (int)value;
        }
        else if (parameterValue is not null)
        {
            // 越界索引不能退化为通配符扩大匹配范围：报告诊断，并绑定到永不匹配的哨兵索引。
            _diagnostics.Add(
                DiagnosticCodes.InvalidParameterIndex,
                DiagnosticSeverity.Error,
                $"The parameter index '{parameterValue}' is outside the Int32 range.",
                syntax.FullSpan);
            parameterIndex = int.MinValue;
        }
        var domain = target == BoundAttributeTargetKind.Property
            ? SelectorTargetDomain.PropertyAccessor
            : SelectorTargetDomain.All;
        return new BoundSelector(
            syntax.FullSpan,
            BoundSelectorKind.Attribute,
            domain,
            new BoundAttributePattern(
                signature.FullSpan,
                target,
                parameterIndex,
                BindTypePattern(typeSyntax, scope, IsUnderNegation)));
    }

    private BoundMethodSignature BindMethodSignature(
        MethodSignatureSyntax syntax,
        VariableScope scope)
    {
        var modifiers = BindModifiers(syntax.Parts.OfType<ModifierSyntax>());
        var returnSyntax = syntax.Parts.OfType<TypeUseSyntax>().First();
        var member = BindMemberPattern(syntax.Parts.OfType<MemberPathSyntax>().Single(), scope, false);
        var returnType = BindTypeUse(returnSyntax, scope, true);
        var parameters = syntax.Parts
            .OfType<ParameterSyntax>()
            .Select(parameter =>
                BindTypeUse(parameter.TypeUse, scope, false))
            .ToArray();
        return new BoundMethodSignature(
            syntax.FullSpan,
            modifiers,
            returnType,
            member,
            parameters,
            MatchesAnyParameters(syntax.Parts));
    }

    private BoundPropertySignature BindPropertySignature(
        PropertySignatureSyntax syntax,
        VariableScope scope)
    {
        var typeSyntax = syntax.Parts.OfType<TypeUseSyntax>().First();
        var member = BindMemberPattern(syntax.Parts.OfType<MemberPathSyntax>().Single(), scope, true);
        return new BoundPropertySignature(
            syntax.FullSpan,
            BindModifiers(syntax.Parts.OfType<ModifierSyntax>()),
            BindTypeUse(typeSyntax, scope, true),
            member);
    }

    private BoundConstructorSignature BindConstructorSignature(
        ConstructorSignatureSyntax syntax,
        VariableScope scope)
    {
        // 声明类型在源码顺序上先于参数，其引入的变量必须先进入作用域，参数内的引用才是合法引用。
        var declaringType = syntax.Parts.OfType<TypePatternSyntax>().First();
        var declaringTypePattern = BindTypePattern(declaringType, scope, IsUnderNegation);
        var parameters = syntax.Parts
            .OfType<ParameterSyntax>()
            .Select(parameter =>
                BindTypeUse(parameter.TypeUse, scope, false))
            .ToArray();
        return new BoundConstructorSignature(
            syntax.FullSpan,
            declaringTypePattern,
            parameters,
            MatchesAnyParameters(syntax.Parts));
    }

    private BoundTypeUsePattern BindTypeUse(
        TypeUseSyntax syntax,
        VariableScope scope,
        bool isReturn)
    {
        var refText = string.Join(
            " ",
            syntax.RefKindTokens.Select(token => token.Text));
        var refKind = refText switch
        {
            "ref" => RefKind.Ref,
            "out" => RefKind.Out,
            "in" => RefKind.In,
            "ref readonly" => RefKind.RefReadOnly,
            _ => RefKind.None
        };
        if (isReturn && refKind is RefKind.Out or RefKind.In)
        {
            _diagnostics.Add(
                DiagnosticCodes.InvalidRefKind,
                DiagnosticSeverity.Error,
                $"'{refText}' is not valid for a return type.",
                syntax.FullSpan);
        }

        var negatedRef = syntax.NegatedRefTokens.Count > 0;
        _referenceOnlyDepth++;
        try
        {
            return new BoundTypeUsePattern(
                refKind,
                negatedRef,
                BindTypePattern(syntax.TypePattern, scope, IsUnderNegation));
        }
        finally
        {
            _referenceOnlyDepth--;
        }
    }

    private List<BoundModifier> BindModifiers(
        IEnumerable<ModifierSyntax> modifierSyntaxes)
    {
        var modifiers = new List<BoundModifier>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var positiveAccessibilityCount = 0;
        var hasPositiveStatic = false;
        var hasNegativeStatic = false;
        foreach (var syntax in modifierSyntaxes)
        {
            var isNegated = syntax.Tokens[0].Kind == SyntaxKind.BangToken;
            // 括号只是复合访问性的分组书写，不参与语义内容；空格/括号都不表达“多选”，
            // 多选由 PipeToken 分支处理。
            var content = string.Join(
                " ",
                syntax.Tokens
                    .Where(token => token.Kind
                        is not (SyntaxKind.BangToken
                            or SyntaxKind.OpenParenToken
                            or SyntaxKind.CloseParenToken))
                    .Select(token => token.Text));
            BoundModifier modifier;
            if (!isNegated &&
                syntax.Tokens.Any(token => token.Kind == SyntaxKind.PipeToken))
            {
                modifier = BindAccessibilityAlternation(syntax);
                positiveAccessibilityCount++;
            }
            else if (content == "static")
            {
                modifier = new BoundModifier(BoundModifierKind.Static, isNegated);
                hasPositiveStatic |= !isNegated;
                hasNegativeStatic |= isNegated;
            }
            else
            {
                modifier = new BoundModifier(
                    BoundModifierKind.Accessibility,
                    isNegated,
                    GetAccessibility(content));
                if (!isNegated) positiveAccessibilityCount++;
            }

            var key = modifier.Alternatives is not null
                ? $"{modifier.Kind}:|{string.Join("|", modifier.Alternatives)}|"
                : $"{modifier.Kind}:{modifier.Accessibility}:{isNegated}";
            if (!keys.Add(key))
            {
                ReportInvalidModifiers(
                    syntax,
                    $"Modifier '{content}' is duplicated.");
            }

            modifiers.Add(modifier);
        }

        if (positiveAccessibilityCount > 1)
        {
            ReportInvalidModifiers(
                modifierSyntaxes.First(),
                "A Standard signature can contain at most one positive accessibility.");
        }

        if (hasPositiveStatic && hasNegativeStatic)
        {
            ReportInvalidModifiers(
                modifierSyntaxes.First(),
                "A signature cannot require both static and !static.");
        }

        return modifiers;
    }

    // 规范 §10：| 多选组整体算作一个正向 accessibility；每个备选经同一 GetAccessibility 映射，
    // static 或非权限词在此映射为 Unknown 并报错。解析器已保证否定不会进入多选。
    private BoundModifier BindAccessibilityAlternation(ModifierSyntax syntax)
    {
        var alternatives = new List<Accessibility>();
        var words = new List<string>();
        foreach (var token in syntax.Tokens)
        {
            switch (token.Kind)
            {
                case SyntaxKind.PipeToken:
                    FlushAlternative();
                    break;
                case SyntaxKind.OpenParenToken:
                case SyntaxKind.CloseParenToken:
                    break;
                default:
                    words.Add(token.Text);
                    break;
            }
        }

        FlushAlternative();
        return new BoundModifier(
            BoundModifierKind.Accessibility,
            false,
            alternatives.Count > 0 ? alternatives[0] : Accessibility.Unknown,
            alternatives);

        void FlushAlternative()
        {
            if (words.Count == 0) return;
            var text = string.Join(" ", words);
            words.Clear();
            var accessibility = GetAccessibility(text);
            if (accessibility == Accessibility.Unknown)
            {
                ReportInvalidModifiers(
                    syntax,
                    $"'{text}' is not a valid accessibility alternative.");
                return;
            }

            if (alternatives.Contains(accessibility))
            {
                ReportInvalidModifiers(
                    syntax,
                    $"Accessibility '{text}' is duplicated.");
                return;
            }

            alternatives.Add(accessibility);
        }
    }

    private BoundTypePattern BindTypePattern(
        TypePatternSyntax syntax,
        VariableScope scope,
        bool underNegation)
    {
        switch (syntax)
        {
            case NamedTypePatternSyntax named:
                return BindNamedTypePattern(named, scope, underNegation);
            case WildcardTypePatternSyntax wildcard:
                return new BoundWildcardTypePattern(wildcard.FullSpan);
            case VariableTypePatternSyntax variable:
                return BindVariable(
                    variable.FullSpan,
                    variable.IdentifierToken.Text,
                    scope,
                    underNegation);
            case ArrayTypePatternSyntax array:
                return BindArrayTypePattern(array, scope, underNegation);
            case NullableTypePatternSyntax nullable:
                return new BoundNullableTypePattern(
                    nullable.FullSpan,
                    BindTypePattern(nullable.UnderlyingType, scope, underNegation));
            case AssignableTypePatternSyntax assignable:
                ReportInvalidAssignableTarget(assignable);
                return new BoundAssignableTypePattern(
                    assignable.FullSpan,
                    BindTypePattern(
                        assignable.TargetType,
                        scope,
                        underNegation));
            case TupleTypePatternSyntax tuple:
                return new BoundTupleTypePattern(
                    tuple.FullSpan,
                    tuple.Elements.Select(element =>
                        BindTypePattern(element, scope, underNegation)));
            case TaskLikeTypePatternSyntax taskLike:
                if (taskLike.ResultType is null)
                {
                    return new BoundTaskLikeTypePattern(taskLike.FullSpan, null);
                }

                var taskLikeResult = BindTypePattern(
                    taskLike.ResultType,
                    scope,
                    underNegation);
                if (IsVoidNamedPattern(taskLikeResult))
                {
                    _diagnostics.Add(
                        DiagnosticCodes.TaskLikeVoidMigration,
                        DiagnosticSeverity.Error,
                        "Use 'tasklike' instead of 'tasklike<void>'.",
                        taskLike.ResultType.FullSpan);
                }

                return new BoundTaskLikeTypePattern(
                    taskLike.FullSpan,
                    taskLikeResult);
            case ParenthesizedTypePatternSyntax parenthesized:
                return BindTypePattern(
                    parenthesized.Pattern,
                    scope,
                    underNegation);
            case UnaryTypePatternSyntax unary:
                {
                    var childScope = scope.Clone();
                    var negatesGenericDefinition = _genericDefinitionDepth > 0 && _referenceOnlyDepth == 0;
                    if (negatesGenericDefinition) _genericDefinitionNegationDepth++;
                    try
                    {
                        var operand = BindTypePattern(unary.Operand, childScope, true);
                        scope.HideNewVariablesFrom(childScope);
                        return new BoundUnaryTypePattern(unary.FullSpan, operand);
                    }
                    finally
                    {
                        if (negatesGenericDefinition) _genericDefinitionNegationDepth--;
                    }
                }
            case BinaryTypePatternSyntax binary:
                return BindBinaryTypePattern(binary, scope, underNegation);
            default:
                throw new InvalidOperationException(
                    $"Unsupported type-pattern syntax '{syntax.Kind}'.");
        }
    }

    private BoundArrayTypePattern BindArrayTypePattern(
        ArrayTypePatternSyntax syntax,
        VariableScope scope,
        bool underNegation)
    {
        var suffixes = new List<ArrayTypePatternSyntax>();
        TypePatternSyntax elementType = syntax;
        while (elementType is ArrayTypePatternSyntax array)
        {
            suffixes.Add(array);
            elementType = array.ElementType;
        }

        BoundTypePattern bound = BindTypePattern(elementType, scope, underNegation);
        foreach (var suffix in suffixes)
        {
            bound = new BoundArrayTypePattern(
                suffix.FullSpan,
                bound,
                suffix.Rank);
        }

        return (BoundArrayTypePattern)bound;
    }

    private static TypePatternSyntax UnwrapParenthesized(TypePatternSyntax syntax)
    {
        while (syntax is ParenthesizedTypePatternSyntax parenthesized)
        {
            syntax = parenthesized.Pattern;
        }

        return syntax;
    }

    private BoundBinaryTypePattern BindBinaryTypePattern(
        BinaryTypePatternSyntax syntax,
        VariableScope scope,
        bool underNegation)
    {
        if (syntax.OperatorToken.Kind == SyntaxKind.AmpersandAmpersandToken)
        {
            return new BoundBinaryTypePattern(
                syntax.FullSpan,
                BindTypePattern(syntax.Left, scope, underNegation),
                BoundTypeLogicalOperator.And,
                BindTypePattern(syntax.Right, scope, underNegation));
        }

        var leftScope = scope.Clone();
        var rightScope = scope.Clone();
        var left = BindTypePattern(syntax.Left, leftScope, underNegation);
        var right = BindTypePattern(syntax.Right, rightScope, underNegation);
        ReportDuplicateBranchVariables(
            scope,
            leftScope,
            rightScope,
            syntax.OperatorToken);
        scope.HideNewVariablesFrom(leftScope);
        scope.HideNewVariablesFrom(rightScope);
        return new BoundBinaryTypePattern(
            syntax.FullSpan,
            left,
            BoundTypeLogicalOperator.Or,
            right);
    }

    private BoundNamedTypePattern BindNamedTypePattern(
        NamedTypePatternSyntax syntax,
        VariableScope scope,
        bool underNegation)
    {
        var outerTypeIndex = syntax.Separators.ToList().FindIndex(token => token.Kind == SyntaxKind.SlashToken);
        if (outerTypeIndex < 0) outerTypeIndex = syntax.Segments.Count - 1;
        var segments = syntax.Segments.Select((segment, index) =>
        {
            var genericSyntax = segment.GenericArguments;
            if (index < outerTypeIndex && genericSyntax is not null)
            {
                _diagnostics.Add(
                    DiagnosticCodes.InvalidGenericClause,
                    DiagnosticSeverity.Error,
                    "A namespace segment cannot have a generic clause; remove it or use '/' for a nested type.",
                    genericSyntax.FullSpan);
                genericSyntax = null;
            }
            var pattern = string.Concat(
                segment.NameParts.Select(token => token.Text));
            return new BoundNameSegment(
                pattern,
                BindGenericPattern(
                    genericSyntax,
                    pattern == "*",
                    scope,
                    underNegation));
        }).ToArray();
        var separators = syntax.Separators.Select(token => token.Text).ToArray();
        var sourceName = BuildCanonicalName(segments, separators);
        var canonicalName = _PrimitiveAliases.TryGetValue(
            sourceName,
            out var alias)
            ? alias
            : sourceName;
        if (canonicalName == "System.Void" &&
            syntax.Segments.Any(segment => segment.GenericArguments is not null))
        {
            // void 永远非泛型，拒绝它的一切泛型子句。
            _diagnostics.Add(
                DiagnosticCodes.InvalidGenericClause,
                DiagnosticSeverity.Error,
                "The 'void' type cannot have a generic clause.",
                syntax.FullSpan);
        }

        return new BoundNamedTypePattern(
            syntax.FullSpan,
            segments,
            separators,
            canonicalName);
    }

    private BoundGenericPattern BindGenericPattern(
        GenericPatternListSyntax? syntax,
        bool nameIsWildcard,
        VariableScope scope,
        bool underNegation)
    {
        if (syntax is null)
        {
            return new BoundGenericPattern(
                nameIsWildcard
                    ? BoundGenericArityKind.Any
                    : BoundGenericArityKind.Exact,
                0);
        }

        BoundTypePattern[] arguments;
        _genericDefinitionDepth++;
        try
        {
            arguments = syntax.Arguments.Select(argument =>
                BindTypePattern(argument, scope, underNegation)).ToArray();
        }
        finally
        {
            _genericDefinitionDepth--;
        }
        return syntax.Form switch
        {
            GenericPatternForm.AnyPositiveArity => new BoundGenericPattern(
                BoundGenericArityKind.AnyPositive,
                0),
            GenericPatternForm.AnyArity => new BoundGenericPattern(
                BoundGenericArityKind.Any,
                0),
            GenericPatternForm.Arguments => new BoundGenericPattern(
                BoundGenericArityKind.Exact,
                syntax.Arity,
                arguments),
            _ => new BoundGenericPattern(
                BoundGenericArityKind.Exact,
                syntax.Arity)
        };
    }

    private BoundVariableTypePattern BindVariable(
        AspectN.Text.TextSpan span,
        string name,
        VariableScope scope,
        bool underNegation)
    {
        if (_genericDefinitionNegationDepth > 0)
        {
            _diagnostics.Add(
                DiagnosticCodes.InvalidVariable,
                DiagnosticSeverity.Error,
                $"Variable '${name}' cannot be negated in a generic declaration.",
                span);
            return new BoundVariableTypePattern(span, new VariableSymbol(name));
        }

        if (scope.TryGet(name, out var existing))
        {
            return new BoundVariableTypePattern(span, existing!);
        }

        var variable = new VariableSymbol(name);
        if (underNegation)
        {
            _diagnostics.Add(
                DiagnosticCodes.InvalidVariable,
                DiagnosticSeverity.Error,
                $"Variable '${name}' cannot be introduced under negation.",
                span);
            scope.Hide(name);
            return new BoundVariableTypePattern(span, variable);
        }

        if (scope.IsHidden(name))
        {
            _diagnostics.Add(
                DiagnosticCodes.InvalidVariableScope,
                DiagnosticSeverity.Error,
                $"Variable '${name}' was introduced in an isolated branch and cannot escape it.",
                span);
            return new BoundVariableTypePattern(span, variable);
        }

        if (_referenceOnlyDepth > 0 || _genericDefinitionDepth == 0)
        {
            _diagnostics.Add(
                DiagnosticCodes.InvalidVariable,
                DiagnosticSeverity.Error,
                $"Variable '${name}' must be declared in a type or method generic clause before it is referenced.",
                span);
            return new BoundVariableTypePattern(span, variable);
        }

        scope.Add(name, variable);
        return new BoundVariableTypePattern(span, variable);
    }

    private void ReportDuplicateBranchVariables(
        VariableScope parent,
        VariableScope left,
        VariableScope right,
        SyntaxToken operatorToken)
    {
        var leftNames = left.GetNamesNotVisibleIn(parent);
        var rightNames = right.GetNamesNotVisibleIn(parent);
        leftNames.IntersectWith(rightNames);
        foreach (var name in leftNames)
        {
            _diagnostics.Add(
                DiagnosticCodes.InvalidVariable,
                DiagnosticSeverity.Error,
                $"Variable '${name}' is introduced independently in both OR branches.",
                operatorToken.Span);
        }
    }

    private static bool MatchesAnyParameters(IReadOnlyList<SyntaxNode> parts)
    {
        for (var index = 0; index < parts.Count - 1; index++)
        {
            if (parts[index] is SyntaxToken
                {
                    Kind: SyntaxKind.OpenParenToken
                } &&
                parts[index + 1] is SyntaxToken
                {
                    Kind: SyntaxKind.DotDotToken
                })
            {
                return true;
            }
        }

        return false;
    }


    private static string BuildCanonicalName(
        BoundNameSegment[] segments,
        string[] separators)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < segments.Length; index++)
        {
            if (index > 0) builder.Append(separators[index - 1]);
            builder.Append(segments[index].Pattern);
        }

        return builder.ToString();
    }

    private bool IsUnderNegation => _negationDepth > 0;

    private static bool EqualsKeyword(string actual, string expected)
    {
        return string.Equals(actual, expected, StringComparison.Ordinal);
    }

    private static Accessibility GetAccessibility(string content)
    {
        return content switch
        {
            "public" => Accessibility.Public,
            "protected" => Accessibility.Protected,
            "internal" => Accessibility.Internal,
            "private" => Accessibility.Private,
            "protected internal" =>
                Accessibility.ProtectedInternal,
            "private protected" =>
                Accessibility.PrivateProtected,
            _ => Accessibility.Unknown
        };
    }

    private static bool IsVoidNamedPattern(BoundTypePattern pattern)
    {
        return pattern is BoundNamedTypePattern named &&
               StringComparer.Ordinal.Equals(
                   named.CanonicalName,
                   "System.Void");
    }

    private static BoundAttributeTargetKind GetAttributeTarget(
        string text)
    {
        // 长形式（callable/property/parameter/return）的迁移诊断由解析器报告；
        // 这里保留其目标映射，保证语法节点的语义一致。
        return text switch
        {
            "type" => BoundAttributeTargetKind.Type,
            "call" or "callable" => BoundAttributeTargetKind.Method,
            "prop" or "property" => BoundAttributeTargetKind.Property,
            "para" or "parameter" => BoundAttributeTargetKind.Parameter,
            "ret" or "return" => BoundAttributeTargetKind.Return,
            "exec" => BoundAttributeTargetKind.Execution,
            _ => BoundAttributeTargetKind.Any
        };
    }

    private void ReportInvalidModifiers(ModifierSyntax syntax, string message)
    {
        _diagnostics.Add(
            DiagnosticCodes.InvalidModifierCombination,
            DiagnosticSeverity.Error,
            message,
            syntax.FullSpan);
    }

    // 规范 §12.4：+ 要求命名目标。Standard 以结构白名单验证（三轮审计裁决）：
    // 解括号后的目标必须是命名类型模式（含通配名与显式泛型子句），或单层"命名类型的
    // nullable"（X?+）；其余结构（数组、多层/交错 nullable 包装、通配、tuple、Boolean、
    // 变量、tasklike、被 nullable 分隔的重复 +）一律绑定期拒绝，与求值器支持范围对齐。
    // 只判定目标节点本身——命名目标泛型实参内的数组/通配/tuple 不受影响（List<int[]>+ 合法）。
    private void ReportInvalidAssignableTarget(AssignableTypePatternSyntax syntax)
    {
        var target = UnwrapParenthesized(syntax.TargetType);

        if (target is AssignableTypePatternSyntax)
        {
            _diagnostics.Add(
                DiagnosticCodes.InvalidTypeSuffix,
                DiagnosticSeverity.Error,
                "The '+' suffix cannot be applied more than once.",
                syntax.PlusToken.Span);
            return;
        }

        var effective = target is NullableTypePatternSyntax nullable
            ? UnwrapParenthesized(nullable.UnderlyingType)
            : target;
        if (effective is NamedTypePatternSyntax)
        {
            return;
        }

        _diagnostics.Add(
            DiagnosticCodes.InvalidTypeSuffix,
            DiagnosticSeverity.Error,
            GetInvalidAssignableTargetMessage(effective),
            syntax.PlusToken.Span);
    }

    private static string GetInvalidAssignableTargetMessage(TypePatternSyntax effective)
    {
        return effective switch
        {
            ArrayTypePatternSyntax =>
                "The '+' suffix requires a named type target; array targets are not supported.",
            WildcardTypePatternSyntax =>
                "The '+' suffix requires a named type target; wildcard targets are not supported.",
            NullableTypePatternSyntax =>
                "The '+' suffix requires a named type target; nested nullable targets are not supported.",
            _ => "The '+' suffix requires a named type target."
        };
    }

    private sealed class VariableScope
    {
        private readonly HashSet<string> _hidden;
        private readonly Dictionary<string, VariableSymbol> _visible;

        public VariableScope()
            : this(
                new Dictionary<string, VariableSymbol>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal))
        {
        }

        private VariableScope(
            Dictionary<string, VariableSymbol> visible,
            HashSet<string> hidden)
        {
            _visible = visible;
            _hidden = hidden;
        }

        public VariableScope Clone()
        {
            return new VariableScope(
                new Dictionary<string, VariableSymbol>(
                    _visible,
                    StringComparer.Ordinal),
                new HashSet<string>(_hidden, StringComparer.Ordinal));
        }

        public bool TryGet(string name, out VariableSymbol? variable)
        {
            return _visible.TryGetValue(name, out variable);
        }

        public void Add(string name, VariableSymbol variable)
        {
            _visible.Add(name, variable);
        }

        public bool IsHidden(string name)
        {
            return _hidden.Contains(name);
        }

        public void Hide(string name)
        {
            _hidden.Add(name);
        }

        public void HideNewVariablesFrom(VariableScope child)
        {
            foreach (var name in child._visible.Keys)
            {
                if (!_visible.ContainsKey(name)) _hidden.Add(name);
            }

            _hidden.UnionWith(child._hidden);
        }

        public HashSet<string> GetNamesNotVisibleIn(VariableScope parent)
        {
            return new HashSet<string>(
                _visible.Keys.Where(name => !parent._visible.ContainsKey(name)),
                StringComparer.Ordinal);
        }
    }
}
