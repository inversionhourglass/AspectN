using System.Runtime.Loader;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Tests.Shared;
using Mono.Cecil;

namespace AspectN.Cecil.Tests;

public sealed class CecilCustomModifierOwnerIdentityTests
{
    [Theory]
    [InlineData("return", false, false)]
    [InlineData("return", true, false)]
    [InlineData("return", false, true)]
    [InlineData("parameter", false, false)]
    [InlineData("element", true, false)]
    [InlineData("required-order", false, false)]
    [InlineData("mixed", false, false)]
    [InlineData("array", true, false)]
    [InlineData("generic", true, false)]
    public void MapMethod_CustomModifierOverloads_PreserveDeclarationsAndConstraints(string location, bool optional, bool otherModifier)
    {
        var image = CustomModifierOwnerFixture.CreateImage(location, optional, otherModifier);
        var context = new AssemblyLoadContext("CustomModifierIdentity", isCollectible: true);
        try
        {
            var runtime = context.LoadFromStream(new MemoryStream(image)).GetType("Example.Owner")!.GetMethods()
                .Where(method => method.Name == "M").ToArray();
            Assert.Equal(2, runtime.Length);
            using var disposable = new MemoryStream();
            foreach (var (method, type, value) in new[]
                     { (runtime[0], typeof(MemoryStream), (object)disposable), (runtime[1], typeof(int), (object)42) })
            {
                var closed = method.MakeGenericMethod(type);
                var argumentType = closed.GetParameters()[0].ParameterType;
                var argument = argumentType.IsArray ? Array.CreateInstance(type, 0) :
                    argumentType.IsGenericType ? Activator.CreateInstance(argumentType)! : value;
                Assert.Equal(argument, closed.Invoke(null, [argument]));
            }

            using var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location)!);
            using var system = new CecilTypeSystem(ModuleDefinition.ReadModule(new MemoryStream(image),
                new ReaderParameters { AssemblyResolver = resolver }));
            var native = system.Module.GetType("Example.Owner").Methods.ToArray();
            var variables = native.Select(method => system.Mapper.MapType(method.GenericParameters[0])).ToArray();
            Assert.Equal(MatchStatus.NotMatched, system.AreEqual(variables[0], variables[1]).Status);
            var target = system.Mapper.MapType(system.Module.ImportReference(typeof(IDisposable)));
            Assert.Equal(MatchStatus.Matched, system.IsAssignableTo(variables[0], target).Status);
            Assert.Equal(MatchStatus.NotMatched, system.IsAssignableTo(variables[1], target).Status);
            // 从 MemberRef 解析也必须核验修饰符，不能误用第一个同形声明。
            var reference = new MethodReference("M", native[1].ReturnType, native[1].DeclaringType) { HasThis = false };
            reference.GenericParameters.Add(new GenericParameter("T", reference));
            reference.Parameters.Add(new ParameterDefinition(native[1].Parameters[0].ParameterType));
            Assert.Equal(system.Mapper.MapMethod(native[1]).GenericParameters[0].OwnerId,
                system.Mapper.MapMethod(reference).GenericParameters[0].OwnerId);
            if (location != "return" || optional || otherModifier) return;
            var compilation = new AspectNEngine().CompileMemberExpression("method(ref readonly System.IDisposable+ Example.Owner.M<..>(..))");
            Assert.True(compilation.TryGetMemberPattern(out var pattern));
            Assert.Equal(MatchStatus.NotMatched, pattern.Match(system.Mapper.MapMethod(native[1]), system).Status);
        }
        finally { context.Unload(); }
    }
}
