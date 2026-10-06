using static AspectN.Tests.Shared.OwnerIdentityFixture;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using AspectN.Diagnostics;
using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;
using Xunit;

namespace AspectN.Reflection.Tests;

public sealed class ReflectionMappingTests
{
    [Fact]
    public void MapType_NestedConstructedArrayAndGenericParameters_PreserveShape()
    {
        var typeSystem = new ReflectionTypeSystem();
        var definition = Assert.IsType<NamedTypeSignature>(
            typeSystem.Mapper.MapType(
                typeof(ReflectionOuter<>.Inner<,>)));
        var constructed = Assert.IsType<NamedTypeSignature>(
            typeSystem.Mapper.MapType(
                typeof(ReflectionOuter<int>.Inner<string, double>)));
        var outerArray = Assert.IsType<ArrayTypeSignature>(
            typeSystem.Mapper.MapType(typeof(int[,][])));
        var innerArray = Assert.IsType<ArrayTypeSignature>(
            outerArray.ElementType);
        var typeParameter = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(
                typeof(ReflectionFixture<>).GetGenericArguments()[0]));
        var method = RequiredMethod(
            typeof(ReflectionFixture<>),
            nameof(ReflectionFixture<object>.Echo));
        var methodParameter = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(method.GetGenericArguments()[0]));

        Assert.Equal(
            $"{typeof(ReflectionOuter<>).Namespace}." +
            "ReflectionOuter`1/Inner`2",
            definition.Identity.FullMetadataName);
        Assert.Equal(3, constructed.TypeArguments.Count);
        Assert.NotNull(constructed.OriginalDefinition);
        Assert.Equal(2, outerArray.Rank);
        Assert.Equal(1, innerArray.Rank);
        Assert.Equal(GenericParameterOwnerKind.Type, typeParameter.Parameter.OwnerKind);
        Assert.Equal(0, typeParameter.Parameter.Position);
        Assert.Equal(
            GenericParameterOwnerKind.Method,
            methodParameter.Parameter.OwnerKind);
        Assert.EndsWith(
            ".Echo`1(!0,!!0)",
            methodParameter.Parameter.OwnerId,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(typeof(string), TypeKind.Class)]
    [InlineData(typeof(int), TypeKind.Struct)]
    [InlineData(typeof(IDisposable), TypeKind.Interface)]
    [InlineData(typeof(DayOfWeek), TypeKind.Enum)]
    [InlineData(typeof(Action), TypeKind.Delegate)]
    public void MapType_RuntimeKindsAndCache_ReturnExpected(
        Type type,
        TypeKind expectedKind)
    {
        var typeSystem = new ReflectionTypeSystem();

        var first = Assert.IsType<NamedTypeSignature>(
            typeSystem.Mapper.MapType(type));
        var second = typeSystem.Mapper.MapType(type);

        Assert.Equal(expectedKind, first.Identity.Kind);
        Assert.Same(first, second);
    }

    [Fact]
    public void MapMethod_DefinitionConstructedMethodAndConstructors_MapShape()
    {
        var typeSystem = new ReflectionTypeSystem();
        var definition = RequiredMethod(
            typeof(ReflectionFixture<>),
            nameof(ReflectionFixture<object>.Echo));
        var constructed = RequiredMethod(
                typeof(ReflectionFixture<string>),
                nameof(ReflectionFixture<object>.Echo))
            .MakeGenericMethod(typeof(int));
        var definitionSignature = typeSystem.Mapper.MapMethod(definition);
        var signature = typeSystem.Mapper.MapMethod(constructed);
        var constructor = typeSystem.Mapper.MapMethod(
            Assert.Single(
                typeof(ReflectionFixture<string>).GetConstructors(
                    BindingFlags.Public |
                    BindingFlags.Instance)));
        var typeInitializer = typeSystem.Mapper.MapMethod(
            Assert.IsAssignableFrom<ConstructorInfo>(
                typeof(ReflectionFixture<string>).TypeInitializer));

        Assert.Single(definitionSignature.GenericParameters);
        Assert.Empty(definitionSignature.GenericArguments);
        Assert.Empty(signature.GenericParameters);
        Assert.Equal("System.Int32", Name(Assert.Single(signature.GenericArguments)));
        Assert.Equal("System.Int32", Name(signature.ReturnType.Type));
        Assert.Collection(
            signature.Parameters,
            parameter => Assert.Equal("System.String", Name(parameter.Type.Type)),
            parameter => Assert.Equal("System.Int32", Name(parameter.Type.Type)));
        Assert.NotNull(signature.OriginalDefinition);
        Assert.Equal(MethodKind.InstanceConstructor, constructor.Kind);
        Assert.Equal(MethodKind.StaticConstructor, typeInitializer.Kind);
    }

    [Fact]
    public void MapMethod_RefKindsAsyncAndSpecialMethods_MapSemantics()
    {
        var typeSystem = new ReflectionTypeSystem();
        var type = typeof(ReflectionFixture<int>);
        var refKinds = typeSystem.Mapper.MapMethod(
            RequiredMethod(type, nameof(ReflectionFixture<int>.RefKinds)));
        var refReturn = typeSystem.Mapper.MapMethod(
            RequiredMethod(type, nameof(ReflectionFixture<int>.RefReadonlyReturn)));
        var asyncMethod = typeSystem.Mapper.MapMethod(
            RequiredMethod(type, nameof(ReflectionFixture<int>.LoadAsync)));
        var taskReturning = typeSystem.Mapper.MapMethod(
            RequiredMethod(type, nameof(ReflectionFixture<int>.TaskReturning)));
        var addition = typeSystem.Mapper.MapMethod(
            RequiredMethod(type, "op_Addition"));
        var conversion = typeSystem.Mapper.MapMethod(
            RequiredMethod(type, "op_Implicit"));

        Assert.Collection(
            refKinds.Parameters,
            parameter => Assert.Equal(RefKind.Ref, parameter.Type.RefKind),
            parameter => Assert.Equal(RefKind.Out, parameter.Type.RefKind),
            parameter => Assert.Equal(RefKind.In, parameter.Type.RefKind),
            parameter => Assert.Equal(RefKind.RefReadOnly, parameter.Type.RefKind));
        Assert.Equal(RefKind.RefReadOnly, refReturn.ReturnType.RefKind);
        Assert.True(asyncMethod.IsAsync);
        Assert.False(taskReturning.IsAsync);
        Assert.Equal(MethodKind.Operator, addition.Kind);
        Assert.Equal(MethodKind.Conversion, conversion.Kind);
    }

    [Fact]
    public void MapProperty_AccessorsIndexerAndEvents_MapShape()
    {
        var typeSystem = new ReflectionTypeSystem();
        var type = typeof(ReflectionFixture<int>);
        var name = Assert.IsAssignableFrom<PropertyInfo>(
            type.GetProperty(nameof(ReflectionFixture<int>.Name)));
        var indexer = Assert.IsAssignableFrom<PropertyInfo>(
            type.GetProperty("Item"));
        var property = typeSystem.Mapper.MapProperty(name);
        var mappedIndexer = typeSystem.Mapper.MapProperty(indexer);
        var getter = typeSystem.Mapper.MapMethod(
            Assert.IsAssignableFrom<MethodInfo>(name.GetMethod));
        var setter = typeSystem.Mapper.MapMethod(
            Assert.IsAssignableFrom<MethodInfo>(name.SetMethod));
        var @event = Assert.IsAssignableFrom<EventInfo>(
            type.GetEvent(nameof(ReflectionFixture<int>.Changed)));
        var adder = typeSystem.Mapper.MapMethod(
            Assert.IsAssignableFrom<MethodInfo>(@event.AddMethod));
        var remover = typeSystem.Mapper.MapMethod(
            Assert.IsAssignableFrom<MethodInfo>(@event.RemoveMethod));

        Assert.Equal("System.String", Name(property.PropertyType.Type));
        Assert.Equal("System.Int32", Name(Assert.Single(mappedIndexer.Parameters).Type.Type));
        Assert.Equal(MethodKind.PropertyGetter, getter.Kind);
        Assert.Equal("Name", getter.AssociatedProperty?.Name);
        Assert.Equal(MethodKind.PropertySetter, setter.Kind);
        Assert.Equal("Name", setter.AssociatedProperty?.Name);
        Assert.Equal(MethodKind.EventAdder, adder.Kind);
        Assert.Equal(MethodKind.EventRemover, remover.Kind);
    }

    [Fact]
    public void MapAndLookupAttributes_UsesCustomAttributeDataWithoutInstantiation()
    {
        var typeSystem = new ReflectionTypeSystem();
        var type = typeof(ReflectionFixture<>);
        var method = RequiredMethod(type, nameof(ReflectionFixture<object>.Echo));
        var property = Assert.IsAssignableFrom<PropertyInfo>(
            type.GetProperty(nameof(ReflectionFixture<object>.Name)));
        var callable = typeSystem.Mapper.MapMethod(method);
        var mappedProperty = typeSystem.Mapper.MapProperty(property);
        var getter = typeSystem.Mapper.MapMethod(
            Assert.IsAssignableFrom<MethodInfo>(property.GetMethod));
        var attributeType = typeSystem.Mapper.MapType(
            typeof(ExplosiveMarkerAttribute));

        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.HasDirectAttribute(
                AttributeTarget.ForType(typeSystem.Mapper.MapType(type)),
                attributeType).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.HasDirectAttribute(
                AttributeTarget.ForMethod(callable),
                attributeType).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.HasDirectAttribute(
                AttributeTarget.ForReturn(callable),
                attributeType).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.HasDirectAttribute(
                AttributeTarget.ForParameter(callable.Parameters[0]),
                attributeType).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.HasDirectAttribute(
                AttributeTarget.ForProperty(mappedProperty),
                attributeType).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.HasDirectAttribute(
                AttributeTarget.ForMethod(getter),
                attributeType).Status);
    }

    [Fact]
    public void MapAndLookupAttributes_GenericAttributeType_PreservesTypeArguments()
    {
        var typeSystem = new ReflectionTypeSystem();
        var method = RequiredMethod(
            typeof(ReflectionFixture<int>),
            nameof(ReflectionFixture<object>.GenericAttributed));
        var callable = typeSystem.Mapper.MapMethod(method);
        var intMarker = typeSystem.Mapper.MapType(
            typeof(GenericMarkerAttribute<int>));
        var stringMarker = typeSystem.Mapper.MapType(
            typeof(GenericMarkerAttribute<string>));

        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.HasDirectAttribute(
                AttributeTarget.ForMethod(callable),
                intMarker).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.HasDirectAttribute(
                AttributeTarget.ForMethod(callable),
                stringMarker).Status);
        Assert.Equal(
            MatchStatus.Matched,
            MemberPattern(
                "attr(call GenericMarkerAttribute<int>)",
                callable,
                typeSystem).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            MemberPattern(
                "attr(call GenericMarkerAttribute<string>)",
                callable,
                typeSystem).Status);
    }

    [Fact]
    public void MemberPattern_NestedGenericAttributeFallback_MatchesOpenNestedArgument()
    {
        var typeSystem = new ReflectionTypeSystem();
        var callable = typeSystem.Mapper.MapMethod(
            RequiredMethod(
                typeof(ReflectionFixture<int>),
                nameof(ReflectionFixture<object>.NestedGenericAttributed)));

        Assert.Equal(
            MatchStatus.Matched,
            MemberPattern(
                "attr(call GenericMarkerAttribute<System.Collections.Generic.List<>>)",
                callable,
                typeSystem).Status);
        Assert.Equal(
            MatchStatus.Matched,
            MemberPattern(
                "attr(call GenericMarkerAttribute<System.Collections.Generic.List<int>>)",
                callable,
                typeSystem).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            MemberPattern(
                "attr(call GenericMarkerAttribute<System.Collections.Generic.List<string>>)",
                callable,
                typeSystem).Status);
    }

    [Fact]
    public void MemberPattern_NullableReferenceAttributeArgument_MatchesPlainReferenceType()
    {
        var typeSystem = new ReflectionTypeSystem();
        var callable = typeSystem.Mapper.MapMethod(
            RequiredMethod(
                typeof(ReflectionFixture<int>),
                nameof(ReflectionFixture<object>.NullableAttributed)));

        Assert.Equal(
            MatchStatus.Matched,
            MemberPattern(
                "attr(call GenericMarkerAttribute<string?>)",
                callable,
                typeSystem).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            MemberPattern(
                "attr(call GenericMarkerAttribute<int?>)",
                callable,
                typeSystem).Status);
    }

    [Fact]
    public void MapType_OverloadedMethodGenericParameters_HaveDistinctOwnerIds()
    {
        var typeSystem = new ReflectionTypeSystem();
        var type = typeof(ReflectionFixture<int>);
        var single = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "SameNameOverload" &&
                method.GetParameters().Length == 1);
        var doubleParameter = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "SameNameOverload" &&
                method.GetParameters().Length == 2);
        var first = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(single.GetGenericArguments()[0]));
        var second = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(doubleParameter.GetGenericArguments()[0]));

        Assert.NotEqual(first.Parameter.OwnerId, second.Parameter.OwnerId);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.AreEqual(first, second).Status);
    }

    [Fact]
    public void MapType_NestedConstructedOverloads_HaveDistinctOwnerIds()
    {
        var typeSystem = new ReflectionTypeSystem();
        var owner = typeof(NestedOwner);
        var intOverload = owner.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "M" &&
                method.GetParameters()[0].ParameterType ==
                    typeof(NestedOuter<int>.NestedInner));
        var stringOverload = owner.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "M" &&
                method.GetParameters()[0].ParameterType ==
                    typeof(NestedOuter<string>.NestedInner));
        var first = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(intOverload.GetGenericArguments()[0]));
        var second = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(stringOverload.GetGenericArguments()[0]));

        // 嵌套类型的构造实参按包含链逐层编入 owner ID。
        Assert.Equal(
            RuntimeType(
                $"AspectN.Reflection.Tests.NestedOuter`1<{FrameworkType("System.Int32")}>/NestedInner", typeof(NestedOuter<>)),
            ExtractParameterForm(first.Parameter.OwnerId));
        Assert.Equal(
            RuntimeType(
                $"AspectN.Reflection.Tests.NestedOuter`1<{FrameworkType("System.String")}>/NestedInner", typeof(NestedOuter<>)),
            ExtractParameterForm(second.Parameter.OwnerId));
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.AreEqual(first, second).Status);
    }

    private static string ExtractParameterForm(string ownerId)
    {
        var open = ownerId.IndexOf('(');
        return ownerId[(open + 1)..^1];
    }

    [Fact]
    public void MapProperty_RefReturningProperties_PreserveRefKind()
    {
        var typeSystem = new ReflectionTypeSystem();
        var type = typeof(ReflectionFixture<int>);
        var refValue = typeSystem.Mapper.MapProperty(
            Assert.IsAssignableFrom<PropertyInfo>(
                type.GetProperty(nameof(ReflectionFixture<object>.RefValue))));
        var refReadonly = typeSystem.Mapper.MapProperty(
            Assert.IsAssignableFrom<PropertyInfo>(
                type.GetProperty(nameof(ReflectionFixture<object>.RefReadonlyValue))));

        Assert.Equal(RefKind.Ref, refValue.PropertyType.RefKind);
        Assert.Equal(RefKind.RefReadOnly, refReadonly.PropertyType.RefKind);
        Assert.Equal(
            "System.Int32",
            Assert.IsType<NamedTypeSignature>(refValue.PropertyType.Type)
                .Identity.FullMetadataName);
    }

    [Fact]
    public void TypeSystem_AssignabilityAndUnqualifiedPattern_ReturnExpected()
    {
        var typeSystem = new ReflectionTypeSystem();
        var candidate = typeSystem.Mapper.MapType(typeof(List<string>));
        var target = typeSystem.Mapper.MapType(typeof(IEnumerable<string>));
        var reverse = typeSystem.IsAssignableTo(target, candidate);
        var direct = typeSystem.IsAssignableTo(candidate, target);
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseTypePattern(
                "System.Collections.Generic.IEnumerable<string>+"));
        Assert.True(compilation.TryGetTypePattern(out var pattern));

        var patternResult = pattern!.Match(candidate, typeSystem);

        Assert.Equal(MatchStatus.Matched, direct.Status);
        Assert.Equal(MatchStatus.NotMatched, reverse.Status);
        Assert.Equal(MatchStatus.Matched, patternResult.Status);
        Assert.Equal(TypeSystemCapabilities.All, typeSystem.Capabilities);
    }

    [Fact]
    public void IsAssignableTo_VarianceInstantiation_ReturnsNotMatched()
    {
        var typeSystem = new ReflectionTypeSystem();
        var candidate = typeSystem.Mapper.MapType(typeof(List<string>));
        var target = typeSystem.Mapper.MapType(typeof(IEnumerable<object>));

        var result = typeSystem.IsAssignableTo(candidate, target);

        Assert.Equal(MatchStatus.NotMatched, result.Status);
    }

    [Fact]
    public void IsAssignableTo_MetadataFailure_ReturnsCapabilityDiagnostic()
    {
        var typeSystem = new ReflectionTypeSystem();
        var nativeType = new FailingMetadataType(
            typeof(ReflectionFixture<int>));
        var candidate = typeSystem.Mapper.MapType(nativeType);
        var target = new NamedTypeSignature(
            new TypeIdentity(
                new AssemblyIdentity(string.Empty),
                "Missing",
                "Target",
                0,
                TypeKind.Class));

        var result = typeSystem.IsAssignableTo(candidate, target);

        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code ==
                DiagnosticCodes.MissingBackendCapability);
    }

    [Fact]
    public void TypeSystem_Assignability_UsesDeclaredInheritance()
    {
        var typeSystem = new ReflectionTypeSystem();

        // + 只含名义继承/实现关系；变型实例化（数组协变、泛型协变/逆变）不可赋值。
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(
                typeSystem.Mapper.MapType(typeof(List<string>)),
                typeSystem.Mapper.MapType(typeof(IEnumerable<string>))).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(
                typeSystem.Mapper.MapType(typeof(List<string>)),
                typeSystem.Mapper.MapType(typeof(IEnumerable<object>))).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(
                typeSystem.Mapper.MapType(typeof(IEnumerable<string>)),
                typeSystem.Mapper.MapType(typeof(IEnumerable<object>))).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(
                typeSystem.Mapper.MapType(typeof(Action<object>)),
                typeSystem.Mapper.MapType(typeof(Action<string>))).Status);
    }

    [Fact]
    public void TypePattern_CompositionalPlus_ExpressesVariance()
    {
        var typeSystem = new ReflectionTypeSystem();

        // 变型经类型实参/元素位置的 + 组合表达（规范 12.4）。
        Assert.Equal(
            MatchStatus.Matched,
            TypePattern("PatternBase+[]", typeof(PatternDerived[]), typeSystem).Status);
        Assert.Equal(
            MatchStatus.Matched,
            TypePattern(
                "System.Collections.Generic.IEnumerable<object+>",
                typeof(IEnumerable<string>),
                typeSystem).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            TypePattern(
                "System.Collections.Generic.IEnumerable<object>+",
                typeof(IEnumerable<string>),
                typeSystem).Status);
    }

    [Fact]
    public void TypePattern_WildcardAssignable_MatchesInheritanceClosure()
    {
        var typeSystem = new ReflectionTypeSystem();

        // 名称通配与命名空间 .. 的 + 目标沿闭包用模式本身求值。
        Assert.Equal(
            MatchStatus.Matched,
            TypePattern("System.Exception+", typeof(InvalidOperationException), typeSystem).Status);
        Assert.Equal(
            MatchStatus.Matched,
            TypePattern("*Exception+", typeof(InvalidOperationException), typeSystem).Status);
        Assert.Equal(
            MatchStatus.Matched,
            TypePattern("System..Exception+", typeof(InvalidOperationException), typeSystem).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            TypePattern("*List+", typeof(InvalidOperationException), typeSystem).Status);
        // string 的闭包（object、IEnumerable、IComparable...）经通配命中 IComparable。
        Assert.Equal(
            MatchStatus.Matched,
            TypePattern("*Comparable*+", typeof(string), typeSystem).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            TypePattern("*Exception+", typeof(string), typeSystem).Status);
    }

    [Fact]
    public void TypePattern_UnqualifiedArrayTargets_MatchSyntheticRelations()
    {
        var typeSystem = new ReflectionTypeSystem();

        // 非限定目标名（空命名空间）对数组合成关系同样成立。
        Assert.Equal(
            MatchStatus.Matched,
            TypePattern("Array+", typeof(string[]), typeSystem).Status);
        Assert.Equal(
            MatchStatus.Matched,
            TypePattern(
                "System.Collections.Generic.IEnumerable<string>+",
                typeof(string[]),
                typeSystem).Status);
    }

    [Fact]
    public void MapType_OpenConstructedOverloads_HaveDistinctOwnerIds()
    {
        var typeSystem = new ReflectionTypeSystem();
        var owner = typeof(OpenConstructedOwner);
        var overloads = owner.GetMethods(BindingFlags.Public | BindingFlags.Static);
        var intOverload = overloads.Single(
            method => method.Name == "M" &&
                method.GetParameters()[0].ParameterType.GetGenericArguments()[1] ==
                    typeof(int));
        var stringOverload = overloads.Single(
            method => method.Name == "M" &&
                method.GetParameters()[0].ParameterType.GetGenericArguments()[1] ==
                    typeof(string));
        var first = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(intOverload.GetGenericArguments()[0]));
        var second = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(stringOverload.GetGenericArguments()[0]));

        // 开放构造类型（含方法泛型参数）保留实参，占位参数用规范形式 !!0 表示。
        Assert.Equal(
            "AspectN.Reflection.Tests.OpenConstructedOwner.M`1(" +
                $"{FrameworkType($"System.Collections.Generic.Dictionary`2<!!0,{FrameworkType("System.Int32")}>")})",
            first.Parameter.OwnerId);
        Assert.Equal(
            "AspectN.Reflection.Tests.OpenConstructedOwner.M`1(" +
                $"{FrameworkType($"System.Collections.Generic.Dictionary`2<!!0,{FrameworkType("System.String")}>")})",
            second.Parameter.OwnerId);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.AreEqual(first, second).Status);
    }

    [Fact]
    public void MapType_NestedTypeParameterOverloads_HaveFlattenedOwnerIds()
    {
        var typeSystem = new ReflectionTypeSystem();
        var inner = typeof(NestedParamOuter<int>.NestedParamInner<string>);
        var overloads = inner.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == "M")
            .ToArray();
        var intOverload = overloads.Single(
            method => method.GetParameters()[0].ParameterType == typeof(int));
        var stringOverload = overloads.Single(
            method => method.GetParameters()[0].ParameterType == typeof(string));
        var first = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(intOverload.GetGenericArguments()[0]));
        var second = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(stringOverload.GetGenericArguments()[0]));

        // .NET 的 GenericParameterPosition 是包含链扁平编号（T=0、U=1），
        // owner ID 与 Cecil/Roslyn 的规范形式逐字节一致。
        Assert.Equal(
            "AspectN.Reflection.Tests.NestedParamOuter`1/NestedParamInner`1.M`1(!0)",
            first.Parameter.OwnerId);
        Assert.Equal(
            "AspectN.Reflection.Tests.NestedParamOuter`1/NestedParamInner`1.M`1(!1)",
            second.Parameter.OwnerId);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.AreEqual(first, second).Status);
    }

    [Fact]
    public void TypePattern_FixedArityAssignable_MatchesAnyArguments()
    {
        var typeSystem = new ReflectionTypeSystem();

        // 规范 13.1：Name<> 表示一个任意实参；固定元数但未指定实参的形式必须回退闭包模式求值，
        // 不能被当作零实参的具体目标（此前 <>+ 错误返回 NotMatched）。
        Assert.Equal(
            MatchStatus.Matched,
            TypePattern(
                "System.Collections.Generic.IEnumerable<>+",
                typeof(List<string>),
                typeSystem).Status);
        Assert.Equal(
            MatchStatus.Matched,
            TypePattern(
                "System.Collections.Generic.IEnumerable<..>+",
                typeof(List<string>),
                typeSystem).Status);
        Assert.Equal(
            MatchStatus.Matched,
            TypePattern(
                "System.Collections.Generic.IEnumerable*<>+",
                typeof(List<string>),
                typeSystem).Status);
        Assert.Equal(
            MatchStatus.Matched,
            TypePattern(
                "System.Collections.Generic.IEnumerable<string>+",
                typeof(List<string>),
                typeSystem).Status);
        // 元数不符与实参不符仍然不匹配。
        Assert.Equal(
            MatchStatus.NotMatched,
            TypePattern(
                "System.Collections.Generic.IEnumerable<,>+",
                typeof(List<string>),
                typeSystem).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            TypePattern(
                "System.Collections.Generic.IEnumerable<int>+",
                typeof(List<string>),
                typeSystem).Status);
        // 嵌套实参中的固定元数无实参形式同样回退闭包求值：
        // List<IEnumerable<string>> 的祖先 IEnumerable<IEnumerable<string>> 满足外层与内层模式。
        Assert.Equal(
            MatchStatus.Matched,
            TypePattern(
                "System.Collections.Generic.IEnumerable<System.Collections.Generic.IEnumerable<>>+",
                typeof(List<IEnumerable<string>>),
                typeSystem).Status);
    }

    [Fact]
    public void MapType_NestedTypeParameterSignatures_UseFlattenedPositions()
    {
        var typeSystem = new ReflectionTypeSystem();
        var mappedT = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(
                typeof(NestedParamOuter<>).GetGenericArguments()[0]));
        var mappedU = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(
                typeof(NestedParamOuter<>.NestedParamInner<>)
                    .GetGenericArguments()[1]));

        // .NET 的类型参数位置是包含链扁平编号：T=0、U=1。
        Assert.Equal(GenericParameterOwnerKind.Type, mappedT.Parameter.OwnerKind);
        Assert.Equal(
            "AspectN.Reflection.Tests.NestedParamOuter`1",
            mappedT.Parameter.OwnerId);
        Assert.Equal(0, mappedT.Parameter.Position);
        Assert.Equal(GenericParameterOwnerKind.Type, mappedU.Parameter.OwnerKind);
        Assert.Equal(
            "AspectN.Reflection.Tests.NestedParamOuter`1/NestedParamInner`1",
            mappedU.Parameter.OwnerId);
        Assert.Equal(1, mappedU.Parameter.Position);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.AreEqual(mappedT, mappedU).Status);
    }

    [Fact]
    public void MapMethod_OuterParameterUsedInNestedMember_NormalizesToOuterDeclaration()
    {
        var typeSystem = new ReflectionTypeSystem();
        var tOverload = Assert.Single(
            typeof(NestedParamOuter<>.NestedParamInner<>).GetMethods(),
            method =>
                method.Name == "M" &&
                method.GetParameters()[0]
                    .ParameterType
                    .GenericParameterPosition == 0);
        var mappedT = Assert.IsType<GenericParameterTypeSignature>(
            Assert.Single(typeSystem.Mapper.MapMethod(tOverload).Parameters)
                .Type
                .Type);
        var declaredT = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(
                typeof(NestedParamOuter<>).GetGenericArguments()[0]));

        // CLR 元数据中嵌套类型重声明外层参数（DeclaringType 为 Inner）；
        // owner 必须归一到原始声明 Outer，与 Roslyn 声明视图及声明处映射一致。
        Assert.Equal(GenericParameterOwnerKind.Type, mappedT.Parameter.OwnerKind);
        Assert.Equal(
            "AspectN.Reflection.Tests.NestedParamOuter`1",
            mappedT.Parameter.OwnerId);
        Assert.Equal(0, mappedT.Parameter.Position);
        Assert.Equal(
            typeof(NestedParamOuter<>).Assembly.GetName().Name,
            mappedT.Parameter.OwnerAssembly.Name);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.AreEqual(mappedT, declaredT).Status);
    }

    [Fact]
    public void AreEqual_SameNamedParametersFromDifferentAssemblies_ReturnNotMatched()
    {
        var first = CreateDynamicBox("ReviewAssemblyA");
        var second = CreateDynamicBox("ReviewAssemblyB");
        Assert.NotEqual(first, second);
        var typeSystem = new ReflectionTypeSystem();
        var mappedFirst = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(first.GetGenericArguments()[0]));
        var mappedSecond = Assert.IsType<GenericParameterTypeSignature>(
            typeSystem.Mapper.MapType(second.GetGenericArguments()[0]));

        // OwnerId 与 Position 完全相同，只有 OwnerAssembly 能区分两个程序集的同名声明。
        Assert.Equal("Review.Box`1", mappedFirst.Parameter.OwnerId);
        Assert.Equal(
            mappedFirst.Parameter.OwnerId,
            mappedSecond.Parameter.OwnerId);
        Assert.Equal(
            mappedFirst.Parameter.Position,
            mappedSecond.Parameter.Position);
        Assert.Equal(
            "ReviewAssemblyA",
            mappedFirst.Parameter.OwnerAssembly.Name);
        Assert.Equal(
            "ReviewAssemblyB",
            mappedSecond.Parameter.OwnerAssembly.Name);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.AreEqual(mappedFirst, mappedSecond).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(mappedFirst, mappedSecond).Status);
    }

    private static Type CreateDynamicBox(string assemblyName)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName(assemblyName),
            AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule(assemblyName);
        var type = module.DefineType(
            "Review.Box`1",
            TypeAttributes.Public | TypeAttributes.Class);
        type.DefineGenericParameters("T");
        return type.CreateType();
    }

    private static MatchResult MemberPattern(
        string source,
        MethodSignature candidate,
        ITypeSystem typeSystem)
    {
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseMemberExpression(source));
        Assert.True(compilation.TryGetMemberPattern(out var pattern));
        return pattern!.Match(candidate, typeSystem);
    }

    private static MatchResult TypePattern(
        string source,
        Type candidate,
        ReflectionTypeSystem typeSystem)
    {
        var compilation = AspectNCompilation.Create(
            AspectNParser.ParseTypePattern(source));
        Assert.True(compilation.TryGetTypePattern(out var pattern));
        return pattern!.Match(typeSystem.Mapper.MapType(candidate), typeSystem);
    }

    private static MethodInfo RequiredMethod(Type type, string name)
    {
        return Assert.IsAssignableFrom<MethodInfo>(
            type.GetMethod(
                name,
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.Static));
    }

    private static string Name(TypeSignature type)
    {
        return Assert.IsType<NamedTypeSignature>(type)
            .Identity.FullMetadataName;
    }

    private sealed class FailingMetadataType : TypeDelegator
    {
        public FailingMetadataType(Type delegatingType)
            : base(delegatingType)
        {
        }

        public override Type[] GetInterfaces()
        {
            throw new TypeLoadException("Synthetic metadata failure.");
        }
    }
}
[AttributeUsage(AttributeTargets.All)]
internal sealed class ExplosiveMarkerAttribute : Attribute
{
    public ExplosiveMarkerAttribute()
    {
        throw new InvalidOperationException("Attributes must not be instantiated.");
    }
}

[AttributeUsage(AttributeTargets.All)]
internal sealed class GenericMarkerAttribute<T> : Attribute
{
}

internal class PatternBase
{
}

internal sealed class PatternDerived : PatternBase
{
}

internal sealed class NestedOuter<T>
{
    public sealed class NestedInner
    {
    }
}

internal sealed class NestedOwner
{
    public static void M<T>(NestedOuter<int>.NestedInner value)
    {
    }

    public static void M<T>(NestedOuter<string>.NestedInner value)
    {
    }
}

internal static class OpenConstructedOwner
{
    public static void M<T>(Dictionary<T, int> value)
        where T : notnull
    {
    }

    public static void M<T>(Dictionary<T, string> value)
        where T : notnull
    {
    }
}

internal sealed class NestedParamOuter<T>
{
    public sealed class NestedParamInner<U>
    {
        public static void M<V>(T value)
        {
        }

        public static void M<V>(U value)
        {
        }
    }
}

[ExplosiveMarker]
internal sealed class ReflectionFixture<T>
{
    private int _value;

    static ReflectionFixture()
    {
    }

    [ExplosiveMarker]
    public ReflectionFixture()
    {
    }

    [ExplosiveMarker]
    [return: ExplosiveMarker]
    public TMethod Echo<TMethod>(
        [ExplosiveMarker] T value,
        TMethod other)
    {
        _value += value is null ? 0 : 1;
        return other;
    }

    public async Task<T> LoadAsync(T value)
    {
        await Task.Yield();
        _value++;
        return value;
    }

    public Task<T> TaskReturning(T value)
    {
        _value++;
        return Task.FromResult(value);
    }

    public void RefKinds(
        ref int first,
        out string second,
        in double third,
        ref readonly long fourth)
    {
        first += _value + (int)third + (int)fourth;
        second = first.ToString(CultureInfo.InvariantCulture);
    }

    public ref readonly int RefReadonlyReturn()
    {
        return ref _value;
    }

    [GenericMarker<int>]
    public void GenericAttributed()
    {
        _value++;
    }

    [GenericMarker<List<int>>]
    public void NestedGenericAttributed()
    {
        _value++;
    }

    [GenericMarker<string>]
    public void NullableAttributed()
    {
        _value++;
    }

    public static void SameNameOverload<TItem>(TItem value) => _ = value;

    public static void SameNameOverload<TItem>(TItem value, int count) =>
        _ = (value, count);

    public ref int RefValue
    {
        get => ref _value;
    }

    public ref readonly int RefReadonlyValue
    {
        get => ref _value;
    }

    [ExplosiveMarker]
    public string Name
    {
        [ExplosiveMarker]
        get;
        set;
    } = string.Empty;

    public string this[int index] => index.ToString(CultureInfo.InvariantCulture);

    public event EventHandler? Changed;

    public void RaiseChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public static ReflectionFixture<T> operator +(
        ReflectionFixture<T> left,
        ReflectionFixture<T> right)
    {
        _ = right;
        return left;
    }

    public static implicit operator string(ReflectionFixture<T> value)
    {
        _ = value;
        return string.Empty;
    }
}

internal sealed class ReflectionOuter<T>
{
    internal sealed class Inner<TKey, TValue>
    {
    }
}
