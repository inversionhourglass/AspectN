using System.Runtime.CompilerServices;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AspectN.Roslyn.Tests;

public sealed class RoslynMappingTests
{
    [Fact]
    public void MapType_ConstructedNestedTupleAndNullable_PreserveSemantics()
    {
        var compilation = RoslynTestCompilation.Create();
        var typeSystem = new RoslynTypeSystem(compilation);
        var outer = RequiredType(compilation, "Fixtures.Outer`1");
        var constructedOuter = outer.Construct(
            compilation.GetSpecialType(SpecialType.System_Int32));
        var inner = Assert.Single(constructedOuter.GetTypeMembers("Inner"));
        var constructedInner = inner.Construct(
            compilation.GetSpecialType(SpecialType.System_String),
            compilation.GetSpecialType(SpecialType.System_Double));
        var mappedInner = Assert.IsType<NamedTypeSignature>(
            typeSystem.Mapper.MapType(constructedInner));
        var sample = RequiredType(compilation, "Fixtures.Sample`1");
        var tupleProperty = RequiredProperty(sample, "Pair");
        var requiredProperty = RequiredProperty(sample, "Required");
        var optionalProperty = RequiredProperty(sample, "Optional");
        var tuple = Assert.IsType<NamedTypeSignature>(
            typeSystem.Mapper.MapType(tupleProperty.Type));
        var typeParameter = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(Assert.Single(sample.TypeParameters)));

        Assert.Equal("Fixtures.Outer`1/Inner`2", mappedInner.Identity.FullMetadataName);
        Assert.Equal(3, mappedInner.TypeArguments.Count);
        Assert.NotNull(mappedInner.OriginalDefinition);
        Assert.Equal("System.ValueTuple`2", tuple.Identity.FullMetadataName);
        Assert.Equal(2, tuple.TypeArguments.Count);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.AreEqual(
                typeSystem.Mapper.MapType(requiredProperty.Type),
                typeSystem.Mapper.MapType(optionalProperty.Type)).Status);
        Assert.Equal(GenericParameterOwnerKind.Type, typeParameter.Parameter.OwnerKind);
        Assert.Equal(0, typeParameter.Parameter.Position);
    }

    [Fact]
    public void MapCallable_ConstructedGenericAndSpecialMembers_MapShape()
    {
        var compilation = RoslynTestCompilation.Create();
        var typeSystem = new RoslynTypeSystem(compilation);
        var definitionType = RequiredType(compilation, "Fixtures.Sample`1");
        var constructedType = definitionType.Construct(
            compilation.GetSpecialType(SpecialType.System_String));
        var definition = RequiredMethod(definitionType, "EchoAsync");
        var constructed = RequiredMethod(constructedType, "EchoAsync")
            .Construct(compilation.GetSpecialType(SpecialType.System_Int32));
        var definitionSignature = typeSystem.Mapper.MapCallable(definition);
        var signature = typeSystem.Mapper.MapCallable(constructed);
        var constructors = definitionType.InstanceConstructors
            .Where(method => !method.IsImplicitlyDeclared)
            .ToArray();
        var constructor = typeSystem.Mapper.MapCallable(
            Assert.Single(constructors));
        var staticConstructor = typeSystem.Mapper.MapCallable(
            Assert.Single(definitionType.StaticConstructors));
        var addition = typeSystem.Mapper.MapCallable(
            RequiredMethod(definitionType, "op_Addition"));
        var conversion = typeSystem.Mapper.MapCallable(
            RequiredMethod(definitionType, "op_Implicit"));

        Assert.Single(definitionSignature.GenericParameters);
        Assert.Empty(definitionSignature.GenericArguments);
        Assert.True(signature.IsAsync);
        Assert.Empty(signature.GenericParameters);
        Assert.Equal("System.Int32", Name(Assert.Single(signature.GenericArguments)));
        Assert.Equal(
            "System.Threading.Tasks.Task`1",
            Name(signature.ReturnType.Type));
        Assert.Equal("System.String", Name(signature.Parameters[0].Type.Type));
        Assert.Equal("System.Int32", Name(signature.Parameters[1].Type.Type));
        Assert.NotNull(signature.OriginalDefinition);
        Assert.Equal(CallableKind.InstanceConstructor, constructor.Kind);
        Assert.Equal(CallableKind.StaticConstructor, staticConstructor.Kind);
        Assert.Equal(CallableKind.Operator, addition.Kind);
        Assert.Equal(CallableKind.Conversion, conversion.Kind);
    }

    [Fact]
    public void MapCallable_RefKindsAndAsyncFlag_MapExactly()
    {
        var compilation = RoslynTestCompilation.Create();
        var typeSystem = new RoslynTypeSystem(compilation);
        var sample = RequiredType(compilation, "Fixtures.Sample`1");
        var refKinds = typeSystem.Mapper.MapCallable(
            RequiredMethod(sample, "RefKinds"));
        var refReturn = typeSystem.Mapper.MapCallable(
            RequiredMethod(sample, "RefReadonlyReturn"));
        var taskReturning = typeSystem.Mapper.MapCallable(
            RequiredMethod(sample, "TaskReturning"));

        Assert.Collection(
            refKinds.Parameters,
            parameter => Assert.Equal(AspectN.Metadata.RefKind.Ref, parameter.Type.RefKind),
            parameter => Assert.Equal(AspectN.Metadata.RefKind.Out, parameter.Type.RefKind),
            parameter => Assert.Equal(AspectN.Metadata.RefKind.In, parameter.Type.RefKind),
            parameter => Assert.Equal(AspectN.Metadata.RefKind.RefReadOnly, parameter.Type.RefKind));
        Assert.Equal(AspectN.Metadata.RefKind.RefReadOnly, refReturn.ReturnType.RefKind);
        Assert.False(taskReturning.IsAsync);
    }

    [Fact]
    public void MapProperty_AccessorsEventsAndAttributes_MapExactly()
    {
        var compilation = RoslynTestCompilation.Create();
        var typeSystem = new RoslynTypeSystem(compilation);
        var sample = RequiredType(compilation, "Fixtures.Sample`1");
        var name = RequiredProperty(sample, "Name");
        var indexer = RequiredProperty(sample, "Item");
        var property = typeSystem.Mapper.MapProperty(name);
        var mappedIndexer = typeSystem.Mapper.MapProperty(indexer);
        var getter = typeSystem.Mapper.MapCallable(
            Assert.IsAssignableFrom<IMethodSymbol>(name.GetMethod));
        var setter = typeSystem.Mapper.MapCallable(
            Assert.IsAssignableFrom<IMethodSymbol>(name.SetMethod));
        var changed = Assert.IsAssignableFrom<IEventSymbol>(
            Assert.Single(sample.GetMembers("Changed")));
        var adder = typeSystem.Mapper.MapCallable(
            Assert.IsAssignableFrom<IMethodSymbol>(changed.AddMethod));
        var remover = typeSystem.Mapper.MapCallable(
            Assert.IsAssignableFrom<IMethodSymbol>(changed.RemoveMethod));
        var echo = typeSystem.Mapper.MapCallable(
            RequiredMethod(sample, "EchoAsync"));
        var marker = typeSystem.Mapper.MapType(
            RequiredType(compilation, "Fixtures.MarkerAttribute"));

        Assert.Equal("System.String", Name(property.PropertyType.Type));
        Assert.Equal("System.Int32", Name(Assert.Single(mappedIndexer.Parameters).Type.Type));
        Assert.Equal(CallableKind.PropertyGetter, getter.Kind);
        Assert.Equal("Name", getter.AssociatedProperty?.Name);
        Assert.Equal(CallableKind.PropertySetter, setter.Kind);
        Assert.Equal(CallableKind.EventAdder, adder.Kind);
        Assert.Equal(CallableKind.EventRemover, remover.Kind);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.HasDirectAttribute(
                AttributeTarget.ForType(typeSystem.Mapper.MapType(sample)),
                marker).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.HasDirectAttribute(
                AttributeTarget.ForCallable(echo),
                marker).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.HasDirectAttribute(
                AttributeTarget.ForReturn(echo),
                marker).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.HasDirectAttribute(
                AttributeTarget.ForParameter(echo.Parameters[0]),
                marker).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.HasDirectAttribute(
                AttributeTarget.ForProperty(property),
                marker).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.HasDirectAttribute(
                AttributeTarget.ForCallable(getter),
                marker).Status);
    }

    [Fact]
    public void TypeSystem_AssignabilityAndCapabilities_ReturnExpected()
    {
        var compilation = RoslynTestCompilation.Create();
        var typeSystem = new RoslynTypeSystem(compilation);
        var list = RequiredType(
            compilation,
            "System.Collections.Generic.List`1").Construct(
                compilation.GetSpecialType(SpecialType.System_String));
        var enumerable = RequiredType(
            compilation,
            "System.Collections.Generic.IEnumerable`1").Construct(
                compilation.GetSpecialType(SpecialType.System_Object));

        var direct = typeSystem.IsAssignableTo(
            typeSystem.Mapper.MapType(list),
            typeSystem.Mapper.MapType(enumerable));
        var reverse = typeSystem.IsAssignableTo(
            typeSystem.Mapper.MapType(enumerable),
            typeSystem.Mapper.MapType(list));

        Assert.Equal(MatchStatus.Matched, direct.Status);
        Assert.Equal(MatchStatus.NotMatched, reverse.Status);
        Assert.Equal(TypeSystemCapabilities.All, typeSystem.Capabilities);
        Assert.Same(compilation, typeSystem.Compilation);
    }

    [Fact]
    public void IsAssignableTo_LanguageConversions_UseClrAssignability()
    {
        var compilation = RoslynTestCompilation.Create();
        var typeSystem = new RoslynTypeSystem(compilation);
        var intType = compilation.GetSpecialType(SpecialType.System_Int32);
        var longType = compilation.GetSpecialType(SpecialType.System_Int64);
        var objectType = compilation.GetSpecialType(SpecialType.System_Object);
        var stringType = compilation.GetSpecialType(SpecialType.System_String);
        var nullable = RequiredType(compilation, "System.Nullable`1");
        var nullableInt = nullable.Construct(intType);
        var nullableLong = nullable.Construct(longType);
        var sample = RequiredType(compilation, "Fixtures.Sample`1")
            .Construct(intType);
        var array = compilation.CreateArrayTypeSymbol(intType);
        var unqualifiedArray = new NamedTypeSignature(
            new TypeIdentity(
                new AspectN.Metadata.AssemblyIdentity(string.Empty),
                "System",
                "Array",
                0,
                AspectN.Metadata.TypeKind.Class));

        Assert.Equal(
            MatchStatus.Matched,
            Assignability(typeSystem, intType, objectType));
        Assert.Equal(
            MatchStatus.Matched,
            Assignability(typeSystem, intType, nullableInt));
        Assert.Equal(
            MatchStatus.NotMatched,
            Assignability(typeSystem, intType, longType));
        Assert.Equal(
            MatchStatus.NotMatched,
            Assignability(typeSystem, intType, nullableLong));
        Assert.Equal(
            MatchStatus.NotMatched,
            Assignability(typeSystem, sample, stringType));
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(
                typeSystem.Mapper.MapType(array),
                unqualifiedArray).Status);
    }

    [Fact]
    public void AdapterLifetime_ReleasingAdapter_ReleasesCompilationGraph()
    {
        var compilationReference = CreateAndReleaseAdapter();

        for (var attempt = 0;
             attempt < 10 && compilationReference.IsAlive;
             attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.False(compilationReference.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateAndReleaseAdapter()
    {
        var compilation = RoslynTestCompilation.Create();
        var compilationReference = new WeakReference(compilation);
        var typeSystem = new RoslynTypeSystem(compilation);
        var sample = RequiredType(compilation, "Fixtures.Sample`1");

        _ = typeSystem.Mapper.MapType(sample);
        _ = typeSystem.Mapper.MapCallable(
            RequiredMethod(sample, "EchoAsync"));
        _ = typeSystem.Mapper.MapProperty(
            RequiredProperty(sample, "Name"));

        return compilationReference;
    }

    private static MatchStatus Assignability(
        RoslynTypeSystem typeSystem,
        ITypeSymbol candidate,
        ITypeSymbol target)
    {
        return typeSystem.IsAssignableTo(
            typeSystem.Mapper.MapType(candidate),
            typeSystem.Mapper.MapType(target)).Status;
    }

    private static INamedTypeSymbol RequiredType(
        Compilation compilation,
        string metadataName)
    {
        return Assert.IsAssignableFrom<INamedTypeSymbol>(
            compilation.GetTypeByMetadataName(metadataName));
    }

    private static IMethodSymbol RequiredMethod(
        INamedTypeSymbol type,
        string metadataName)
    {
        return Assert.IsAssignableFrom<IMethodSymbol>(
            Assert.Single(
                type.GetMembers(metadataName).OfType<IMethodSymbol>()));
    }

    private static IPropertySymbol RequiredProperty(
        INamedTypeSymbol type,
        string metadataName)
    {
        return Assert.Single(
            type.GetMembers().OfType<IPropertySymbol>(),
            property => property.MetadataName == metadataName);
    }

    private static string Name(TypeSignature type)
    {
        return Assert.IsType<NamedTypeSignature>(type)
            .Identity.FullMetadataName;
    }
}

internal static class RoslynTestCompilation
{
    public const string SOURCE = """
        #nullable enable
        using System;
        using System.Threading.Tasks;

        namespace Fixtures;

        [AttributeUsage(AttributeTargets.All)]
        public sealed class MarkerAttribute : Attribute
        {
        }

        public class Outer<T>
        {
            public class Inner<TKey, TValue>
            {
            }
        }

        [Marker]
        public class Sample<T>
        {
            private int _value;

            static Sample()
            {
            }

            public Sample()
            {
            }

            [Marker]
            [return: Marker]
            public async Task<TMethod> EchoAsync<TMethod>(
                [Marker] T value,
                TMethod other)
            {
                await Task.Yield();
                _value++;
                return other;
            }

            public Task<T> TaskReturning(T value)
            {
                return Task.FromResult(value);
            }

            public void RefKinds(
                ref int first,
                out string second,
                in double third,
                ref readonly long fourth)
            {
                first += (int)third + (int)fourth;
                second = first.ToString();
            }

            public ref readonly int RefReadonlyReturn()
            {
                return ref _value;
            }

            [Marker]
            public string? Name
            {
                [Marker]
                get;
                set;
            }

            public string this[int index] => index.ToString();

            public (string Name, int Count) Pair => default;

            public string Required => string.Empty;

            public string? Optional => null;

            public event EventHandler? Changed;

            public void RaiseChanged()
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }

            public static Sample<T> operator +(
                Sample<T> left,
                Sample<T> right)
            {
                _ = right;
                return left;
            }

            public static implicit operator string(Sample<T> value)
            {
                _ = value;
                return string.Empty;
            }
        }
        """;

    public static CSharpCompilation Create(string source = SOURCE)
    {
        var trustedAssemblies = Assert.IsType<string>(
            AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"));
        var references = trustedAssemblies
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            CSharpParseOptions.Default.WithLanguageVersion(
                LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create(
            $"AspectN.Roslyn.Tests.{Guid.NewGuid():N}",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity ==
                DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(errors);
        return compilation;
    }
}
