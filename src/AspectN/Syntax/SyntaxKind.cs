namespace AspectN.Syntax;

/// <summary>Specifies the syntax kind values.</summary>
public enum SyntaxKind
{
    /// <summary>An unrecognized input token retained for error reporting.</summary>
    BadToken,
    /// <summary>The zero-length token marking the end of the source.</summary>
    EndOfFileToken,
    /// <summary>An identifier or selector keyword.</summary>
    IdentifierToken,
    /// <summary>A quoted string literal.</summary>
    StringToken,
    /// <summary>An integer literal.</summary>
    IntegerToken,
    /// <summary>The raw regular-expression body inside a regex selector.</summary>
    RegexBodyToken,
    /// <summary>The <c>!</c> token.</summary>
    BangToken,
    /// <summary>The <c>&amp;&amp;</c> token.</summary>
    AmpersandAmpersandToken,
    /// <summary>The <c>||</c> token.</summary>
    PipePipeToken,
    /// <summary>The <c>(</c> token.</summary>
    OpenParenToken,
    /// <summary>The <c>)</c> token.</summary>
    CloseParenToken,
    /// <summary>The <c>&lt;</c> token.</summary>
    LessThanToken,
    /// <summary>The <c>&gt;</c> token.</summary>
    GreaterThanToken,
    /// <summary>The <c>[</c> token.</summary>
    OpenBracketToken,
    /// <summary>The <c>]</c> token.</summary>
    CloseBracketToken,
    /// <summary>The <c>,</c> token.</summary>
    CommaToken,
    /// <summary>The <c>.</c> token.</summary>
    DotToken,
    /// <summary>The <c>..</c> token.</summary>
    DotDotToken,
    /// <summary>The <c>/</c> token.</summary>
    SlashToken,
    /// <summary>The <c>*</c> token.</summary>
    StarToken,
    /// <summary>The <c>+</c> token.</summary>
    PlusToken,
    /// <summary>The <c>?</c> token.</summary>
    QuestionToken,
    /// <summary>The <c>$</c> token.</summary>
    DollarToken,
    /// <summary>A negated member expression.</summary>
    UnaryExpression,
    /// <summary>A conjunction or disjunction of member expressions.</summary>
    BinaryExpression,
    /// <summary>A parenthesized member expression.</summary>
    ParenthesizedExpression,
    /// <summary>A callable or execution selector.</summary>
    CallableSelector,
    /// <summary>A method selector.</summary>
    MethodSelector,
    /// <summary>A property selector.</summary>
    PropertySelector,
    /// <summary>A property-getter selector.</summary>
    GetterSelector,
    /// <summary>A property-setter selector.</summary>
    SetterSelector,
    /// <summary>An instance-constructor selector.</summary>
    ConstructorSelector,
    /// <summary>A static-constructor selector.</summary>
    StaticConstructorSelector,
    /// <summary>A direct-attribute selector.</summary>
    AttributeSelector,
    /// <summary>A regular-expression selector.</summary>
    RegexSelector,
    /// <summary>A recovery node for an unrecognized or missing selector.</summary>
    InvalidSelector,
    /// <summary>A callable signature pattern.</summary>
    MethodSignature,
    /// <summary>A property signature pattern.</summary>
    PropertySignature,
    /// <summary>An instance-constructor signature pattern.</summary>
    ConstructorSignature,
    /// <summary>An attribute target and type pattern.</summary>
    AttributeSignature,
    /// <summary>A callable parameter pattern.</summary>
    Parameter,
    /// <summary>A member modifier or accessibility pattern.</summary>
    Modifier,
    /// <summary>A generic arity or argument-pattern clause.</summary>
    GenericPatternList,
    /// <summary>A type pattern together with its ref annotations.</summary>
    TypeUse,
    /// <summary>A name-pattern segment and its optional generic clause.</summary>
    NamePatternSegment,
    /// <summary>A named type pattern.</summary>
    NamedTypePattern,
    /// <summary>A wildcard type pattern.</summary>
    WildcardTypePattern,
    /// <summary>A tuple type pattern.</summary>
    TupleTypePattern,
    /// <summary>A tasklike type pattern.</summary>
    TaskLikeTypePattern,
    /// <summary>A generic-variable type pattern.</summary>
    VariableTypePattern,
    /// <summary>An array type pattern.</summary>
    ArrayTypePattern,
    /// <summary>A nullable type pattern.</summary>
    NullableTypePattern,
    /// <summary>A type pattern with the assignability suffix.</summary>
    AssignableTypePattern,
    /// <summary>A parenthesized type pattern.</summary>
    ParenthesizedTypePattern,
    /// <summary>A negated type pattern.</summary>
    UnaryTypePattern,
    /// <summary>A conjunction or disjunction of type patterns.</summary>
    BinaryTypePattern,
    /// <summary>The single <c>|</c> token used in alternatives.</summary>
    PipeToken,
    /// <summary>A declaring-type path and member-name pattern.</summary>
    MemberPath,
    /// <summary>A name-based member pattern.</summary>
    NamedMemberPattern,
    /// <summary>A negated member-name pattern.</summary>
    UnaryMemberNamePattern,
    /// <summary>A conjunction or disjunction of member-name patterns.</summary>
    BinaryMemberNamePattern,
    /// <summary>A parenthesized member-name pattern.</summary>
    ParenthesizedMemberNamePattern
}
