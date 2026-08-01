using Mono.Cecil;

namespace AspectN.Cecil.Tests;

internal static class TestModuleFactory
{
    public static ModuleDefinition Create(string name = "AspectN.Cecil.Tests")
    {
        return ModuleDefinition.CreateModule(name, ModuleKind.Dll);
    }
}
