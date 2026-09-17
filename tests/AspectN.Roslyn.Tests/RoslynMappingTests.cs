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
    public void MapType_GlobalNamespaceType_UsesEmptyNamespaceInIdentity()
    {
        var compilation = RoslynTestCompilation.Create(
            "public class GlobalFixture { }");
        var typeSystem = new RoslynTypeSystem(compilation);
        var symbol = compilation.GetTypeByMetadataName("GlobalFixture");

        Assert.NotNull(symbol);
        var mapped = Assert.IsType<NamedTypeSignature>(
            typeSystem.Mapper.MapType(symbol!));
        Assert.Equal(string.Empty, mapped.Identity.Namespace);
        Assert.Equal("GlobalFixture", mapped.Identity.FullMetadataName);
    }

    [Fact]
    public void MapType_NestedConstructedOverloads_HaveDistinctOwnerIds()
    {
        var compilation = RoslynTestCompilation.Create("""
            public class Outer<T>
            {
                public class Inner
                {
                }
            }

            public class Owner
            {
                public void M<T>(Outer<int>.Inner value) { }

                public void M<T>(Outer<string>.Inner value) { }
            }
            """);
        var typeSystem = new RoslynTypeSystem(compilation);
        var owner = compilation.GetTypeByMetadataName("Owner");
        Assert.NotNull(owner);
        var overloads = owner.GetMembers("M").OfType<IMethodSymbol>().ToArray();
        var intOverload = Assert.Single(
            overloads,
            method => method.Parameters[0].Type.ToDisplayString()
                .Contains("Outer<int>"));
        var stringOverload = Assert.Single(
            overloads,
            method => method.Parameters[0].Type.ToDisplayString()
                .Contains("Outer<string>"));

        var first = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(intOverload.TypeParameters[0]));
        var second = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(stringOverload.TypeParameters[0]));

        // 嵌套类型的构造实参按包含链逐层编入 owner ID，与 Reflection/Cecil 逐字节一致。
        Assert.Equal("Owner.M`1(Outer`1<System.Int32>/Inner)", first.Parameter.OwnerId);
        Assert.Equal(
            "Owner.M`1(Outer`1<System.String>/Inner)",
            second.Parameter.OwnerId);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.AreEqual(first, second).Status);
    }

    [Fact]
    public void MapType_NestedTypeParameterSignatures_UseFlattenedPositions()
    {
        var compilation = RoslynTestCompilation.Create("""
            public class Outer<T>
            {
                public class Inner<U>
                {
                }
            }
            """);
        var typeSystem = new RoslynTypeSystem(compilation);
        var outer = compilation.GetTypeByMetadataName("Outer`1");
        var inner = compilation.GetTypeByMetadataName("Outer`1+Inner`1");
        Assert.NotNull(outer);
        Assert.NotNull(inner);
        var mappedT = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(outer.TypeParameters[0]));
        var mappedU = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(inner.TypeParameters[0]));

        // 实际签名的 Position 也使用包含链扁平序号（T=0、U=1），
        // 与 Reflection 的 GenericParameterPosition 及 Cecil 的 ECMA 编号一致，
        // 保证跨适配器结构身份比较逐字段相等。
        Assert.Equal(GenericParameterOwnerKind.Type, mappedT.Parameter.OwnerKind);
        Assert.Equal("Outer`1", mappedT.Parameter.OwnerId);
        Assert.Equal(0, mappedT.Parameter.Position);
        Assert.Equal(GenericParameterOwnerKind.Type, mappedU.Parameter.OwnerKind);
        Assert.Equal("Outer`1/Inner`1", mappedU.Parameter.OwnerId);
        Assert.Equal(1, mappedU.Parameter.Position);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.AreEqual(mappedT, mappedU).Status);
    }

    [Fact]
    public void MapCallable_OuterParameterUsedInNestedMember_KeepsOuterDeclarationOwner()
    {
        var compilation = RoslynTestCompilation.Create("""
            public class Outer<T>
            {
                public class Inner<U>
                {
                    public void M<V>(T x) { }

                    public void M<V>(U x) { }
                }
            }
            """);
        var typeSystem = new RoslynTypeSystem(compilation);
        var outer = compilation.GetTypeByMetadataName("Outer`1");
        var inner = compilation.GetTypeByMetadataName("Outer`1+Inner`1");
        Assert.NotNull(outer);
        Assert.NotNull(inner);
        var tOverload = Assert.Single(
            inner.GetMembers("M").OfType<IMethodSymbol>(),
            method =>
                method.Parameters[0].Type is ITypeParameterSymbol parameter &&
                parameter.Name == "T");
        var mappedT = Assert.IsType<GenericParameterTypeSignature>(
            Assert.Single(typeSystem.Mapper.MapCallable(tOverload).Parameters)
                .Type
                .Type);
        var declaredT = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(outer.TypeParameters[0]));

        // Roslyn 符号天然是声明视图：内层成员使用的外层 T 仍归属 Outer，
        // 与声明处映射逐字段一致；OwnerAssembly 携带程序集身份供跨程序集区分。
        Assert.Equal(GenericParameterOwnerKind.Type, mappedT.Parameter.OwnerKind);
        Assert.Equal("Outer`1", mappedT.Parameter.OwnerId);
        Assert.Equal(0, mappedT.Parameter.Position);
        Assert.Equal(
            compilation.Assembly.Name,
            mappedT.Parameter.OwnerAssembly.Name);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.AreEqual(mappedT, declaredT).Status);
    }

    [Fact]
    public void MapType_NestedTypeParameterOverloads_HaveFlattenedOwnerIds()
    {
        var compilation = RoslynTestCompilation.Create("""
            public class Outer<T>
            {
                public class Inner<U>
                {
                    public void M<V>(T x) { }

                    public void M<V>(U x) { }
                }
            }
            """);
        var typeSystem = new RoslynTypeSystem(compilation);
        var inner = compilation.GetTypeByMetadataName("Outer`1+Inner`1");
        Assert.NotNull(inner);
        var overloads = inner.GetMembers("M").OfType<IMethodSymbol>().ToArray();
        Assert.Equal(2, overloads.Length);

        var mapped = overloads
            .Select(method => Assert.IsType<GenericParameterTypeSignature>(
                typeSystem.Mapper.MapType(method.TypeParameters[0])))
            .ToArray();
        var ownerIds = mapped
            .Select(signature => signature.Parameter.OwnerId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        // Roslyn 的 Ordinal 相对声明类型，必须加包含链元数偏移，
        // 与 Reflection/Cecil 的 ECMA 扁平编号逐字节一致（T=!0、U=!1）。
        Assert.Equal("Outer`1/Inner`1.M`1(!0)", ownerIds[0]);
        Assert.Equal("Outer`1/Inner`1.M`1(!1)", ownerIds[1]);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.AreEqual(mapped[0], mapped[1]).Status);
    }

    [Fact]
    public void MapType_RefOverloadGenericParameters_HaveDistinctOwnerIds()
    {
        var compilation = RoslynTestCompilation.Create();
        var typeSystem = new RoslynTypeSystem(compilation);
        var probe = RequiredType(compilation, "Fixtures.OwnerProbe");
        var overloads = probe.GetMembers("M").OfType<IMethodSymbol>().ToArray();
        var valueOverload = Assert.Single(
            overloads,
            method => method.Parameters.Length == 1 &&
                method.Parameters[0].RefKind ==
                    Microsoft.CodeAnalysis.RefKind.None);
        var refOverload = Assert.Single(
            overloads,
            method => method.Parameters.Length == 1 &&
                method.Parameters[0].RefKind ==
                    Microsoft.CodeAnalysis.RefKind.Ref);

        var first = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(valueOverload.TypeParameters[0]));
        var second = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(refOverload.TypeParameters[0]));

        // ref 方式以 & 后缀编入规范身份，与 Reflection/Cecil 的元数据形状一致。
        Assert.Equal("Fixtures.OwnerProbe.M`1(!!0)", first.Parameter.OwnerId);
        Assert.Equal("Fixtures.OwnerProbe.M`1(!!0&)", second.Parameter.OwnerId);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.AreEqual(first, second).Status);
    }

    [Fact]
    public void MapProperty_RefReturningProperties_PreserveRefKind()
    {
        var compilation = RoslynTestCompilation.Create();
        var typeSystem = new RoslynTypeSystem(compilation);
        var sample = RequiredType(compilation, "Fixtures.Sample`1");

        var refValue = typeSystem.Mapper.MapProperty(
            RequiredProperty(sample, "RefValue"));
        var refReadonly = typeSystem.Mapper.MapProperty(
            RequiredProperty(sample, "RefReadonlyValue"));

        Assert.Equal(AspectN.Metadata.RefKind.Ref, refValue.PropertyType.RefKind);
        Assert.Equal(AspectN.Metadata.RefKind.RefReadOnly, refReadonly.PropertyType.RefKind);
    }

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
        var enumerableOfObject = RequiredType(
            compilation,
            "System.Collections.Generic.IEnumerable`1").Construct(
                compilation.GetSpecialType(SpecialType.System_Object));
        var enumerableOfString = RequiredType(
            compilation,
            "System.Collections.Generic.IEnumerable`1").Construct(
                compilation.GetSpecialType(SpecialType.System_String));

        // + 只含名义继承/实现：声明的接口实例化可赋值，变型实例化不可。
        var direct = typeSystem.IsAssignableTo(
            typeSystem.Mapper.MapType(list),
            typeSystem.Mapper.MapType(enumerableOfObject));
        var nominal = typeSystem.IsAssignableTo(
            typeSystem.Mapper.MapType(list),
            typeSystem.Mapper.MapType(enumerableOfString));
        var reverse = typeSystem.IsAssignableTo(
            typeSystem.Mapper.MapType(enumerableOfString),
            typeSystem.Mapper.MapType(list));

        Assert.Equal(MatchStatus.NotMatched, direct.Status);
        Assert.Equal(MatchStatus.Matched, nominal.Status);
        Assert.Equal(MatchStatus.NotMatched, reverse.Status);
        Assert.Equal(TypeSystemCapabilities.All, typeSystem.Capabilities);
        Assert.Same(compilation, typeSystem.Compilation);
    }

    [Fact]
    public void IsAssignableTo_LanguageConversions_UseDeclaredInheritance()
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
        // Nullable 提升不是名义继承/实现关系。
        Assert.Equal(
            MatchStatus.NotMatched,
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

    [Fact]
    public void MapType_FunctionPointerParameterOverloads_HaveUnifiedOwnerIds()
    {
        var compilation = RoslynTestCompilation.Create("""
            public static unsafe class FunctionPointerProbe
            {
                public static TOutput Convert<TOutput>(
                    delegate*<int, TOutput> parser,
                    int value)
                {
                    return parser(value);
                }

                public static TOutput Convert<TOutput>(
                    delegate* unmanaged[Cdecl]<int, TOutput> parser,
                    int value)
                {
                    return parser(value);
                }
            }
            """, allowUnsafe: true);
        var typeSystem = new RoslynTypeSystem(compilation);
        var probe = compilation.GetTypeByMetadataName("FunctionPointerProbe");
        Assert.NotNull(probe);
        var overloads = probe.GetMembers("Convert").OfType<IMethodSymbol>().ToArray();
        var cdecl = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(Assert.Single(
                    overloads,
                    method => method.Parameters[0].Type.ToDisplayString()
                        .Contains("Cdecl", StringComparison.Ordinal))
                .TypeParameters[0]));
        var managed = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(Assert.Single(
                    overloads,
                    method => !method.Parameters[0].Type.ToDisplayString()
                        .Contains("Cdecl", StringComparison.Ordinal))
                .TypeParameters[0]));

        // 与 Cecil/Reflection 逐字节一致的统一文本；仅调用约定不同的重载必须区分。
        Assert.Equal(
            "FunctionPointerProbe.Convert`1(" +
            "delegate*[cdecl]<System.Int32,!!0>,System.Int32)",
            cdecl.Parameter.OwnerId);
        Assert.Equal(
            "FunctionPointerProbe.Convert`1(" +
            "delegate*[managed]<System.Int32,!!0>,System.Int32)",
            managed.Parameter.OwnerId);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.AreEqual(cdecl, managed).Status);
    }

    [Fact]
    public void MapType_FunctionPointerRefReturnOverloads_HaveDistinctOwnerIds()
    {
        var compilation = RoslynTestCompilation.Create("""
            public static unsafe class RefReturnProbe
            {
                public static TOutput Convert<TOutput>(
                    delegate*<ref int, TOutput> parser,
                    int value)
                {
                    return parser(ref value);
                }

                public static TOutput Convert<TOutput>(
                    delegate*<ref int, ref readonly TOutput> parser,
                    int value)
                {
                    return parser(ref value);
                }
            }
            """, allowUnsafe: true);
        var typeSystem = new RoslynTypeSystem(compilation);
        var probe = compilation.GetTypeByMetadataName("RefReturnProbe");
        Assert.NotNull(probe);
        var overloads = probe.GetMembers("Convert").OfType<IMethodSymbol>().ToArray();
        Assert.Equal(2, overloads.Length);
        var refReadonlyReturn = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(overloads
                .Single(method => method.Parameters[0].Type.ToDisplayString()
                    .Contains("ref readonly", StringComparison.Ordinal))
                .TypeParameters[0]));
        var plainReturn = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(overloads
                .Single(method => !method.Parameters[0].Type.ToDisplayString()
                    .Contains("ref readonly", StringComparison.Ordinal))
                .TypeParameters[0]));

        // ref readonly 返回在符号上报 ReturnsByRef=false/RefKind=In，& 的判定必须走
        // RefKind；只读性不同的重载不得折叠，且文本与 Cecil 一致（!!0& 带 &）。
        Assert.Equal(
            "RefReturnProbe.Convert`1(" +
            "delegate*[managed]<System.Int32&,!!0&>,System.Int32)",
            refReadonlyReturn.Parameter.OwnerId);
        Assert.Equal(
            "RefReturnProbe.Convert`1(" +
            "delegate*[managed]<System.Int32&,!!0>,System.Int32)",
            plainReturn.Parameter.OwnerId);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.AreEqual(refReadonlyReturn, plainReturn).Status);
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

        public class OwnerProbe
        {
            public void M<T>(T value)
            {
            }

            public void M<T>(ref T value)
            {
                _ = value;
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

            public ref int RefValue => ref _value;

            public ref readonly int RefReadonlyValue => ref _value;

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

    public static CSharpCompilation Create(
        string source = SOURCE,
        bool allowUnsafe = false)
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
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: allowUnsafe));
        var errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity ==
                DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(errors);
        return compilation;
    }
}
