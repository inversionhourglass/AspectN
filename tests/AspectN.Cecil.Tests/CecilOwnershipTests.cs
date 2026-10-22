using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AspectN.Cecil.Tests;

public sealed class CecilOwnershipTests
{
    [Theory]
    [InlineData("omitted", true)]
    [InlineData("default_options", true)]
    [InlineData("leave_open", true)]
    [InlineData("take_ownership", false)]
    public void Dispose_ModuleOwnership_ControlsDeferredBodyAccess(string configuration, bool remainsOpen)
    {
        var path = WriteModule();
        try
        {
            using var module = ReadModule(path);
            var method = module.GetType("Example.Owner").Methods.Single();
            var options = configuration switch
            {
                "omitted" => null,
                "default_options" => new CecilMappingOptions(),
                "leave_open" => new CecilMappingOptions(leaveOpen: true),
                "take_ownership" => new CecilMappingOptions(leaveOpen: false),
                _ => throw new ArgumentOutOfRangeException(nameof(configuration))
            };
            var system = new CecilTypeSystem(module, options);
            var mapper = system.Mapper;

            system.Dispose();
            system.Dispose();

            Assert.Throws<ObjectDisposedException>(() => system.Module);
            Assert.Throws<ObjectDisposedException>(() => mapper.MapType(method.DeclaringType));
            if (remainsOpen)
            {
                Assert.Equal(OpCodes.Ret, Assert.Single(method.Body.Instructions).OpCode);
                using var rewritten = new MemoryStream();
                module.Write(rewritten);
                Assert.NotEqual(0, rewritten.Length);
            }
            else
            {
                Assert.Throws<ObjectDisposedException>(() => method.Body);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Dispose_DefaultOwnership_KeepsOtherAdapterUsable()
    {
        var path = WriteModule();
        try
        {
            using var module = ReadModule(path);
            var method = module.GetType("Example.Owner").Methods.Single();
            using var first = new CecilTypeSystem(module);
            using var second = new CecilTypeSystem(module);

            first.Dispose();

            Assert.IsType<AspectN.Metadata.NamedTypeSignature>(second.Mapper.MapType(method.DeclaringType));
            Assert.Equal(OpCodes.Ret, Assert.Single(method.Body.Instructions).OpCode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static ModuleDefinition ReadModule(string path) => ModuleDefinition.ReadModule(path,
        new ReaderParameters { ReadingMode = ReadingMode.Deferred, InMemory = false });

    private static string WriteModule()
    {
        var path = Path.Combine(Path.GetTempPath(), $"AspectN.Ownership.{Guid.NewGuid():N}.dll");
        using var module = ModuleDefinition.CreateModule("Ownership", ModuleKind.Dll);
        var type = new TypeDefinition("Example", "Owner", TypeAttributes.Public | TypeAttributes.Class,
            module.TypeSystem.Object);
        module.Types.Add(type);
        var method = new MethodDefinition("Run", MethodAttributes.Public | MethodAttributes.Static,
            module.TypeSystem.Void);
        type.Methods.Add(method);
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        module.Write(path);
        return path;
    }
}
