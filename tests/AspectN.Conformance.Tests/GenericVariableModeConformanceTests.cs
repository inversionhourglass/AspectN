using System.Runtime.Loader;
using AspectN.Cecil;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.Reflection;
using AspectN.Roslyn;
using AspectN.TypeSystem;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Mono.Cecil;
using Xunit;

namespace AspectN.Conformance.Tests;

public sealed class GenericVariableModeConformanceTests
{
    [Fact]
    public void Match_SameSourceAndImage_DeclarationAndCaptureAgreeAcrossBackends()
    {
        var compilation = CSharpCompilation.Create("VariableModes", [CSharpSyntaxTree.ParseText("""
            namespace Review;
            public class Other<T> { }
            public class Outer<T>
            {
                public class Inner<U>
                {
                    public V Echo<V>(T first, U second, System.Collections.Generic.List<V[]> values) => default(V);
                    public U Value { get; }
                }
            }
            """)], FixtureCompiler.TrustedPlatformReferences(), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var image = FixtureCompiler.Emit(compilation);
        using var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location)!);
        using var cecil = new CecilTypeSystem(ModuleDefinition.ReadModule(new MemoryStream(image),
            new ReaderParameters { AssemblyResolver = resolver, InMemory = true }));
        var loadContext = new AssemblyLoadContext("VariableModes", isCollectible: true);
        try
        {
            var assembly = loadContext.LoadFromStream(new MemoryStream(image));
            var reflection = new ReflectionTypeSystem();
            var roslyn = new RoslynTypeSystem(compilation);
            var peCompilation = CSharpCompilation.Create("VariableModes.Consumer", references:
                FixtureCompiler.TrustedPlatformReferences().Concat([MetadataReference.CreateFromImage(image)]));
            var pe = new RoslynTypeSystem(peCompilation);
            var runtimeInner = assembly.GetType("Review.Outer`1+Inner`1")!;
            var runtimeOther = assembly.GetType("Review.Other`1")!.GetGenericArguments()[0];
            var cecilInner = cecil.Module.GetType("Review.Outer`1/Inner`1");
            var cecilOther = cecil.Module.GetType("Review.Other`1").GenericParameters[0];
            foreach (var (system, inner, borrowed, number, mapType, mapMethod) in new[]
                     {
                         RoslynView(roslyn, compilation), RoslynView(pe, peCompilation)
                     })
            {
                AssertView(system, mapType(inner), mapType(inner.ContainingType.Construct(number).GetTypeMembers("Inner", 1).Single().Construct(number)),
                    mapType(inner.ContainingType.Construct(borrowed).GetTypeMembers("Inner", 1).Single().Construct(borrowed)),
                    mapMethod(inner.GetMembers("Echo").OfType<IMethodSymbol>().Single()),
                    mapMethod(inner.GetMembers("Echo").OfType<IMethodSymbol>().Single().Construct(number)),
                    mapMethod(inner.GetMembers("Value").OfType<IPropertySymbol>().Single().GetMethod!));
            }
            AssertView(reflection, reflection.Mapper.MapType(runtimeInner), reflection.Mapper.MapType(runtimeInner.MakeGenericType(typeof(int), typeof(int))),
                reflection.Mapper.MapType(runtimeInner.MakeGenericType(runtimeOther, runtimeOther)),
                reflection.Mapper.MapMethod(runtimeInner.GetMethod("Echo")!),
                reflection.Mapper.MapMethod(runtimeInner.GetMethod("Echo")!.MakeGenericMethod(typeof(int))),
                reflection.Mapper.MapMethod(runtimeInner.GetProperty("Value")!.GetMethod!));
            var constructed = new GenericInstanceType(cecilInner);
            constructed.GenericArguments.Add(cecil.Module.TypeSystem.Int32);
            constructed.GenericArguments.Add(cecil.Module.TypeSystem.Int32);
            var borrowedType = new GenericInstanceType(cecilInner);
            borrowedType.GenericArguments.Add(cecilOther);
            borrowedType.GenericArguments.Add(cecilOther);
            var method = cecilInner.Methods.Single(item => item.Name == "Echo");
            var closedMethod = new GenericInstanceMethod(method);
            closedMethod.GenericArguments.Add(cecil.Module.TypeSystem.Int32);
            AssertView(cecil, cecil.Mapper.MapType(cecilInner), cecil.Mapper.MapType(constructed), cecil.Mapper.MapType(borrowedType),
                cecil.Mapper.MapMethod(method), cecil.Mapper.MapMethod(closedMethod),
                cecil.Mapper.MapMethod(cecilInner.Methods.Single(item => item.Name == "get_Value")));
        }
        finally
        {
            loadContext.Unload();
        }
    }

    private static (ITypeSystem System, INamedTypeSymbol Inner, ITypeSymbol Borrowed, ITypeSymbol Number,
        Func<ITypeSymbol, TypeSignature> MapType, Func<IMethodSymbol, MethodSignature> MapMethod)
        RoslynView(RoslynTypeSystem system, Compilation compilation) =>
        (system, compilation.GetTypeByMetadataName("Review.Outer`1+Inner`1")!,
            compilation.GetTypeByMetadataName("Review.Other`1")!.TypeParameters[0], compilation.GetSpecialType(SpecialType.System_Int32),
            type => system.Mapper.MapType(type), method => system.Mapper.MapMethod(method));

    private static void AssertView(ITypeSystem system, TypeSignature definition, TypeSignature closed, TypeSignature borrowed,
        MethodSignature method, MethodSignature closedMethod, MethodSignature getter)
    {
        var engine = new AspectNEngine();
        foreach (var mode in new[] { GenericVariableMode.Declaration, GenericVariableMode.Capture })
        {
            var options = new AspectNCompilationOptions(mode);
            Assert.True(engine.CompileTypePattern("Review.Outer<$T>/Inner<$U>", compilationOptions: options).TryGetTypePattern(out var typePattern));
            Assert.Equal(MatchStatus.Matched, typePattern.Match(definition, system).Status);
            var constructionStatus = mode == GenericVariableMode.Declaration ? MatchStatus.NotMatched : MatchStatus.Matched;
            Assert.Equal(constructionStatus, typePattern.Match(closed, system).Status);
            Assert.Equal(constructionStatus, typePattern.Match(borrowed, system).Status);
            Assert.True(engine.CompileMemberExpression("method($V Review.Outer<$T>/Inner<$U>.Echo<$V>($T, $U, List<$V[]>))", compilationOptions: options).TryGetMemberPattern(out var memberPattern));
            Assert.Equal(MatchStatus.Matched, memberPattern.Match(method, system).Status);
            Assert.Equal(constructionStatus, memberPattern.Match(closedMethod, system).Status);
            Assert.True(engine.CompileMemberExpression("getter($U Review.Outer<$T>/Inner<$U>.Value)", compilationOptions: options).TryGetMemberPattern(out var propertyPattern));
            Assert.Equal(MatchStatus.Matched, propertyPattern.Match(getter, system).Status);
            Assert.True(engine.CompileMemberExpression("method(* Echo<$T>(..)) || method(* Echo<$U>(..))", compilationOptions: new AspectNCompilationOptions(GenericVariableMode.Capture, new AspectNMatchOptions(maximumBindingAlternatives: ResourceLimit.Finite(1)))).TryGetMemberPattern(out var limited));
            var failure = limited.Match(method, system);
            Assert.Equal(MatchStatus.Unknown, failure.Status);
            Assert.Contains(failure.Diagnostics, item => item.Code == "AN3007");
        }
    }
}
