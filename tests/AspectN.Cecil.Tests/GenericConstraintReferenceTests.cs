using AspectN.Diagnostics;
using AspectN.Evaluation;
using Mono.Cecil;

namespace AspectN.Cecil.Tests;

public sealed class GenericConstraintReferenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsAssignableTo_MethodReferenceParameter_RecoversAndSubstitutesDeclarationConstraints(bool closedOwner)
    {
        using var module = TestModuleFactory.Create();
        var owner = new TypeDefinition("Example", "Owner`1", TypeAttributes.Public, module.TypeSystem.Object);
        owner.GenericParameters.Add(new GenericParameter("T", owner));
        module.Types.Add(owner);
        var method = new MethodDefinition("M", MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
        owner.Methods.Add(method);
        var declarationParameter = new GenericParameter("U", method);
        method.GenericParameters.Add(declarationParameter);
        declarationParameter.Constraints.Add(new GenericParameterConstraint(
            closedOwner ? owner.GenericParameters[0] : module.ImportReference(typeof(IDisposable))));

        TypeReference declaring = owner;
        if (closedOwner)
        {
            var constructed = new GenericInstanceType(owner);
            constructed.GenericArguments.Add(module.TypeSystem.String);
            declaring = constructed;
        }
        var reference = new MethodReference("M", module.TypeSystem.Void, declaring) { HasThis = false };
        var referenceParameter = new GenericParameter("U", reference);
        reference.GenericParameters.Add(referenceParameter);
        Assert.Empty(referenceParameter.Constraints);
        using var system = new CecilTypeSystem(module);
        var candidate = system.Mapper.MapType(referenceParameter);
        var target = system.Mapper.MapType(closedOwner ? module.TypeSystem.String : module.ImportReference(typeof(IDisposable)));
        Assert.Equal(MatchStatus.Matched, system.IsAssignableTo(candidate, target).Status);
        Assert.Contains(system.GetInheritanceClosure(candidate).Ancestors,
            ancestor => system.AreEqual(ancestor, target).Status == MatchStatus.Matched);
    }

    [Fact]
    public void IsAssignableTo_TransitiveMethodConstraint_RetainsClosedDeclaringContext()
    {
        using var module = TestModuleFactory.Create();
        var owner = new TypeDefinition("Example", "Owner`1", TypeAttributes.Public, module.TypeSystem.Object);
        owner.GenericParameters.Add(new GenericParameter("Outer", owner));
        module.Types.Add(owner);
        var method = new MethodDefinition("M", MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
        owner.Methods.Add(method);
        var u = new GenericParameter("U", method);
        var v = new GenericParameter("V", method);
        method.GenericParameters.Add(u);
        method.GenericParameters.Add(v);
        u.Constraints.Add(new GenericParameterConstraint(v));
        v.Constraints.Add(new GenericParameterConstraint(owner.GenericParameters[0]));
        var declaring = new GenericInstanceType(owner);
        declaring.GenericArguments.Add(module.TypeSystem.String);
        var reference = new MethodReference("M", module.TypeSystem.Void, declaring) { HasThis = false };
        reference.GenericParameters.Add(new GenericParameter("U", reference));
        reference.GenericParameters.Add(new GenericParameter("V", reference));
        using var system = new CecilTypeSystem(module);
        var candidate = system.Mapper.MapType(reference.GenericParameters[0]);
        var target = system.Mapper.MapType(module.TypeSystem.String);
        Assert.Equal(MatchStatus.Matched, system.IsAssignableTo(candidate, target).Status);
        Assert.Contains(system.GetInheritanceClosure(candidate).Ancestors,
            ancestor => system.AreEqual(ancestor, target).Status == MatchStatus.Matched);
    }

    [Fact]
    public void IsAssignableTo_UnresolvedParameterOwner_PreservesUnknown()
    {
        using var module = TestModuleFactory.Create();
        var missing = new TypeReference("Missing", "Owner", module, new AssemblyNameReference("Missing", new Version(1, 0)));
        var method = new MethodReference("M", module.TypeSystem.Void, missing);
        var parameter = new GenericParameter("T", method);
        method.GenericParameters.Add(parameter);
        using var system = new CecilTypeSystem(module);
        var candidate = system.Mapper.MapType(parameter);
        var result = system.IsAssignableTo(candidate, system.Mapper.MapType(module.TypeSystem.String));
        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCodes.ResolutionFailure);
        Assert.Equal(MatchStatus.Matched, system.IsAssignableTo(candidate, system.Mapper.MapType(module.TypeSystem.Object)).Status);
    }
}
