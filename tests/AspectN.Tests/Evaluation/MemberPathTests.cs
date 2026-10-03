using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;

namespace AspectN.Tests.Evaluation;

public sealed class MemberPathTests
{
    [Theory]
    [InlineData("method(* !Run(..))", "Service", "Run", 0, MatchStatus.NotMatched)]
    [InlineData("method(* !Run(..))", "Service", "Run", 1, MatchStatus.Matched)]
    [InlineData("method(* !Run<?>(..))", "Service", "Run", 1, MatchStatus.NotMatched)]
    [InlineData("method(* !Service.Run(..))", "Other", "Run", 0, MatchStatus.Matched)]
    [InlineData("method(* !Service.Run(..))", "Service", "Run", 0, MatchStatus.NotMatched)]
    [InlineData("method(* !*(..))", "Service", "Run", 1, MatchStatus.NotMatched)]
    [InlineData("method(* (Service || Other).Run(..))", "Other", "Run", 0, MatchStatus.Matched)]
    [InlineData("method(* (Service && !Other).Run(..))", "Service", "Run", 0, MatchStatus.Matched)]
    [InlineData("method(* Service.(Run || Stop)(..))", "Service", "Stop", 0, MatchStatus.Matched)]
    [InlineData("method(* Service.(Run || Stop)(..))", "Other", "Stop", 0, MatchStatus.NotMatched)]
    [InlineData("method(* Service.(Run<?> && !Run<!>)(..))", "Service", "Run", 1, MatchStatus.Matched)]
    [InlineData("method(* Service.(Run<?> && !Run<!>)(..))", "Service", "Run", 0, MatchStatus.NotMatched)]
    [InlineData("method(* !(Service || Other).(Run || Stop)(..))", "Service", "Stop", 0, MatchStatus.NotMatched)]
    [InlineData("method(* !(Service || Other).(Run || Stop)(..))", "Third", "Stop", 0, MatchStatus.Matched)]
    public void Match_PathBooleanAndNegation_UsesJointNameAndArity(
        string source, string owner, string name, int arity, MatchStatus expected)
    {
        Assert.Equal(expected, Match(source, Candidate(owner, name, arity)).Status);
    }

    [Theory]
    [InlineData("method(int !Run(..))", MatchStatus.NotMatched)]
    [InlineData("method(* !Run(int))", MatchStatus.NotMatched)]
    public void Match_PathNegation_DoesNotNegateReturnOrParameters(string source, MatchStatus expected)
    {
        Assert.Equal(expected, Match(source, Candidate("Service", "Stop")).Status);
    }

    [Fact]
    public void Match_DeclaringArrayAndNullablePatterns_UseTypePatternSemantics()
    {
        var array = new ArrayTypeSignature(Named("System", "Int32", TypeKind.Struct), 2);
        var nullable = new NamedTypeSignature(
            new TypeIdentity(new AssemblyIdentity("Test"), "System", "Nullable", 1, TypeKind.Struct),
            [Named("System", "Int32", TypeKind.Struct)]);
        Assert.Equal(MatchStatus.Matched, Match("method(* int[,].Get(..))", Candidate("", "Get", declaringType: array)).Status);
        Assert.Equal(MatchStatus.Matched, Match("method(* *.Get(..))", Candidate("", "Get", declaringType: array)).Status);
        Assert.Equal(MatchStatus.Matched, Match("method(* int?.GetValueOrDefault(..))", Candidate("", "GetValueOrDefault", declaringType: nullable)).Status);
    }

    [Fact]
    public void Match_NegatedUnknownDeclaringType_PropagatesUnknown()
    {
        var candidate = Candidate("", "Run", declaringType: new UnsupportedTypeSignature("missing", "Unavailable"));
        Assert.Equal(MatchStatus.Unknown, Match("method(* !Service.Run(..))", candidate).Status);
        Assert.Equal(MatchStatus.NotMatched, Match("method(* !Service.Run(int))", candidate).Status);
    }

    [Fact]
    public void Match_NameBranches_KeepExistingVariableBindings()
    {
        var candidate = Candidate("Service", "Run", 1);
        Assert.Equal(MatchStatus.Matched,
            Match("method(* Run<$T>(..)) && method(* Service.(Run<$T> || Stop<$T>)(..))", candidate).Status);
        Assert.Equal(MatchStatus.Matched,
            Match("method(* Run<$T>(..)) && method(* !Service.Stop<$T>(..)) && method(* Run<$T>(..))", candidate).Status);
    }

    [Theory]
    [InlineData("method(* Service.(Run<int> || Run<string>)(..))", "Int32", MatchStatus.Matched)]
    [InlineData("method(* Service.(Run<int> || Run<string>)(..))", "String", MatchStatus.Matched)]
    [InlineData("method(* Service.(Run<int> || Run<string>)(..))", "Boolean", MatchStatus.NotMatched)]
    [InlineData("method(* Run<!int>(..))", "String", MatchStatus.Matched)]
    [InlineData("method(* Run<!int>(..))", "Int32", MatchStatus.NotMatched)]
    public void Match_NameGenericArgumentPatterns_KeepBranchAndNegationConstraints(string source, string argument, MatchStatus expected)
    {
        var candidate = Candidate("Service", "Run", typeArguments: [Named("System", argument)]);
        Assert.Equal(expected, Match(source, candidate).Status);
    }

    [Theory]
    [InlineData("method(* Service.(Run<int> || Run<?>)(..))", MatchStatus.Matched)]
    [InlineData("method(* Service.(Run<int> && Stop<?>)(..))", MatchStatus.NotMatched)]
    [InlineData("method(* Service.!Run<int>(..))", MatchStatus.Unknown)]
    public void Match_NameBooleanWithUnknownGenericArgument_UsesThreeValuedLogic(string source, MatchStatus expected)
    {
        var candidate = Candidate("Service", "Run", typeArguments: [new UnsupportedTypeSignature("missing", "Unavailable")]);
        Assert.Equal(expected, Match(source, candidate).Status);
    }

    [Fact]
    public void Match_NameBoolean_WhenCancelled_ThrowsCancellation()
    {
        var compilation = Compile("method(* Service.(Run<int> || Run<?>)(..))");
        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            pattern!.Match(Candidate("Service", "Run", 1), new StubTypeSystem(), cancellation.Token));
    }

    [Theory]
    [InlineData("method(* Ns.Outer<(int || string)>/Inner<Box<!int>>.(Run<List<Ns.T>> || Stop<?>)(..))")]
    [InlineData("method(* tasklike<int>.Run(..))")]
    [InlineData("method(* (tasklike || Other).Run(..))")]
    public void Create_ComplexDeclaringPatterns_FindOnlyTheMemberSeparator(string source)
    {
        Assert.Empty(Compile(source).Diagnostics);
    }

    [Theory]
    [InlineData("method(* !Service.Run<$T>(..))", "AN2006")]
    [InlineData("method(* Service.(Run<$T> || Stop)(..)) && method(* Run<$T>(..))", "AN2007")]
    [InlineData("method(* (Repo<$T> || Other).Run($T))", "AN2007")]
    public void Create_VariableFirstIntroducedInNegativeOrOrBranch_ReportsScopeError(string source, string code)
    {
        var compilation = Compile(source);
        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == code);
        Assert.False(compilation.TryGetMemberPattern(out _));
    }

    [Theory]
    [InlineData("method(* !!Run(..))")]
    [InlineData("method(* Service.(!!Run)(..))")]
    [InlineData("method(* Service.(Run ||)(..))")]
    [InlineData("method(* (Service ||).Run(..))")]
    [InlineData("getter(* Service.(Value<> || Other))")]
    [InlineData("method(* Service .Run(..))")]
    [InlineData("method(* Service. (Run || Stop)(..))")]
    public void Create_InvalidMemberPath_ReportsErrorWithoutProducingMatcher(string source)
    {
        Assert.False(Compile(source).TryGetMemberPattern(out _));
    }

    private static AspectNCompilation Compile(string source) =>
        AspectNCompilation.Create(AspectNParser.ParseMemberExpression(source));

    private static MatchResult Match(string source, MethodSignature candidate)
    {
        var compilation = Compile(source);
        Assert.True(compilation.TryGetMemberPattern(out var pattern), string.Join("; ", compilation.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return pattern!.Match(candidate, new StubTypeSystem());
    }

    private static MethodSignature Candidate(string owner, string name, int arity = 0, TypeSignature? declaringType = null,
        IReadOnlyList<TypeSignature>? typeArguments = null) =>
        new(name, MethodKind.OrdinaryMethod, Accessibility.Public, false, false,
            declaringType ?? Named("Example", owner),
            new TypeUseSignature(Named("System", "Void", TypeKind.Struct), RefKind.None, TypeUseKind.Return),
            [], [], typeArguments ?? Enumerable.Repeat<TypeSignature>(Named("System", "String"), arity).ToArray(), [], []);

    private static NamedTypeSignature Named(string ns, string name, TypeKind kind = TypeKind.Class) =>
        new(new TypeIdentity(new AssemblyIdentity("Test"), ns, name, 0, kind));
}
