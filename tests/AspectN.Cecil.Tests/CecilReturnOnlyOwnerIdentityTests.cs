using System.Runtime.Loader;
using AspectN.Evaluation;
using AspectN.Metadata;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AspectN.Cecil.Tests;

public sealed class CecilReturnOnlyOwnerIdentityTests
{
    [Fact]
    public void MapMethod_ReturnOnlyOverloads_PreserveSeparateDeclarations()
    {
        using var module = ModuleDefinition.CreateModule("ReturnOnlyIdentity", ModuleKind.Dll);
        var owner = new TypeDefinition("Example", "Owner", TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
        module.Types.Add(owner);
        foreach (var integer in new[] { true, false })
        {
            var method = new MethodDefinition("M", MethodAttributes.Public | MethodAttributes.Static,
                integer ? module.TypeSystem.Int32 : module.TypeSystem.String);
            owner.Methods.Add(method);
            var parameter = new GenericParameter("T", method);
            method.GenericParameters.Add(parameter);
            parameter.Constraints.Add(new GenericParameterConstraint(module.ImportReference(
                integer ? typeof(IDisposable) : typeof(IComparable))));
            method.Parameters.Add(new ParameterDefinition(parameter));
            method.Body.Instructions.Add(Instruction.Create(integer ? OpCodes.Ldc_I4_0 : OpCodes.Ldnull));
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        }
        using var image = new MemoryStream();
        module.Write(image);
        var context = new AssemblyLoadContext("ReturnOnlyIdentity", isCollectible: true);
        try
        {
            var runtime = context.LoadFromStream(new MemoryStream(image.ToArray())).GetType("Example.Owner")!
                .GetMethods().Where(method => method.Name == "M").ToArray();
            Assert.Equal(2, runtime.Length);
            using var disposable = new MemoryStream();
            foreach (var method in runtime)
                if (method.ReturnType == typeof(int))
                    Assert.Equal(0, method.MakeGenericMethod(typeof(MemoryStream)).Invoke(null, [disposable]));
                else
                    Assert.Null(method.MakeGenericMethod(typeof(int)).Invoke(null, [0]));

            using var system = new CecilTypeSystem(ModuleDefinition.ReadModule(new MemoryStream(image.ToArray())));
            var methods = system.Module.GetType("Example.Owner").Methods.Select(system.Mapper.MapMethod).ToArray();
            var first = methods[0].GenericParameters[0];
            var second = methods[1].GenericParameters[0];

            Assert.NotEqual(first.OwnerId, second.OwnerId);
            Assert.Equal(MatchStatus.NotMatched, system.AreEqual(
                new GenericParameterTypeSignature(first), new GenericParameterTypeSignature(second)).Status);
        }
        finally { context.Unload(); }
    }
}
