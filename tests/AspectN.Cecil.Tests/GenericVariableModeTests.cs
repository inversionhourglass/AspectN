using AspectN.Evaluation;
using Mono.Cecil;

namespace AspectN.Cecil.Tests;

public sealed class GenericVariableModeTests
{
    [Fact]
    public void Match_OpenAndConstructedMethod_RespectsConfiguredModeAndBudget()
    {
        using var module = ModuleDefinition.CreateModule("VariableModes", ModuleKind.Dll);
        var owner = new TypeDefinition("Example", "Service", TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(owner);
        var method = new MethodDefinition("Echo", MethodAttributes.Public, module.TypeSystem.Void);
        owner.Methods.Add(method);
        var parameter = new GenericParameter("T", method);
        method.GenericParameters.Add(parameter);
        method.ReturnType = parameter;
        method.Parameters.Add(new ParameterDefinition(parameter));
        var constructed = new GenericInstanceMethod(method);
        constructed.GenericArguments.Add(module.TypeSystem.Int32);
        using var system = new CecilTypeSystem(module);
        var engine = new AspectNEngine();
        foreach (var mode in new[] { GenericVariableMode.Declaration, GenericVariableMode.Capture })
        {
            Assert.True(engine.CompileMemberExpression("method($T Service.Echo<$T>($T))",
                new AspectNCompilationOptions(null, mode)).TryGetMemberPattern(out var pattern));
            Assert.Equal(MatchStatus.Matched, pattern.Match(system.Mapper.MapMethod(method), system).Status);
            Assert.Equal(mode == GenericVariableMode.Declaration ? MatchStatus.NotMatched : MatchStatus.Matched,
                pattern.Match(system.Mapper.MapMethod(constructed), system).Status);
        }
        Assert.True(engine.CompileMemberExpression("method(* Echo<$L>(..)) || method(* Echo<$R>(..))",
            new AspectNCompilationOptions(null, GenericVariableMode.Capture, 1)).TryGetMemberPattern(out var limited));
        var result = limited.Match(system.Mapper.MapMethod(method), system);
        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(result.Diagnostics, item => item.Code == "AN3007");
    }
}
