using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AspectN.Cecil.Tests;

public sealed class CecilUnavailableMetadataTests
{
    [Theory]
    [InlineData("execution(* External.Widget.Run(..))", MatchStatus.Matched)]
    [InlineData("execution(!static * External.Widget.Run(..))", MatchStatus.Matched)]
    [InlineData("execution(static * External.Widget.Run(..))", MatchStatus.NotMatched)]
    [InlineData("execution(public * External.Widget.Run(..))", MatchStatus.Unknown)]
    [InlineData("execution(!public * External.Widget.Run(..))", MatchStatus.Unknown)]
    [InlineData("!execution(public * External.Widget.Run(..))", MatchStatus.Unknown)]
    [InlineData("method(* External.Widget.Run(..))", MatchStatus.Unknown)]
    [InlineData("callable(* External.Widget.Run(..))", MatchStatus.Unknown)]
    [InlineData("getter(* External.Widget.Run)", MatchStatus.Unknown)]
    [InlineData("method(* External.Widget.Other(..))", MatchStatus.NotMatched)]
    [InlineData("execution(public string External.Widget.Run(..))", MatchStatus.NotMatched)]
    [InlineData("attr(call System.ObsoleteAttribute)", MatchStatus.Unknown)]
    [InlineData("attr(call *)", MatchStatus.Unknown)]
    [InlineData("!attr(call *)", MatchStatus.Unknown)]
    [InlineData("attr(ret *)", MatchStatus.Unknown)]
    [InlineData("attr(type *)", MatchStatus.Unknown)]
    [InlineData("attr(para[*] *)", MatchStatus.Unknown)]
    [InlineData("attr(para[5] *)", MatchStatus.NotMatched)]
    [InlineData("attr(exec *)", MatchStatus.Unknown)]
    [InlineData("attr(any *)", MatchStatus.Unknown)]
    [InlineData("regex(Run)", MatchStatus.Unknown)]
    public void Match_UnresolvedMember_UsesOnlyAvailableFacts(string expression, MatchStatus expected)
    {
        using var resolver = new ThrowingResolver("missing");
        using var module = CreateModule(resolver);
        using var typeSystem = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var signature = typeSystem.Mapper.MapMethod(CreateReference(module));
        var compilation = new AspectNEngine().CompileMemberExpression(expression);
        Assert.True(compilation.TryGetMemberPattern(out var pattern));

        var result = pattern!.Match(signature, typeSystem);

        Assert.Equal(expected, result.Status);
        if (expected == MatchStatus.Unknown)
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "AN3004");
    }

    [Theory]
    [InlineData("execution(* External.Widget.Run(*))", MatchStatus.Matched)]
    [InlineData("execution(ref int External.Widget.Run(*))", MatchStatus.Unknown)]
    [InlineData("execution(ref readonly int External.Widget.Run(*))", MatchStatus.Unknown)]
    [InlineData("execution(* External.Widget.Run(out int))", MatchStatus.Unknown)]
    [InlineData("execution(* External.Widget.Run(in int))", MatchStatus.Unknown)]
    [InlineData("execution(!ref int External.Widget.Run(*))", MatchStatus.NotMatched)]
    [InlineData("execution(* External.Widget.Run(!ref int))", MatchStatus.NotMatched)]
    public void Match_UnresolvedByRefMember_PreservesByRefButNotItsClassification(string expression, MatchStatus expected)
    {
        using var resolver = new ThrowingResolver("missing");
        using var module = CreateModule(resolver);
        using var typeSystem = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var reference = CreateReference(module);
        reference.ReturnType = new ByReferenceType(module.TypeSystem.Int32);
        reference.Parameters[0].ParameterType = new ByReferenceType(module.TypeSystem.Int32);
        var signature = typeSystem.Mapper.MapMethod(reference);
        var compilation = new AspectNEngine().CompileMemberExpression(expression);
        Assert.True(compilation.TryGetMemberPattern(out var pattern));

        Assert.Equal(expected, pattern!.Match(signature, typeSystem).Status);
    }

    [Theory]
    [InlineData("io")]
    [InlineData("bad-image")]
    [InlineData("argument")]
    [InlineData("unsupported")]
    [InlineData("invalid-operation")]
    [InlineData("missing")]
    public void MapAndQuery_RecoverableResolverFailure_ReturnsUnknownWithDistinctCode(string failure)
    {
        using var resolver = new ThrowingResolver(failure);
        using var module = CreateModule(resolver);
        using var typeSystem = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var signature = typeSystem.Mapper.MapMethod(CreateReference(module));
        var attribute = typeSystem.Mapper.MapType(module.TypeSystem.Object);

        var result = typeSystem.HasDirectAttribute(AttributeTarget.ForMethod(signature), attribute);
        var typeResult = typeSystem.HasDirectAttribute(AttributeTarget.ForType(signature.DeclaringType), attribute);

        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Equal(MatchStatus.Unknown, typeResult.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "AN3004");
        Assert.Contains(typeResult.Diagnostics, diagnostic => diagnostic.Code == "AN3004");
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.InvalidRegex);
    }

    [Fact]
    public void MapMethod_Cancellation_Propagates()
    {
        using var resolver = new ThrowingResolver("cancel");
        using var module = CreateModule(resolver);
        using var typeSystem = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));

        Assert.Throws<OperationCanceledException>(() => typeSystem.Mapper.MapMethod(CreateReference(module)));
    }

    [Fact]
    public void MapMethod_ResolvedReference_UsesDefinitionParameterName()
    {
        using var module = ModuleDefinition.CreateModule("ParameterNames", ModuleKind.Dll);
        var owner = new TypeDefinition("Example", "Widget", TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(owner);
        var definition = new MethodDefinition("Run", MethodAttributes.Public, module.TypeSystem.Void);
        definition.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, module.TypeSystem.Int32));
        owner.Methods.Add(definition);
        var reference = new MethodReference("Run", module.TypeSystem.Void, owner) { HasThis = true };
        reference.Parameters.Add(new ParameterDefinition(module.TypeSystem.Int32));
        using var typeSystem = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));

        var parameter = Assert.Single(typeSystem.Mapper.MapMethod(reference).Parameters);

        Assert.Equal("value", parameter.Name);
        Assert.True(parameter.Availability.IsKnown(MetadataFacts.Name));
    }

    [Fact]
    public void MapProperty_DamagedLazySemantics_PreservesIndependentFacts()
    {
        using var original = ModuleDefinition.CreateModule("MalformedProperty", ModuleKind.Dll);
        var owner = new TypeDefinition("Example", "Widget", TypeAttributes.Public, original.TypeSystem.Object);
        original.Types.Add(owner);
        var getter = new MethodDefinition("get_Value", MethodAttributes.Public | MethodAttributes.SpecialName,
            original.TypeSystem.Int32);
        getter.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4_0));
        getter.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        owner.Methods.Add(getter);
        owner.Properties.Add(new PropertyDefinition("Value", PropertyAttributes.None, original.TypeSystem.Int32)
        { GetMethod = getter });
        using var buffer = new MemoryStream();
        original.Write(buffer);
        using var loaded = ModuleDefinition.ReadModule(new MemoryStream(buffer.ToArray()),
            new ReaderParameters { ReadingMode = ReadingMode.Deferred });
        var property = loaded.Types[1].Properties[0];
        CecilDamagedMetadataFixture.DamagePropertySemantics(loaded);
        using var typeSystem = new CecilTypeSystem(loaded, new CecilMappingOptions(leaveOpen: true));

        var signature = typeSystem.Mapper.MapProperty(property);

        Assert.Equal("Value", signature.Name);
        Assert.False(signature.Availability.IsKnown(MetadataFacts.Accessibility | MetadataFacts.IsStatic));
        Assert.Contains(signature.Availability.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.ResolutionFailure);
        Assert.True(signature.Availability.IsKnown(MetadataFacts.Attributes));
        Assert.False(signature.Availability.IsKnown(MetadataFacts.Parameters));
        Assert.IsType<NamedTypeSignature>(signature.PropertyType.Type);
    }

    private static ModuleDefinition CreateModule(IAssemblyResolver resolver)
    {
        return ModuleDefinition.CreateModule("UnavailableMetadata", new ModuleParameters { AssemblyResolver = resolver });
    }

    private static MethodReference CreateReference(ModuleDefinition module)
    {
        var owner = new TypeReference("External", "Widget", module,
            new AssemblyNameReference("MissingMetadata", new Version(1, 0)));
        var reference = new MethodReference("Run", module.TypeSystem.Int32, owner) { HasThis = true };
        reference.Parameters.Add(new ParameterDefinition(module.TypeSystem.Int32));
        return reference;
    }

    private sealed class ThrowingResolver(string failure) : IAssemblyResolver
    {
        public AssemblyDefinition Resolve(AssemblyNameReference name) => Resolve(name, new ReaderParameters());

        public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            throw failure switch
            {
                "io" => new IOException("Unreadable metadata."),
                "bad-image" => new BadImageFormatException("Invalid metadata image."),
                "argument" => new ArgumentException("Invalid metadata."),
                "unsupported" => new NotSupportedException("Unsupported metadata."),
                "invalid-operation" => new InvalidOperationException("Unresolved metadata."),
                "cancel" => new OperationCanceledException(),
                _ => new AssemblyResolutionException(name)
            };
        }

        public void Dispose() { }
    }
}
