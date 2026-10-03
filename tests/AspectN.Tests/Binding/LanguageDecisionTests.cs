using AspectN.Diagnostics;
using AspectN.Syntax;
using AspectN.Text;

namespace AspectN.Tests.Binding;

public sealed class LanguageDecisionTests
{
    [Theory]
    [InlineData("property(* Owner.Value<>)")]
    [InlineData("getter(* Owner.Value<!>)")]
    [InlineData("setter(* Owner.Value<?>)")]
    [InlineData("property(* Owner<int>.Value<$T>)")]
    public void Create_PropertyNameGenericClause_ReportsClauseError(string source)
    {
        var compilation = Compile(source, false);
        var diagnostic = Assert.Single(compilation.Diagnostics,
            diagnostic => diagnostic.Code == DiagnosticCodes.InvalidGenericClause);
        var start = source.LastIndexOf('<');
        Assert.Equal(new TextSpan(start, source.IndexOf('>', start) - start + 1), diagnostic.Span);
        Assert.Contains("property", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(compilation.TryGetMemberPattern(out _));
    }

    [Theory]
    [InlineData("Ns<int>.Owner", true)]
    [InlineData("Ns<?>..Owner", true)]
    [InlineData("Ns<!>.Outer<int>/Inner<string>", true)]
    [InlineData("method(* Ns<$T>.Owner.Run(..))", false)]
    [InlineData("method(Ns<..>.Owner Run(..))", false)]
    [InlineData("attr(exec Ns<>.Marker)", false)]
    public void Create_NamespaceGenericClause_ReportsClauseError(string source, bool typeEntry)
    {
        var compilation = Compile(source, typeEntry);
        var diagnostic = Assert.Single(compilation.Diagnostics,
            diagnostic => diagnostic.Code == DiagnosticCodes.InvalidGenericClause);
        var start = source.IndexOf('<');
        Assert.Equal(new TextSpan(start, source.IndexOf('>', start) - start + 1), diagnostic.Span);
        Assert.Contains("namespace", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(compilation.BoundRoot);
    }

    [Theory]
    [InlineData("Ns.Outer<int>/Inner<string>", true)]
    [InlineData("method(* Ns.Owner<int>.Run<string>(..))", false)]
    [InlineData("property(* Ns.Owner<int>.Value)", false)]
    public void Create_TypeAndMethodGenericClauses_RemainValid(string source, bool typeEntry)
    {
        Assert.Empty(Compile(source, typeEntry).Diagnostics);
    }

    [Theory]
    [InlineData("!!int", true)]
    [InlineData("! !int", true)]
    [InlineData("Box<!!int>", true)]
    [InlineData("!!method(* Run(..))", false)]
    [InlineData("! !method(* Run(..))", false)]
    [InlineData("method(!!int Run(..))", false)]
    [InlineData("method(* Run(! !int))", false)]
    public void Parse_ConsecutiveNegation_ReportsSecondBang(string source, bool typeEntry)
    {
        var compilation = Compile(source, typeEntry);
        var diagnostic = Assert.Single(compilation.Diagnostics,
            diagnostic => diagnostic.Code == DiagnosticCodes.UnexpectedToken &&
                diagnostic.Message.Contains("consecutive", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(new TextSpan(source.IndexOf('!', source.IndexOf('!') + 1), 1), diagnostic.Span);
        Assert.Null(compilation.BoundRoot);
    }

    [Theory]
    [InlineData("tasklike <int>", true)]
    [InlineData("tasklike\t<int>", true)]
    [InlineData("method(tasklike <int> Run(..))", false)]
    public void Parse_TaskLikeClauseSeparatedByWhitespace_ReportsAdjacencyError(string source, bool typeEntry)
    {
        var compilation = Compile(source, typeEntry);
        Assert.Contains(compilation.Diagnostics,
            diagnostic => diagnostic.Code == DiagnosticCodes.UnexpectedToken &&
                diagnostic.Span == new TextSpan(source.IndexOf('<'), 1));
        Assert.Null(compilation.BoundRoot);
    }

    [Theory]
    [InlineData("Box<!int>", true)]
    [InlineData("Pair<!int, string>", true)]
    [InlineData("Box<!int || string>", true)]
    [InlineData("method(* Run<!int>(..))", false)]
    [InlineData("method(Box<!int> Run(..))", false)]
    public void Create_NegatedFirstGenericArgument_IsArgumentPattern(string source, bool typeEntry)
    {
        Assert.Empty(Compile(source, typeEntry).Diagnostics);
    }

    [Theory]
    [InlineData("Box<!>")]
    [InlineData("Box<! >")]
    public void Parse_ExplicitZeroArity_RemainsFixedZero(string source)
    {
        var tree = AspectNParser.ParseTypePattern(source);
        var generic = Assert.IsType<NamedTypePatternSyntax>(tree.Root).Segments[0].GenericArguments;
        Assert.Empty(tree.Diagnostics);
        Assert.Equal(GenericPatternForm.FixedArity, generic!.Form);
        Assert.Equal(0, generic.Arity);
    }

    private static AspectNCompilation Compile(string source, bool typeEntry) =>
        AspectNCompilation.Create(typeEntry
            ? AspectNParser.ParseTypePattern(source)
            : AspectNParser.ParseMemberExpression(source));
}
