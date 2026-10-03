using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;

namespace AspectN.Tests.Evaluation;

/// <summary>
/// 锁定 2026-10-02 用户定稿的 `*` 家族语义表与 `*[]`/`*?` 版本差异
/// （docs/language/standard-design-decisions.md），
/// 防止后续改动 BindNamedTypePattern / EvaluateNamed 时回归。
/// </summary>
public sealed class WildcardFamilySemanticsTests
{
    [Theory]
    [InlineData("*", MatchStatus.Matched)]
    [InlineData("*<?>", MatchStatus.Matched)]
    [InlineData("*<!>", MatchStatus.Matched)]
    [InlineData("*<..>", MatchStatus.NotMatched)]
    public void Match_WildcardFamily_PlainNonGenericCandidate(
        string pattern,
        MatchStatus expected)
    {
        AssertMatch(pattern, Named("System", "Int32", TypeKind.Struct), expected);
    }

    [Theory]
    [InlineData("*", MatchStatus.Matched)]
    [InlineData("*<?>", MatchStatus.Matched)]
    [InlineData("*<!>", MatchStatus.NotMatched)]
    [InlineData("*<..>", MatchStatus.Matched)]
    public void Match_WildcardFamily_ConstructedGenericCandidate(
        string pattern,
        MatchStatus expected)
    {
        AssertMatch(pattern, Generic("System.Collections.Generic", "List", 1, Named("System", "Int32", TypeKind.Struct)), expected);
    }

    [Fact]
    public void Match_WildcardFamily_OpenGenericDefinition()
    {
        var openList = Generic("System.Collections.Generic", "List", 1);

        AssertMatch("*", openList, MatchStatus.Matched);
        AssertMatch("*<?>", openList, MatchStatus.Matched);
        AssertMatch("*<!>", openList, MatchStatus.NotMatched);
        AssertMatch("*<..>", openList, MatchStatus.Matched);
    }

    [Fact]
    public void Match_WildcardFamily_NullableCandidate()
    {
        var nullable = Generic(
            "System",
            "Nullable",
            1,
            Named("System", "Int32", TypeKind.Struct));

        AssertMatch("*", nullable, MatchStatus.Matched);
        AssertMatch("*<?>", nullable, MatchStatus.Matched);
        AssertMatch("*<!>", nullable, MatchStatus.NotMatched);
        AssertMatch("*<..>", nullable, MatchStatus.Matched);
    }

    [Fact]
    public void Match_WildcardFamily_TupleCandidates()
    {
        var tuple = Generic(
            "System",
            "ValueTuple",
            2,
            Named("System", "Int32", TypeKind.Struct),
            Named("System", "String"));
        var bareTuple = Named("System", "ValueTuple");

        // ValueTuple 是普通命名泛型：按 CLR 元数参与匹配（§6.5 审计修正）。
        AssertMatch("*", tuple, MatchStatus.Matched);
        AssertMatch("*<?>", tuple, MatchStatus.Matched);
        AssertMatch("*<!>", tuple, MatchStatus.NotMatched);
        AssertMatch("*<..>", tuple, MatchStatus.Matched);
        AssertMatch("*", bareTuple, MatchStatus.Matched);
        AssertMatch("*<?>", bareTuple, MatchStatus.Matched);
        AssertMatch("*<!>", bareTuple, MatchStatus.Matched);
        AssertMatch("*<..>", bareTuple, MatchStatus.NotMatched);
    }

    [Theory]
    [InlineData("*", MatchStatus.Matched)]
    [InlineData("*<?>", MatchStatus.NotMatched)]
    [InlineData("*<!>", MatchStatus.NotMatched)]
    [InlineData("*<..>", MatchStatus.NotMatched)]
    public void Match_WildcardFamily_ArrayCandidate(
        string pattern,
        MatchStatus expected)
    {
        AssertMatch(pattern, new ArrayTypeSignature(Named("System", "Int32", TypeKind.Struct), 1), expected);
    }

    [Fact]
    public void Match_WildcardFamily_GenericParameterCandidate()
    {
        var parameter = new GenericParameterTypeSignature(
            new GenericParameterSignature(
                "T",
                GenericParameterOwnerKind.Method,
                "Example.Service.Map`1",
                new AssemblyIdentity("Example.Assembly"),
                0));

        // 泛型形参候选不是命名类型：只有全通配 * 命中。
        AssertMatch("*", parameter, MatchStatus.Matched);
        AssertMatch("*<?>", parameter, MatchStatus.NotMatched);
        AssertMatch("*<!>", parameter, MatchStatus.NotMatched);
        AssertMatch("*<..>", parameter, MatchStatus.NotMatched);
    }

    [Fact]
    public void Match_WildcardFamily_NestedCandidate()
    {
        var nested = new NamedTypeSignature(
            Identity("Example", "Inner", containingType: Identity("Example", "Outer")));

        // 单段命名通配不跨嵌套；裸 * 是全通配（§5.5）。
        AssertMatch("*", nested, MatchStatus.Matched);
        AssertMatch("*<?>", nested, MatchStatus.NotMatched);
        AssertMatch("*<!>", nested, MatchStatus.NotMatched);
        AssertMatch("*<..>", nested, MatchStatus.NotMatched);
    }

    [Theory]
    [InlineData("System.Int32[]", MatchStatus.Matched)]
    [InlineData("System.Collections.Generic.List<System.Int32>[]", MatchStatus.Matched)]
    [InlineData("System.Int32[][]", MatchStatus.Matched)]
    public void Match_SuffixedWildcardArray_MatchesAnyElement(
        string candidateTypeName,
        MatchStatus expected)
    {
        var candidate = BuildCandidate(candidateTypeName);
        AssertMatch("*[]", candidate, expected);
    }

    [Fact]
    public void Match_SuffixedWildcardNullable_MatchesGenericValueArgument()
    {
        var nullableOfGeneric = Generic(
            "System",
            "Nullable",
            1,
            Generic("Example", "GenericStruct", 1, Named("System", "Int32", TypeKind.Struct)));

        AssertMatch(
            "*?",
            nullableOfGeneric,
            MatchStatus.Matched);
    }

    private static void AssertMatch(
        string source,
        TypeSignature candidate,
        MatchStatus expected)
    {
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseTypePattern(source));
        Assert.True(compilation.TryGetTypePattern(out var pattern));

        Assert.Equal(expected, pattern!.Match(candidate, new StubTypeSystem()).Status);
    }

    private static TypeSignature BuildCandidate(string typeName)
    {
        var rank = 0;
        while (typeName.EndsWith("[]", StringComparison.Ordinal))
        {
            rank++;
            typeName = typeName.Substring(0, typeName.Length - 2);
        }

        var int32 = Named("System", "Int32", TypeKind.Struct);
        TypeSignature candidate = typeName switch
        {
            "System.Int32" => int32,
            "System.Collections.Generic.List<System.Int32>" =>
                Generic("System.Collections.Generic", "List", 1, int32),
            _ => throw new ArgumentException(
                $"No candidate mapping for {typeName}.",
                nameof(typeName))
        };

        for (var index = 0; index < rank; index++)
        {
            candidate = new ArrayTypeSignature(candidate, 1);
        }

        return candidate;
    }

    private static NamedTypeSignature Named(
        string @namespace,
        string name,
        TypeKind kind = TypeKind.Class)
    {
        return new NamedTypeSignature(Identity(@namespace, name, kind: kind));
    }

    private static NamedTypeSignature Generic(
        string @namespace,
        string name,
        int arity,
        params TypeSignature[] arguments)
    {
        var identity = Identity(@namespace, name, arity);
        return new NamedTypeSignature(
            identity,
            arguments,
            new NamedTypeSignature(identity));
    }

    private static TypeIdentity Identity(
        string @namespace,
        string name,
        int genericArity = 0,
        TypeIdentity? containingType = null,
        TypeKind kind = TypeKind.Class)
    {
        return new TypeIdentity(
            new AssemblyIdentity("Example.Assembly"),
            @namespace,
            name,
            genericArity,
            kind,
            containingType);
    }
}
