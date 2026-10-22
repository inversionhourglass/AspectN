using AspectN.Evaluation;
using AspectN.Binding;
using AspectN.Metadata;
using AspectN.Tests.TestDoubles;

namespace AspectN.Tests.Evaluation;

public sealed class GenericVariableModeTests
{
    [Fact]
    public void Options_DefaultAndCapture_ExposeValidatedValues()
    {
        var defaults = new AspectNCompilationOptions();
        Assert.Equal(GenericVariableMode.Declaration, defaults.GenericVariableMode);
        var capture = new AspectNCompilationOptions(GenericVariableMode.Capture, new AspectNMatchOptions(maximumBindingAlternatives: ResourceLimit.Finite(7), maximumBindingWork: ResourceLimit.Finite(123)));
        Assert.Equal(GenericVariableMode.Capture, capture.GenericVariableMode);
        Assert.Equal(7, capture.MatchOptions.MaximumBindingAlternatives.Value);
        Assert.Equal(123, capture.MatchOptions.MaximumBindingWork.Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => new AspectNCompilationOptions((GenericVariableMode)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AspectNCompilationOptions(GenericVariableMode.Capture, new AspectNMatchOptions(maximumBindingAlternatives: ResourceLimit.Finite(0))));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AspectNCompilationOptions(GenericVariableMode.Capture, new AspectNMatchOptions(maximumBindingAlternatives: ResourceLimit.Finite(1), maximumBindingWork: ResourceLimit.Finite(0))));
        Assert.Equal(defaults.MatchOptions.RegexTimeout.Value, new AspectNCompilationOptions().MatchOptions.RegexTimeout.Value);
        Assert.Equal(defaults.MatchOptions.RegexTimeout.Value, new AspectNCompilationOptions().MatchOptions.RegexTimeout.Value);
        Assert.Equal(TimeSpan.FromSeconds(1), new AspectNCompilationOptions(matchOptions: new AspectNMatchOptions(regexTimeout: TimeLimit.Finite(TimeSpan.FromSeconds(1)))).MatchOptions.RegexTimeout.Value);
    }

    [Fact]
    public void Compile_ModeAndBudgets_IsolateCacheEntries()
    {
        var engine = new AspectNEngine();
        var first = engine.CompileTypePattern("Box<$T>");
        var capture = engine.CompileTypePattern("Box<$T>", compilationOptions: new AspectNCompilationOptions(GenericVariableMode.Capture));
        Assert.NotSame(first, capture);
        Assert.Same(capture, engine.CompileTypePattern("Box<$T>", compilationOptions: new AspectNCompilationOptions(GenericVariableMode.Capture)));
        Assert.NotSame(capture, engine.CompileTypePattern("Box<$T>", compilationOptions: new AspectNCompilationOptions(GenericVariableMode.Capture, new AspectNMatchOptions(maximumBindingAlternatives: ResourceLimit.Finite(5)))));
        Assert.NotSame(capture, engine.CompileTypePattern("Box<$T>", compilationOptions: new AspectNCompilationOptions(GenericVariableMode.Capture, new AspectNMatchOptions(maximumBindingAlternatives: ResourceLimit.Finite(4096), maximumBindingWork: ResourceLimit.Finite(20)))));
    }

    [Fact]
    public void Match_DefaultVariable_RejectsConcreteArgument()
    {
        var definition = Definition();
        var concrete = new NamedTypeSignature(definition.Identity, [Number()], definition);
        var compilation = new AspectNEngine().CompileTypePattern("Box<$T>");
        Assert.True(compilation.TryGetTypePattern(out var pattern));

        Assert.Equal(MatchStatus.NotMatched, pattern.Match(concrete, new StubTypeSystem()).Status);
    }

    [Fact]
    public void Match_DefaultVariable_AcceptsOwnDeclarationSlot()
    {
        var compilation = new AspectNEngine().CompileTypePattern("Box<$T>");
        Assert.True(compilation.TryGetTypePattern(out var pattern));

        Assert.Equal(MatchStatus.Matched, pattern.Match(Definition(), new StubTypeSystem()).Status);
    }

    [Theory]
    [InlineData("IValue<$T>+")]
    [InlineData("Box<List<$T>>")]
    [InlineData("tasklike<Box<$T>>")]
    [InlineData("(Box<$T>, int)")]
    public void Compile_DefaultVariable_RejectsIndirectDeclaration(string source)
    {
        var compilation = new AspectNEngine().CompileTypePattern(source);

        Assert.False(compilation.TryGetTypePattern(out _));
        Assert.Contains(compilation.Diagnostics, item => item.Code == "AN2006");
    }

    [Fact]
    public void Compile_DefaultAttributeVariable_RejectsNewCapture()
    {
        var compilation = new AspectNEngine().CompileMemberExpression("attr(call TagAttribute<$T>)");

        Assert.False(compilation.TryGetMemberPattern(out _));
        Assert.Contains(compilation.Diagnostics, item => item.Code == "AN2006");
    }

    [Fact]
    public void Match_DefaultVariable_RejectsBorrowedParameterInOpenConstruction()
    {
        var borrowed = Parameter(GenericParameterOwnerKind.Type, "Example.Other`1", 0);
        var candidate = new NamedTypeSignature(Definition().Identity, [borrowed]);
        Assert.Equal(MatchStatus.NotMatched, TypePattern("Box<$T>").Match(candidate, new StubTypeSystem()).Status);
    }

    [Fact]
    public void Match_NestedTypeSlots_UseContainingOwnerAndFlattenedPosition()
    {
        var outer = Definition().Identity;
        var inner = new TypeIdentity(outer.Assembly, "", "Inner", 1, TypeKind.Class, outer);
        var first = Parameter(GenericParameterOwnerKind.Type, outer.FullMetadataName, 0);
        var second = Parameter(GenericParameterOwnerKind.Type, inner.FullMetadataName, 1);
        var candidate = new NamedTypeSignature(inner, [first, second]);
        Assert.Equal(MatchStatus.Matched, TypePattern("Box<$T>/Inner<$U>").Match(candidate, new StubTypeSystem()).Status);
        Assert.Equal(MatchStatus.NotMatched, TypePattern("Box<$T>/Inner<$T>").Match(candidate, new StubTypeSystem()).Status);
        var wrongOwner = new NamedTypeSignature(inner, [first, Parameter(GenericParameterOwnerKind.Type, outer.FullMetadataName, 1)]);
        Assert.Equal(MatchStatus.NotMatched, TypePattern("Box<$T>/Inner<$U>").Match(wrongOwner, new StubTypeSystem()).Status);
    }

    [Fact]
    public void Match_WrappedRoot_ResolvesArrayAndNullableReferencePaths()
    {
        var array = new ArrayTypeSignature(Definition(), 1);
        Assert.Equal(MatchStatus.Matched, TypePattern("Box<$T>[]").Match(array, new StubTypeSystem()).Status);
        Assert.Equal(MatchStatus.Matched, TypePattern("Box<$T>?").Match(Definition(), new StubTypeSystem()).Status);
    }

    [Fact]
    public void Match_MethodSlot_RequiresOwnIdentityAndKeepsActualConstruction()
    {
        var own = Parameter(GenericParameterOwnerKind.Method, "Example.Service.Echo`1(!!0)->!!0", 0);
        var definition = Method(own, own);
        var pattern = MemberPattern("method($T Service.Echo<$T>($T))");
        Assert.Equal(MatchStatus.Matched, pattern.Match(definition, new StubTypeSystem()).Status);
        foreach (var other in new TypeSignature[] { Number(), Parameter(GenericParameterOwnerKind.Type, own.Parameter.OwnerId, 0),
                     Parameter(GenericParameterOwnerKind.Method, "Other.Echo`1(!!0)->!!0", 0) })
        {
            var construction = Method(own, other, definition);
            Assert.Equal(MatchStatus.NotMatched, pattern.Match(construction, new StubTypeSystem()).Status);
        }
        var wrongReturn = Method(own, Parameter(GenericParameterOwnerKind.Method, "Different.Echo", 0));
        Assert.Equal(MatchStatus.NotMatched, pattern.Match(wrongReturn, new StubTypeSystem()).Status);
    }

    [Fact]
    public void Match_DefaultReferences_StayFixedAcrossAttributeAndAncestorEnumeration()
    {
        var own = Parameter(GenericParameterOwnerKind.Method, "Example.Service.Echo`1(!!0)->!!0", 0);
        var tag = new TypeIdentity(own.Parameter.OwnerAssembly, "Example", "TagAttribute", 1, TypeKind.Class);
        var candidate = Method(own, own, attributes: [new AttributeSignature(new NamedTypeSignature(tag, [Number()]), AttributeTargetKind.Method),
            new AttributeSignature(new NamedTypeSignature(tag, [own]), AttributeTargetKind.Method)]);
        var pattern = MemberPattern("method($T Service.Echo<$T>($T)) && attr(call TagAttribute<$T>) && method(IValue<$T>+ Echo<>(..))");
        var system = new StubTypeSystem
        {
            Closure = (_, _) => new AspectN.TypeSystem.InheritanceClosure([
            new NamedTypeSignature(new TypeIdentity(tag.Assembly, "Example", "IValue", 1, TypeKind.Interface), [Number()]),
            new NamedTypeSignature(new TypeIdentity(tag.Assembly, "Example", "IValue", 1, TypeKind.Interface), [own])], null)
        };
        Assert.Equal(MatchStatus.Matched, pattern.Match(candidate, system).Status);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(13)]
    public void Evaluate_DefaultOrGroups_HasNoDynamicEnvironment(int groups)
    {
        var source = string.Join(" && ", Enumerable.Range(0, groups).Select(index => $"(Box<$L{index}> || Box<$R{index}>)"));
        var tree = AspectNParser.ParseTypePattern(source);
        var options = new AspectNCompilationOptions(GenericVariableMode.Declaration, new AspectNMatchOptions(maximumBindingAlternatives: ResourceLimit.Finite(1), maximumBindingWork: ResourceLimit.Finite(1)));
        var root = Assert.IsAssignableFrom<BoundTypePattern>(new Binder(tree, options).Bind());
        var context = new EvaluationContext(new StubTypeSystem(), options, default, type: Definition());
        var result = context.CreateResult(TypePatternEvaluator.Evaluate(root, Definition(), context));
        Assert.Equal(MatchStatus.Matched, result.Status);
        Assert.False(context.HasDynamicBindings);
        Assert.Null(context.CreateAlternativeCollector());
        Assert.False(context.Fork().HasDynamicBindings);
        Assert.Equal(0, context.BindingWorkUsed);
    }

    private static CompiledTypePattern TypePattern(string source)
    {
        var compilation = new AspectNEngine().CompileTypePattern(source);
        Assert.True(compilation.TryGetTypePattern(out var pattern), string.Join("; ", compilation.Diagnostics.Select(item => item.Message)));
        return pattern;
    }

    private static CompiledMemberPattern MemberPattern(string source)
    {
        var compilation = new AspectNEngine().CompileMemberExpression(source);
        Assert.True(compilation.TryGetMemberPattern(out var pattern), string.Join("; ", compilation.Diagnostics.Select(item => item.Message)));
        return pattern;
    }

    private static GenericParameterTypeSignature Parameter(GenericParameterOwnerKind kind, string owner, int position) =>
        new(new GenericParameterSignature("T", kind, owner, new AssemblyIdentity("Example"), position));

    private static MethodSignature Method(GenericParameterTypeSignature declaration, TypeSignature value,
        MethodSignature? original = null, IEnumerable<AttributeSignature>? attributes = null) =>
        new("Echo", MethodKind.OrdinaryMethod, Accessibility.Public, false, false,
            new NamedTypeSignature(new TypeIdentity(new AssemblyIdentity("Example"), "Example", "Service", 0, TypeKind.Class)),
            new TypeUseSignature(value, RefKind.None, TypeUseKind.Return),
            [new ParameterSignature("value", 0, new TypeUseSignature(value, RefKind.None, TypeUseKind.Parameter))],
            [declaration.Parameter], original is null ? [] : [value], attributes ?? [], [], originalDefinition: original);

    private static NamedTypeSignature Definition() => new(new TypeIdentity(new AssemblyIdentity("Example"), "Example", "Box", 1, TypeKind.Class));
    private static NamedTypeSignature Number() => new(new TypeIdentity(new AssemblyIdentity("System"), "System", "Int32", 0, TypeKind.Struct));
}
