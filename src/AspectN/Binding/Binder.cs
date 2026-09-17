using System.Collections.ObjectModel;
using System.Text;
using AspectN.Diagnostics;
using AspectN.Binding.Legacy;
using AspectN.Extensibility;
using AspectN.Metadata;
using AspectN.Parsing;
using AspectN.Syntax;

namespace AspectN.Binding;

internal sealed class Binder(
    AspectNSyntaxTree syntaxTree,
    AspectNExtensionRegistry extensions)
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
    private readonly AspectNExtensionRegistry _extensions = extensions ??
            throw new ArgumentNullException(nameof(extensions));
    private readonly AspectNSyntaxTree _syntaxTree = syntaxTree ??
            throw new ArgumentNullException(nameof(syntaxTree));
    private readonly bool _isLegacy = syntaxTree.LanguageVersion == AspectNLanguageVersion.Legacy;
    private int _negationDepth;

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
        if (IsUnavailableSelector(syntax))
        {
            return InvalidSelector(syntax);
        }

        return syntax switch
        {
            CallableSelectorSyntax callable => new BoundSelector(
                callable.FullSpan,
                BoundSelectorKind.Callable,
                _isLegacy
                    ? SelectorTargetDomain.All
                    : SelectorTargetDomain.Callable,
                BindCallableSignature(
                    (CallableSignatureSyntax)callable.Body!,
                    scope)),
            MethodSelectorSyntax method => new BoundSelector(
                method.FullSpan,
                BoundSelectorKind.Method,
                SelectorTargetDomain.OrdinaryMethod,
                BindCallableSignature(
                    (CallableSignatureSyntax)method.Body!,
                    scope)),
            AccessorSelectorSyntax accessor => new BoundSelector(
                accessor.FullSpan,
                BoundSelectorKind.Accessor,
                SelectorTargetDomain.PropertyAccessor,
                BindPropertySignature(
                    (PropertySignatureSyntax)accessor.Body!,
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
            ExtensionSelectorSyntax extension => BindExtension(extension),
            _ => throw new InvalidOperationException(
                $"Unsupported selector syntax '{syntax.Kind}'.")
        };
    }

    private BoundSelector BindExtension(ExtensionSelectorSyntax syntax)
    {
        var qualifiedName = GetExtensionName(syntax);
        if (!_extensions.TryGet(qualifiedName, out var extension))
        {
            var isQualified = syntax.NameTokens.Count > 1;
            _diagnostics.Add(
                isQualified
                    ? DiagnosticCodes.MissingExtension
                    : DiagnosticCodes.UnknownSelector,
                DiagnosticSeverity.Error,
                isQualified
                    ? $"Extension '{qualifiedName}' is not registered."
                    : $"Selector '{qualifiedName}' is not recognized.",
                syntax.FullSpan);
            return InvalidSelector(syntax);
        }

        var targetDomain = (SelectorTargetDomain)(int)extension!.TargetDomain;
        if (targetDomain == SelectorTargetDomain.None)
        {
            _diagnostics.Add(
                DiagnosticCodes.InvalidTargetDomain,
                DiagnosticSeverity.Error,
                $"Extension '{qualifiedName}' does not declare a target domain.",
                syntax.FullSpan);
            return InvalidSelector(syntax);
        }

        var compileContext = new ExtensionCompileContext(
            qualifiedName,
            _syntaxTree.LanguageVersion,
            syntax.Arguments.Select(ExtensionArgument.FromSyntax),
            syntax.FullSpan);
        var matcher = extension.Compile(compileContext) ??
            throw new InvalidOperationException(
                $"Extension '{qualifiedName}' returned a null matcher.");
        return new BoundSelector(
            syntax.FullSpan,
            BoundSelectorKind.Extension,
            targetDomain,
            new BoundExtensionPattern(
                syntax.FullSpan,
                qualifiedName,
                extension.RequiredCapabilities,
                matcher));
    }

    private BoundSelector BindAttributeSelector(
        AttributeSelectorSyntax syntax,
        VariableScope scope)
    {
        var signature = (AttributeSignatureSyntax)syntax.Body!;
        var tokens = signature.Parts.OfType<SyntaxToken>().ToArray();
        var typeSyntax = signature.Parts.OfType<TypePatternSyntax>().Last();
        var targetText = tokens.Length == 0 ? string.Empty : tokens[0].Text;
        var target = _isLegacy
            ? LegacyBinder.GetAttributeTarget(targetText)
            : GetStandardAttributeTarget(targetText);
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

    private BoundCallableSignature BindCallableSignature(
        CallableSignatureSyntax syntax,
        VariableScope scope)
    {
        var modifiers = BindModifiers(syntax.Parts.OfType<ModifierSyntax>());
        var returnSyntax = syntax.Parts.OfType<TypeUseSyntax>().First();
        var member = BindMemberPattern(
            syntax.Parts,
            returnSyntax,
            scope,
            out var genericSyntax);
        var generic = BindGenericPattern(
            genericSyntax,
            member.NamePattern == "*",
            scope,
            IsUnderNegation,
            declaresLegacyVariables: true);
        var returnType = BindTypeUse(returnSyntax, scope, true);
        var parameters = syntax.Parts
            .OfType<ParameterSyntax>()
            .Select(parameter =>
                BindTypeUse(parameter.TypeUse, scope, false))
            .ToArray();
        var requiresAsync = syntax.Parts
            .TakeWhile(part => !ReferenceEquals(part, returnSyntax))
            .OfType<SyntaxToken>()
            .Any(token => EqualsKeyword(token.Text, "async"));
        return new BoundCallableSignature(
            syntax.FullSpan,
            modifiers,
            requiresAsync,
            returnType,
            member,
            generic,
            parameters,
            MatchesAnyParameters(syntax.Parts));
    }

    private BoundPropertySignature BindPropertySignature(
        PropertySignatureSyntax syntax,
        VariableScope scope)
    {
        var typeSyntax = syntax.Parts.OfType<TypeUseSyntax>().First();
        var member = BindMemberPattern(
            syntax.Parts,
            typeSyntax,
            scope,
            out _);
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
        var declaringTypePattern = BindTypePattern(
            declaringType,
            scope,
            IsUnderNegation);
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
        var refKind = refText.ToLowerInvariant() switch
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

        return new BoundTypeUsePattern(
            refKind,
            BindTypePattern(syntax.TypePattern, scope, IsUnderNegation));
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
            var content = string.Join(
                " ",
                syntax.Tokens
                    .Where(token => token.Kind != SyntaxKind.BangToken)
                    .Select(token => token.Text));
            if (_isLegacy)
            {
                content = LegacyBinder.NormalizeModifier(content);
            }

            BoundModifier modifier;
            if (content == "static")
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

            var key = $"{modifier.Kind}:{modifier.Accessibility}:{isNegated}";
            if (!keys.Add(key) &&
                _syntaxTree.LanguageVersion == AspectNLanguageVersion.Standard)
            {
                ReportInvalidModifiers(
                    syntax,
                    $"Modifier '{content}' is duplicated.");
            }

            modifiers.Add(modifier);
        }

        if (_syntaxTree.LanguageVersion == AspectNLanguageVersion.Standard)
        {
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
        }

        return modifiers;
    }

    private BoundTypePattern BindTypePattern(
        TypePatternSyntax syntax,
        VariableScope scope,
        bool underNegation)
    {
        switch (syntax)
        {
            case NamedTypePatternSyntax named:
                if (TryGetLegacyImplicitVariableName(
                        named,
                        scope,
                        declaration: false,
                        out var variableName))
                {
                    return BindVariable(
                        named.FullSpan,
                        variableName!,
                        scope,
                        underNegation);
                }

                return BindNamedTypePattern(
                    named,
                    scope,
                    underNegation,
                    declaresLegacyVariables: false);
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
                    BindTypePattern(
                        nullable.UnderlyingType,
                        scope,
                        underNegation));
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
                return new BoundTaskLikeTypePattern(
                    taskLike.FullSpan,
                    BindTypePattern(
                        taskLike.ResultType,
                        scope,
                        underNegation));
            case ParenthesizedTypePatternSyntax parenthesized:
                return BindTypePattern(
                    parenthesized.Pattern,
                    scope,
                    underNegation);
            case UnaryTypePatternSyntax unary:
                {
                    var childScope = scope.Clone();
                    var operand = BindTypePattern(unary.Operand, childScope, true);
                    scope.HideNewVariablesFrom(childScope);
                    return new BoundUnaryTypePattern(unary.FullSpan, operand);
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

        BoundTypePattern bound = BindTypePattern(
            elementType,
            scope,
            underNegation);
        foreach (var suffix in suffixes)
        {
            bound = new BoundArrayTypePattern(
                suffix.FullSpan,
                bound,
                suffix.Rank);
        }

        return (BoundArrayTypePattern)bound;
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
        bool underNegation,
        bool declaresLegacyVariables)
    {
        var segments = syntax.Segments.Select(segment =>
        {
            var pattern = string.Concat(
                segment.NameParts.Select(token => token.Text));
            return new BoundNameSegment(
                pattern,
                BindGenericPattern(
                    segment.GenericArguments,
                    pattern == "*",
                    scope,
                    underNegation,
                    declaresLegacyVariables));
        }).ToArray();
        var separators = syntax.Separators.Select(token => token.Text).ToArray();
        var sourceName = BuildCanonicalName(segments, separators);
        var canonicalName = _PrimitiveAliases.TryGetValue(
            sourceName,
            out var alias)
            ? alias
            : sourceName;
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
        bool underNegation = false,
        bool declaresLegacyVariables = false)
    {
        if (syntax is null)
        {
            if (_isLegacy)
            {
                return LegacyBinder.CreateOmittedGenericPattern();
            }

            return new BoundGenericPattern(
                nameIsWildcard
                    ? BoundGenericArityKind.Any
                    : BoundGenericArityKind.Exact,
                0);
        }

        var arguments = syntax.Arguments.Select(argument =>
            BindGenericArgument(
                argument,
                scope,
                underNegation,
                declaresLegacyVariables));
        return syntax.Form switch
        {
            GenericPatternForm.AnyPositiveArity => new BoundGenericPattern(
                BoundGenericArityKind.AnyPositive,
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

    private BoundTypePattern BindGenericArgument(
        TypePatternSyntax syntax,
        VariableScope scope,
        bool underNegation,
        bool declaresLegacyVariables)
    {
        if (declaresLegacyVariables &&
            syntax is NamedTypePatternSyntax named &&
            TryGetLegacyImplicitVariableName(
                named,
                scope,
                declaration: true,
                out var variableName))
        {
            return BindVariable(
                named.FullSpan,
                variableName!,
                scope,
                underNegation);
        }

        return BindTypePattern(syntax, scope, underNegation);
    }

    private BoundVariableTypePattern BindVariable(
        AspectN.Text.TextSpan span,
        string name,
        VariableScope scope,
        bool underNegation)
    {
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

    private bool IsUnavailableSelector(SelectorSyntax syntax)
    {
        var keyword = syntax.KeywordToken?.Text;
        if (keyword is null) return false;

        if (!_isLegacy)
        {
            return keyword is "execution" or "property";
        }

        var unavailable = syntax switch
        {
            CallableSelectorSyntax => LegacyBinder.EqualsKeyword(keyword, "callable"),
            AccessorSelectorSyntax => LegacyBinder.EqualsKeyword(keyword, "accessor"),
            _ => false
        };
        if (unavailable)
        {
            _diagnostics.Add(
                DiagnosticCodes.SelectorUnavailable,
                DiagnosticSeverity.Error,
                $"Selector '{keyword}' is unavailable in Legacy.",
                syntax.KeywordToken!.Span);
        }

        return unavailable;
    }

    private static BoundSelector InvalidSelector(SelectorSyntax syntax)
    {
        return new BoundSelector(
            syntax.FullSpan,
            BoundSelectorKind.Regex,
            SelectorTargetDomain.All,
            new BoundRegexPattern(syntax.FullSpan, string.Empty));
    }

    private BoundMemberPattern BindMemberPattern(
        IReadOnlyList<SyntaxNode> parts,
        TypeUseSyntax typeUse,
        VariableScope scope,
        out GenericPatternListSyntax? callableGeneric)
    {
        var path = new List<SyntaxNode>();
        var afterType = false;
        foreach (var part in parts)
        {
            if (ReferenceEquals(part, typeUse))
            {
                afterType = true;
                continue;
            }

            if (!afterType) continue;
            if (part is SyntaxToken token &&
                token.Kind == SyntaxKind.OpenParenToken)
            {
                break;
            }

            path.Add(part);
        }

        var memberSeparator = -1;
        for (var index = path.Count - 1; index >= 0; index--)
        {
            if (path[index] is SyntaxToken { Kind: SyntaxKind.DotToken })
            {
                memberSeparator = index;
                break;
            }
        }

        var memberNodes = path.Skip(memberSeparator + 1).ToArray();
        callableGeneric = memberNodes
            .OfType<GenericPatternListSyntax>()
            .LastOrDefault();
        var memberName = string.Concat(
            memberNodes.OfType<SyntaxToken>().Select(token => token.Text));
        var declaringNodes = path.Take(memberSeparator).ToArray();
        var assignableSuffix = declaringNodes.LastOrDefault() as SyntaxToken;
        var isAssignable = assignableSuffix?.Kind == SyntaxKind.PlusToken;
        foreach (var plusToken in path
                     .OfType<SyntaxToken>()
                     .Where(token => token.Kind == SyntaxKind.PlusToken))
        {
            if (isAssignable && ReferenceEquals(plusToken, assignableSuffix))
            {
                continue;
            }

            _diagnostics.Add(
                DiagnosticCodes.UnexpectedToken,
                DiagnosticSeverity.Error,
                "Assignable suffix '+' is only valid on a declaring type.",
                plusToken.Span);
        }
        if (memberSeparator < 0)
        {
            return new BoundMemberPattern(null, memberName);
        }

        var declaringSyntax = CreateNamedTypeSyntax(
            isAssignable
                ? declaringNodes.Take(declaringNodes.Length - 1)
                : declaringNodes);
        BoundTypePattern declaringType = BindNamedTypePattern(
            declaringSyntax,
            scope,
            IsUnderNegation,
            declaresLegacyVariables: _isLegacy);
        if (isAssignable)
        {
            var assignableSyntax = new AssignableTypePatternSyntax(
                declaringSyntax,
                assignableSuffix!);
            declaringType = new BoundAssignableTypePattern(
                assignableSyntax.FullSpan,
                declaringType);
        }

        return new BoundMemberPattern(declaringType, memberName);
    }

    private static NamedTypePatternSyntax CreateNamedTypeSyntax(
        IEnumerable<SyntaxNode> nodes)
    {
        var segments = new List<NamePatternSegmentSyntax>();
        var separators = new List<SyntaxToken>();
        var nameParts = new List<SyntaxToken>();
        GenericPatternListSyntax? generic = null;

        foreach (var node in nodes)
        {
            if (node is GenericPatternListSyntax genericList)
            {
                generic = genericList;
                continue;
            }

            var token = (SyntaxToken)node;
            if (token.Kind is SyntaxKind.DotToken or
                SyntaxKind.DotDotToken or
                SyntaxKind.SlashToken)
            {
                segments.Add(new NamePatternSegmentSyntax(nameParts, generic));
                nameParts.Clear();
                generic = null;
                separators.Add(token);
                continue;
            }

            nameParts.Add(token);
        }

        segments.Add(new NamePatternSegmentSyntax(nameParts, generic));
        return new NamedTypePatternSyntax(segments, separators);
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

    private bool TryGetLegacyImplicitVariableName(
        NamedTypePatternSyntax syntax,
        VariableScope scope,
        bool declaration,
        out string? name)
    {
        if (!_isLegacy)
        {
            name = null;
            return false;
        }

        return LegacyBinder.TryGetImplicitVariableName(
            syntax,
            declaration,
            variableName => scope.TryGet(variableName, out _),
            out name);
    }

    private bool IsUnderNegation => _negationDepth > 0;

    private bool EqualsKeyword(string actual, string expected)
    {
        return _isLegacy
            ? LegacyBinder.EqualsKeyword(actual, expected)
            : string.Equals(actual, expected, StringComparison.Ordinal);
    }

    private static Accessibility GetAccessibility(string content)
    {
        return content switch
        {
            "public" => Accessibility.Public,
            "protected" => Accessibility.Protected,
            "internal" => Accessibility.Internal,
            "private" => Accessibility.Private,
            "protected internal" or "protectedinternal" =>
                Accessibility.ProtectedInternal,
            "private protected" or "privateprotected" =>
                Accessibility.PrivateProtected,
            _ => Accessibility.Unknown
        };
    }

    private static BoundAttributeTargetKind GetStandardAttributeTarget(
        string text)
    {
        return text switch
        {
            "type" => BoundAttributeTargetKind.Type,
            "callable" => BoundAttributeTargetKind.Callable,
            "property" => BoundAttributeTargetKind.Property,
            "parameter" => BoundAttributeTargetKind.Parameter,
            "return" => BoundAttributeTargetKind.Return,
            _ => BoundAttributeTargetKind.Any
        };
    }

    private static string GetExtensionName(ExtensionSelectorSyntax syntax)
    {
        return string.Concat(syntax.NameTokens.Select(token => token.Text));
    }

    private void ReportInvalidModifiers(ModifierSyntax syntax, string message)
    {
        _diagnostics.Add(
            DiagnosticCodes.InvalidModifierCombination,
            DiagnosticSeverity.Error,
            message,
            syntax.FullSpan);
    }

    // 规范 §12.1/§12.4：绑定器校验后缀适用性；+ 要求命名目标，数组目标不支持，也不能重复叠加。
    // nullable 目标（int?+）是合法组合，不在此报告。
    private void ReportInvalidAssignableTarget(AssignableTypePatternSyntax syntax)
    {
        var target = syntax.TargetType;
        while (target is ParenthesizedTypePatternSyntax parenthesized)
        {
            target = parenthesized.Pattern;
        }

        if (target is ArrayTypePatternSyntax)
        {
            _diagnostics.Add(
                DiagnosticCodes.InvalidTypeSuffix,
                DiagnosticSeverity.Error,
                "The '+' suffix requires a named type target; array targets are not supported.",
                syntax.PlusToken.Span);
        }
        else if (target is AssignableTypePatternSyntax)
        {
            _diagnostics.Add(
                DiagnosticCodes.InvalidTypeSuffix,
                DiagnosticSeverity.Error,
                "The '+' suffix cannot be applied more than once.",
                syntax.PlusToken.Span);
        }
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
