using AspectN.Binding;
using AspectN.Diagnostics;

namespace AspectN.Tests.Binding;

public sealed class StandardBindingTests
{
    [Theory]
    [InlineData("execution(* Service.Get(..))", "All")]
    [InlineData("callable(* Service.Get(..))", "Callable")]
    public void Create_CallableSelector_BindsExpectedDomain(string source, string domain)
    {
        var compilation = CompileMember(source);
        var selector = Assert.IsType<BoundSelector>(compilation.BoundRoot);
        Assert.Equal(BoundSelectorKind.Callable, selector.SelectorKind);
        Assert.Equal(domain, selector.TargetDomain.ToString());
        Assert.Empty(compilation.Diagnostics);
    }

    [Theory]
    [InlineData("accessor(*)")]
    [InlineData("METHOD()")]
    public void Create_UnknownSelector_ReportsUnknownSelector(string source)
    {
        var compilation = CompileMember(source);
        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.UnknownSelector);
        Assert.Null(compilation.BoundRoot);
    }

    [Theory]
    [InlineData("Name")]
    [InlineData("Example.Name")]
    [InlineData("Name<!>")]
    public void Create_NonGenericName_BindsZeroArity(string source)
    {
        var compilation = CompileType(source);
        var named = Assert.IsType<BoundNamedTypePattern>(compilation.BoundRoot);
        Assert.Equal(BoundGenericArityKind.Exact, named.Segments[^1].Generic.Kind);
        Assert.Equal(0, named.Segments[^1].Generic.Arity);
        Assert.Empty(compilation.Diagnostics);
    }

    [Fact]
    public void Create_OrdinaryIdentifier_IsLiteralTypeName()
    {
        Assert.IsType<BoundNamedTypePattern>(CompileType("T").BoundRoot);
    }

    [Fact]
    public void Create_MemberPathJoinedWithDotDot_ReportsError()
    {
        var compilation = CompileMember("method(* *..StaticMethod(..))");
        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.UnexpectedToken);
        Assert.Null(compilation.BoundRoot);
    }

    [Fact]
    public void Create_QualifiedNamespaceMemberPath_Accepts()
    {
        var compilation = CompileMember("method(* System..Service.Foo(..))");
        Assert.IsType<BoundMethodSignature>(Assert.IsType<BoundSelector>(compilation.BoundRoot).Payload);
        Assert.Empty(compilation.Diagnostics);
    }

    [Theory]
    [InlineData("method(* Service.Get(int,))", 2)]
    [InlineData("method(* Service.Get(,int))", 2)]
    [InlineData("method(* Service.Get(int,,string))", 3)]
    public void Create_ParameterEmptySlots_FillsWildcardParameters(string source, int count)
    {
        var compilation = CompileMember(source);
        var signature = Assert.IsType<BoundMethodSignature>(Assert.IsType<BoundSelector>(compilation.BoundRoot).Payload);
        Assert.Equal(count, signature.Parameters.Count);
        Assert.Empty(compilation.Diagnostics);
    }

    [Theory]
    [InlineData("void<>")]
    [InlineData("void<..>")]
    [InlineData("void<!>")]
    [InlineData("void<?>")]
    [InlineData("System.Void<int>")]
    public void Create_VoidGenericClause_Rejects(string source)
    {
        var compilation = CompileType(source);
        Assert.Contains(compilation.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.InvalidGenericClause);
        Assert.Null(compilation.BoundRoot);
    }

    [Theory]
    [InlineData("method(* Service.Get(..))", "Exact")]
    [InlineData("method(* Service.Get<?>(..))", "Any")]
    [InlineData("method(* Service.*(..))", "Any")]
    public void Create_MethodGenericClause_BindsExpectedArity(string source, string kind)
    {
        var signature = Assert.IsType<BoundMethodSignature>(Assert.IsType<BoundSelector>(CompileMember(source).BoundRoot).Payload);
        Assert.Equal(kind, signature.Generic!.Kind.ToString());
        Assert.Equal(0, signature.Generic.Arity);
    }

    [Fact]
    public void Create_QuestionArityClause_BindsAny()
    {
        var compilation = CompileType("List<?>");
        var named = Assert.IsType<BoundNamedTypePattern>(compilation.BoundRoot);
        Assert.Equal(BoundGenericArityKind.Any, named.Segments[0].Generic.Kind);
        Assert.Empty(compilation.Diagnostics);
    }

    private static AspectNCompilation CompileMember(string source) =>
        AspectNCompilation.Create(AspectNParser.ParseMemberExpression(source));

    private static AspectNCompilation CompileType(string source) =>
        AspectNCompilation.Create(AspectNParser.ParseTypePattern(source));
}
