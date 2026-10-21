using AspectN.Binding;
using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;
using AspectN.TypeSystem;

namespace AspectN.Tests.Evaluation;

public sealed class BindingAlternativesTests
{
    [Fact]
    public void ReplaceWithAlternativesFrom_EquivalentBindings_CoalescesBranches()
    {
        var bindings = new BindingEnvironment(new BindingBudget(new AspectNCompilationOptions(null, GenericVariableMode.Capture), default));
        var variable = new VariableSymbol("T");
        var candidate = Named("String");
        bindings.Bind(variable, candidate, new StubTypeSystem(), static _ => { });

        bindings.ReplaceWithAlternativesFrom([bindings.Clone(), bindings.Clone()]);

        Assert.Single(bindings.CloneAlternatives());
    }

    [Fact]
    public void ReplaceWithAlternativesFrom_DifferentCertainty_PreservesBothBranches()
    {
        var bindings = new BindingEnvironment(new BindingBudget(new AspectNCompilationOptions(null, GenericVariableMode.Capture), default));
        var unknown = bindings.Clone();
        unknown.SetUnknown(true);

        bindings.ReplaceWithAlternativesFrom([bindings.Clone(), unknown]);

        Assert.Equal(2, bindings.CloneAlternatives().Count());
    }

    [Fact]
    public void Match_RepeatedEquivalentOr_AllocationsStayBounded()
    {
        var source = string.Join(" && ", Enumerable.Repeat("(* || *)", 18));
        var pattern = Compile(source);
        var candidate = Named("String");
        var typeSystem = new StubTypeSystem();
        pattern.Match(candidate, typeSystem);
        var before = GC.GetAllocatedBytesForCurrentThread();

        var result = pattern.Match(candidate, typeSystem);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(MatchStatus.Matched, result.Status);
        Assert.True(allocated < 5 * 1024 * 1024, $"Allocated {allocated:N0} bytes for an equivalent OR chain.");
    }

    [Fact]
    public void ReplaceWithAlternativesFrom_DifferentVariableKeys_PreservesBindings()
    {
        var left = new BindingEnvironment(new BindingBudget(new AspectNCompilationOptions(null, GenericVariableMode.Capture), default));
        var right = new BindingEnvironment(new BindingBudget(new AspectNCompilationOptions(null, GenericVariableMode.Capture), default));
        var type = Named("Int32");
        var system = new StubTypeSystem();
        left.Bind(new VariableSymbol("L"), type, system, static _ => { });
        right.Bind(new VariableSymbol("R"), type, system, static _ => { });
        left.ReplaceWithAlternativesFrom([left, right]);
        Assert.Equal(2, left.CloneAlternatives().Count());
    }

    [Fact]
    public void Match_EightDistinctVariableOrGroups_PreservesResultWithinRegressionBudget()
    {
        var source = string.Join(" && ", Enumerable.Range(0, 8).Select(index => $"(List<$L{index}> || List<$R{index}>)")) + " && List<int>";
        var pattern = Compile(source);
        var candidate = Construct("List", Named("Int32"));
        var system = new StubTypeSystem();
        pattern.Match(candidate, system);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = pattern.Match(candidate, system);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(MatchStatus.Matched, result.Status);
        Assert.True(allocated < 5 * 1024 * 1024, $"Allocated {allocated:N0} bytes for eight distinct-binding OR groups.");
        Assert.Equal(MatchStatus.NotMatched, pattern.Match(Construct("List", Named("String")), system).Status);
    }

    [Theory]
    [InlineData("IValue<$T>+ && ((Carrier<$T, *> && IMissing<$T>+) || Carrier<*, $T>) && Carrier<$T, *>")]
    [InlineData("IValue<$T>+ && ((Carrier<$T, *> && IMissing<$T>+ && Carrier<$T, *>) || (Carrier<*, $T> && Carrier<$T, *>))")]
    public void Match_OrWithPartialClosure_PreservesUnknownAlternative(string source)
    {
        var candidate = Construct("Carrier", Named("String"), Named("Int32"));
        var typeSystem = new StubTypeSystem
        {
            Closure = (_, _) => new InheritanceClosure(
                [Construct("IValue", Named("String")), Construct("IValue", Named("Int32"))],
                TypeRelationResult.Unknown(new AspectNDiagnostic(
                    DiagnosticCodes.MissingBackendCapability,
                    DiagnosticSeverity.Warning,
                    "The inheritance closure is incomplete.")))
        };

        var result = Compile(source).Match(candidate, typeSystem);

        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.NotEmpty(result.Diagnostics);
    }

    private static CompiledTypePattern Compile(string source)
    {
        var compilation = AspectNCompilation.Create(AspectNParser.ParseTypePattern(source), new AspectNCompilationOptions(null, GenericVariableMode.Capture));
        Assert.True(compilation.TryGetTypePattern(out var pattern));
        return pattern!;
    }

    [Theory]
    [InlineData("attr(call TagAttribute<$T>) && method($T Service.(Run<$T, *> || *<*, $T>)(..))")]
    [InlineData("attr(call TagAttribute<$T>) && (method($T Service.Run<$T, *>(..)) || method($T Service.*<*, $T>(..)))")]
    public void Match_MemberNameOrWithUnavailableName_PreservesUnknownBinding(string source)
    {
        var text = Named("String");
        var number = Named("Int32");
        var candidate = new MethodSignature(
            "Run", MethodKind.OrdinaryMethod, Accessibility.Public, false, false,
            new NamedTypeSignature(new TypeIdentity(new AssemblyIdentity("Example"), "Example", "Service", 0, TypeKind.Class)),
            new TypeUseSignature(text, RefKind.None, TypeUseKind.Return), [], [], [text, number],
            [new AttributeSignature(Construct("TagAttribute", text), AttributeTargetKind.Method),
                new AttributeSignature(Construct("TagAttribute", number), AttributeTargetKind.Method)], [],
            availability: new MetadataAvailability(MetadataFacts.Name));
        var compilation = new AspectNEngine().CompileMemberExpression(source, new AspectNCompilationOptions(null, GenericVariableMode.Capture));
        Assert.True(compilation.TryGetMemberPattern(out var pattern));

        Assert.Equal(MatchStatus.Unknown, pattern!.Match(candidate, new StubTypeSystem()).Status);
    }

    private static NamedTypeSignature Named(string name) => new(
        new TypeIdentity(new AssemblyIdentity("Example"), "System", name, 0, TypeKind.Class));

    private static NamedTypeSignature Construct(string name, params TypeSignature[] arguments)
    {
        var definition = new NamedTypeSignature(
            new TypeIdentity(new AssemblyIdentity("Example"), "Example", name, arguments.Length, TypeKind.Class));
        return new NamedTypeSignature(definition.Identity, arguments, definition);
    }
}
