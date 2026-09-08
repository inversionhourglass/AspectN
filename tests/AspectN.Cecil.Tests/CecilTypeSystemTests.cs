using AspectN.Evaluation;
using AspectN.Metadata;
using Mono.Cecil;

namespace AspectN.Cecil.Tests;

public sealed class CecilTypeSystemTests
{
    [Fact]
    public void AreEqual_ExactType_ReturnsMatched()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var type = typeSystem.Mapper.MapType(module.TypeSystem.Int32);

        var result = typeSystem.AreEqual(type, type);

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void IsAssignableTo_BaseClass_ReturnsMatchedOnlyInForwardDirection()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var baseType = AddClass(module, "Base", module.TypeSystem.Object);
        var derivedType = AddClass(module, "Derived", baseType);
        var mappedBase = typeSystem.Mapper.MapType(baseType);
        var mappedDerived = typeSystem.Mapper.MapType(derivedType);

        var forward = typeSystem.IsAssignableTo(mappedDerived, mappedBase);
        var reverse = typeSystem.IsAssignableTo(mappedBase, mappedDerived);

        Assert.Equal(MatchStatus.Matched, forward.Status);
        Assert.Equal(MatchStatus.NotMatched, reverse.Status);
    }

    [Fact]
    public void IsAssignableTo_Interface_ReturnsMatched()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var contract = new TypeDefinition(
            "Example",
            "IContract",
            TypeAttributes.Public |
            TypeAttributes.Interface |
            TypeAttributes.Abstract,
            module.TypeSystem.Object);
        module.Types.Add(contract);
        var implementation = AddClass(
            module,
            "Implementation",
            module.TypeSystem.Object);
        implementation.Interfaces.Add(new InterfaceImplementation(contract));

        var result = typeSystem.IsAssignableTo(
            typeSystem.Mapper.MapType(implementation),
            typeSystem.Mapper.MapType(contract));

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void IsAssignableTo_ArrayCandidate_DoesNotInheritElementBaseRelationship()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var baseType = AddClass(module, "Base", module.TypeSystem.Object);
        var derivedType = AddClass(module, "Derived", baseType);
        var derivedArray = typeSystem.Mapper.MapType(
            new ArrayType(derivedType, 1));

        var result = typeSystem.IsAssignableTo(
            derivedArray,
            typeSystem.Mapper.MapType(baseType));

        Assert.Equal(MatchStatus.NotMatched, result.Status);
    }

    [Fact]
    public void IsAssignableTo_ArrayCandidate_DoesNotInheritElementInterfaceRelationship()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var contract = new TypeDefinition(
            "Example",
            "IElement",
            TypeAttributes.Public |
            TypeAttributes.Interface |
            TypeAttributes.Abstract,
            module.TypeSystem.Object);
        module.Types.Add(contract);
        var derivedType = AddClass(module, "Derived", module.TypeSystem.Object);
        derivedType.Interfaces.Add(new InterfaceImplementation(contract));
        var derivedArray = typeSystem.Mapper.MapType(
            new ArrayType(derivedType, 1));

        var result = typeSystem.IsAssignableTo(
            derivedArray,
            typeSystem.Mapper.MapType(contract));

        Assert.Equal(MatchStatus.NotMatched, result.Status);
    }

    [Fact]
    public void IsAssignableTo_CovariantArray_MatchesBaseElementArray()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var baseType = AddClass(module, "Base", module.TypeSystem.Object);
        var derivedType = AddClass(module, "Derived", baseType);
        var derivedArray = typeSystem.Mapper.MapType(
            new ArrayType(derivedType, 1));
        var baseArray = typeSystem.Mapper.MapType(new ArrayType(baseType, 1));

        var forward = typeSystem.IsAssignableTo(derivedArray, baseArray);
        var reverse = typeSystem.IsAssignableTo(baseArray, derivedArray);
        var rankMismatch = typeSystem.IsAssignableTo(
            derivedArray,
            typeSystem.Mapper.MapType(new ArrayType(baseType, 2)));

        Assert.Equal(MatchStatus.Matched, forward.Status);
        Assert.Equal(MatchStatus.NotMatched, reverse.Status);
        Assert.Equal(MatchStatus.NotMatched, rankMismatch.Status);
    }

    [Fact]
    public void IsAssignableTo_ValueElementArray_DoesNotMatchOtherElementArray()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var valueType = AddStruct(module, "Number");
        var valueArray = typeSystem.Mapper.MapType(
            new ArrayType(valueType, 1));
        var objectArray = typeSystem.Mapper.MapType(
            new ArrayType(module.TypeSystem.Object, 1));

        var result = typeSystem.IsAssignableTo(valueArray, objectArray);

        Assert.Equal(MatchStatus.NotMatched, result.Status);
    }

    [Fact]
    public void IsAssignableTo_ArrayCandidate_MatchesSystemArrayRootsOnly()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var baseType = AddClass(module, "Base", module.TypeSystem.Object);
        var derivedType = AddClass(module, "Derived", baseType);
        var derivedArray = typeSystem.Mapper.MapType(
            new ArrayType(derivedType, 1));
        var arrayRoot = typeSystem.Mapper.MapType(
            new TypeReference("System", "Array", module, module.TypeSystem.CoreLibrary));
        var objectRoot = typeSystem.Mapper.MapType(module.TypeSystem.Object);
        var unrelated = typeSystem.Mapper.MapType(baseType);

        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(derivedArray, arrayRoot).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(derivedArray, objectRoot).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(derivedArray, unrelated).Status);
    }

    [Fact]
    public void IsAssignableTo_ArrayCandidate_MatchesArrayOwnedNonGenericInterfaces()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var valueType = AddStruct(module, "Number");
        var valueArray = typeSystem.Mapper.MapType(
            new ArrayType(valueType, 2));
        var multiDimensional = typeSystem.Mapper.MapType(
            new ArrayType(module.TypeSystem.Int32, 2));

        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(
                valueArray,
                NamedTarget(typeSystem, module, "System.Collections", "IEnumerable")).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(
                valueArray,
                NamedTarget(typeSystem, module, "System.Collections", "IList")).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(
                multiDimensional,
                NamedTarget(typeSystem, module, "System", "ICloneable")).Status);
    }

    [Fact]
    public void IsAssignableTo_SzArray_MatchesGenericCollectionInterfaces()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var baseType = AddClass(module, "Base", module.TypeSystem.Object);
        var derivedType = AddClass(module, "Derived", baseType);
        var derivedArray = typeSystem.Mapper.MapType(
            new ArrayType(derivedType, 1));
        var numberType = AddStruct(module, "Number");
        var numberArray = typeSystem.Mapper.MapType(
            new ArrayType(numberType, 1));
        var multiDimensional = typeSystem.Mapper.MapType(
            new ArrayType(module.TypeSystem.Int32, 2));

        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(
                derivedArray,
                GenericTarget(
                    typeSystem,
                    module,
                    "IEnumerable",
                    derivedType)).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(
                derivedArray,
                GenericTarget(
                    typeSystem,
                    module,
                    "IEnumerable",
                    baseType)).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(
                derivedArray,
                GenericTarget(
                    typeSystem,
                    module,
                    "IList",
                    baseType)).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(
                numberArray,
                GenericTarget(
                    typeSystem,
                    module,
                    "IList",
                    numberType)).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(
                numberArray,
                GenericTarget(
                    typeSystem,
                    module,
                    "IEnumerable",
                    baseType)).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(
                multiDimensional,
                GenericTarget(
                    typeSystem,
                    module,
                    "IEnumerable",
                    module.TypeSystem.Int32)).Status);
    }

    [Fact]
    public void IsAssignableTo_NonVectorRankOneArray_RejectsVectorOnlyRelations()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var baseType = AddClass(module, "Base", module.TypeSystem.Object);
        var derivedType = AddClass(module, "Derived", baseType);
        var nonVector = new ArrayType(derivedType, 1);
        nonVector.Dimensions[0] = new ArrayDimension(1, null);
        Assert.False(nonVector.IsVector);
        var nonVectorSignature = typeSystem.Mapper.MapType(nonVector);
        var baseArray = typeSystem.Mapper.MapType(
            new ArrayType(baseType, 1));

        // 非向量（非零下界）一维数组不能赋给向量数组；泛型集合接口仅对向量数组成立。
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(nonVectorSignature, baseArray).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(
                nonVectorSignature,
                GenericTarget(typeSystem, module, "IEnumerable", derivedType)).Status);
        // 反方向（向量 → 非向量一维）按 CLR 规则允许，元素引用协变即可。
        var derivedVector = typeSystem.Mapper.MapType(
            new ArrayType(derivedType, 1));
        var baseNonVector = typeSystem.Mapper.MapType(nonVector);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(derivedVector, baseNonVector).Status);
        // 非泛型接口由任意秩的数组实现，与向量性无关。
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(
                nonVectorSignature,
                NamedTarget(typeSystem, module, "System.Collections", "IEnumerable")).Status);
    }

    [Fact]
    public void IsAssignableTo_MultidimensionalReferenceArray_MatchesCovariance()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var baseType = AddClass(module, "Base", module.TypeSystem.Object);
        var derivedType = AddClass(module, "Derived", baseType);
        var numberType = AddStruct(module, "Number");
        var derivedGrid = typeSystem.Mapper.MapType(
            new ArrayType(derivedType, 2));
        var baseGrid = typeSystem.Mapper.MapType(
            new ArrayType(baseType, 2));
        var numberGrid = typeSystem.Mapper.MapType(
            new ArrayType(numberType, 2));

        // 多维引用类型数组支持协变；值类型元素不协变。
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(derivedGrid, baseGrid).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(baseGrid, derivedGrid).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(numberGrid, baseGrid).Status);
    }

    [Fact]
    public void MapCallable_NonVectorArrayReturnType_PreservesShape()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var probe = AddClass(module, "Probe", module.TypeSystem.Object);
        var nonVector = new ArrayType(module.TypeSystem.String, 1);
        nonVector.Dimensions[0] = new ArrayDimension(1, null);
        var nonVectorMethod = new MethodDefinition(
            "Array",
            MethodAttributes.Public,
            nonVector);
        var vectorMethod = new MethodDefinition(
            "Vector",
            MethodAttributes.Public,
            new ArrayType(module.TypeSystem.String, 1));
        probe.Methods.Add(nonVectorMethod);
        probe.Methods.Add(vectorMethod);

        // 成员映射复制数组类型时必须保留维度形状：非 SZ 返回值不能匹配 SZ 专属的泛型集合接口。
        var mapped = typeSystem.Mapper.MapCallable(nonVectorMethod);
        var target = GenericTarget(
            typeSystem,
            module,
            "IEnumerable",
            module.TypeSystem.String);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(mapped.ReturnType.Type, target).Status);

        var nonVectorPattern = AspectN.AspectNCompilation.Create(
            AspectN.AspectNParser.ParseMemberExpression(
                "method(System.Collections.Generic.IEnumerable<string>+ Example.Probe.Array())"));
        Assert.True(nonVectorPattern.TryGetMemberPattern(out var pattern));
        Assert.Equal(
            MatchStatus.NotMatched,
            pattern!.Match(mapped, typeSystem).Status);

        // 同一目标的向量返回值作为对照，证明负例来自形状而非目标构造。
        var vectorMapped = typeSystem.Mapper.MapCallable(vectorMethod);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(vectorMapped.ReturnType.Type, target).Status);
    }

    [Fact]
    public void IsAssignableTo_GenericVariance_RespectsParameterVariance()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var baseType = AddClass(module, "Element", module.TypeSystem.Object);
        var derivedType = AddClass(module, "SpecificElement", baseType);
        var numberType = AddStruct(module, "Number");
        var covariant = AddVariantInterface(module, "IProducer`1", isCovariant: true);
        var contravariant = AddVariantInterface(
            module,
            "IConsumer`1",
            isCovariant: false);

        var producerOfDerived = typeSystem.Mapper.MapType(
            InstanceOf(covariant, derivedType));
        var producerOfBase = typeSystem.Mapper.MapType(
            InstanceOf(covariant, baseType));
        var producerOfNumber = typeSystem.Mapper.MapType(
            InstanceOf(covariant, numberType));
        var consumerOfBase = typeSystem.Mapper.MapType(
            InstanceOf(contravariant, baseType));
        var consumerOfDerived = typeSystem.Mapper.MapType(
            InstanceOf(contravariant, derivedType));
        var consumerOfNumber = typeSystem.Mapper.MapType(
            InstanceOf(contravariant, numberType));

        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(producerOfDerived, producerOfBase).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(producerOfBase, producerOfDerived).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(consumerOfBase, consumerOfDerived).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(consumerOfDerived, consumerOfBase).Status);
        // 变型不能通过装箱将值类型实参转换为引用类型实参。
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(producerOfNumber, producerOfBase).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(consumerOfBase, consumerOfNumber).Status);
    }

    [Fact]
    public void IsAssignableTo_InvariantGenericArguments_RequireStructuralEquality()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var baseType = AddClass(module, "Element", module.TypeSystem.Object);
        var derivedType = AddClass(module, "SpecificElement", baseType);
        var container = AddGenericType(module, "IBox`1", TypeAttributes.Interface |
            TypeAttributes.Public | TypeAttributes.Abstract);

        var boxOfDerived = typeSystem.Mapper.MapType(
            InstanceOf(container, derivedType));
        var boxOfBase = typeSystem.Mapper.MapType(
            InstanceOf(container, baseType));

        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(boxOfDerived, boxOfBase).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(boxOfDerived, boxOfDerived).Status);
    }

    private static TypeSignature NamedTarget(
        CecilTypeSystem typeSystem,
        ModuleDefinition module,
        string @namespace,
        string name)
    {
        return typeSystem.Mapper.MapType(
            new TypeReference(@namespace, name, module, module.TypeSystem.CoreLibrary));
    }

    private static NamedTypeSignature GenericTarget(
        CecilTypeSystem typeSystem,
        ModuleDefinition module,
        string name,
        TypeReference argument)
    {
        var identity = new TypeIdentity(
            new AssemblyIdentity(string.Empty),
            "System.Collections.Generic",
            name,
            1,
            TypeKind.Interface);
        return new NamedTypeSignature(
            identity,
            new[] { typeSystem.Mapper.MapType(argument) },
            new NamedTypeSignature(identity));
    }

    private static GenericInstanceType InstanceOf(
        TypeReference definition,
        TypeReference argument)
    {
        var instance = new GenericInstanceType(definition);
        instance.GenericArguments.Add(argument);
        return instance;
    }

    private static TypeDefinition AddVariantInterface(
        ModuleDefinition module,
        string name,
        bool isCovariant)
    {
        var definition = new TypeDefinition(
            "Example",
            name,
            TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract,
            module.TypeSystem.Object);
        var parameter = new GenericParameter("T", definition);
        if (isCovariant)
        {
            parameter.IsCovariant = true;
        }
        else
        {
            parameter.IsContravariant = true;
        }

        definition.GenericParameters.Add(parameter);
        module.Types.Add(definition);
        return definition;
    }

    [Fact]
    public void IsAssignableTo_ConstructedGenericInterface_SubstitutesArguments()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var contract = AddGenericType(
            module,
            "IBox`1",
            TypeAttributes.Public |
            TypeAttributes.Interface |
            TypeAttributes.Abstract);
        var implementation = AddGenericType(
            module,
            "Box`1",
            TypeAttributes.Public | TypeAttributes.Class);
        var implementedContract = new GenericInstanceType(contract);
        implementedContract.GenericArguments.Add(
            implementation.GenericParameters[0]);
        implementation.Interfaces.Add(
            new InterfaceImplementation(implementedContract));
        var constructedImplementation = new GenericInstanceType(implementation);
        constructedImplementation.GenericArguments.Add(module.TypeSystem.String);
        var constructedContract = new GenericInstanceType(contract);
        constructedContract.GenericArguments.Add(module.TypeSystem.String);

        var result = typeSystem.IsAssignableTo(
            typeSystem.Mapper.MapType(constructedImplementation),
            typeSystem.Mapper.MapType(constructedContract));

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void IsAssignableTo_CyclicBaseGraph_TerminatesAsNotMatched()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var first = AddClass(module, "First", module.TypeSystem.Object);
        var second = AddClass(module, "Second", first);
        first.BaseType = second;
        var unrelated = AddClass(module, "Unrelated", module.TypeSystem.Object);

        var result = typeSystem.IsAssignableTo(
            typeSystem.Mapper.MapType(first),
            typeSystem.Mapper.MapType(unrelated));

        Assert.Equal(MatchStatus.NotMatched, result.Status);
    }

    [Fact]
    public void IsAssignableTo_UnresolvedAssembly_ReturnsUnknownWithResolutionDiagnostic()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var assembly = new AssemblyNameReference(
            "Missing.Contracts",
            new Version(1, 0, 0, 0));
        module.AssemblyReferences.Add(assembly);
        var unresolved = new TypeReference(
            "Missing",
            "Contract",
            module,
            assembly);

        var result = typeSystem.IsAssignableTo(
            typeSystem.Mapper.MapType(unresolved),
            typeSystem.Mapper.MapType(module.TypeSystem.Object));

        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == "AN3002");
    }

    [Fact]
    public void IsAssignableTo_ResolverException_ReturnsUnknownWithResolutionDiagnostic()
    {
        var module = ModuleDefinition.CreateModule(
            "Throwing.Resolver",
            new ModuleParameters
            {
                Kind = ModuleKind.Dll,
                AssemblyResolver = new ThrowingAssemblyResolver()
            });
        using var typeSystem = new CecilTypeSystem(module);
        var assembly = new AssemblyNameReference(
            "Broken.Contracts",
            new Version(1, 0, 0, 0));
        var unresolved = new TypeReference(
            "Broken",
            "Contract",
            module,
            assembly);

        var result = typeSystem.IsAssignableTo(
            typeSystem.Mapper.MapType(unresolved),
            typeSystem.Mapper.MapType(module.TypeSystem.Object));

        Assert.Equal(MatchStatus.Unknown, result.Status);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == "AN3002");
    }

    [Fact]
    public void IsAssignableTo_CanceledToken_ThrowsOperationCanceledException()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var candidate = typeSystem.Mapper.MapType(module.TypeSystem.String);
        var target = typeSystem.Mapper.MapType(module.TypeSystem.Object);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            typeSystem.IsAssignableTo(candidate, target, cancellation.Token));
    }

    [Fact]
    public void Dispose_LeaveOpen_KeepsModuleWritable()
    {
        var module = TestModuleFactory.Create();
        var typeSystem = new CecilTypeSystem(
            module,
            new CecilMappingOptions(
                compositeAccessibility: false,
                leaveOpen: true));

        typeSystem.Dispose();

        using var stream = new MemoryStream();
        module.Write(stream);
        Assert.True(stream.Length > 0);
        module.Dispose();
    }

    private static TypeDefinition AddClass(
        ModuleDefinition module,
        string name,
        TypeReference baseType)
    {
        var definition = new TypeDefinition(
            "Example",
            name,
            TypeAttributes.Public | TypeAttributes.Class,
            baseType);
        module.Types.Add(definition);
        return definition;
    }

    private static TypeDefinition AddStruct(ModuleDefinition module, string name)
    {
        var valueType = new TypeReference(
            "System",
            "ValueType",
            module,
            module.TypeSystem.CoreLibrary);
        var definition = new TypeDefinition(
            "Example",
            name,
            TypeAttributes.Public |
            TypeAttributes.SequentialLayout |
            TypeAttributes.Sealed,
            valueType);
        module.Types.Add(definition);
        return definition;
    }

    private static TypeDefinition AddGenericType(
        ModuleDefinition module,
        string name,
        TypeAttributes attributes)
    {
        var definition = new TypeDefinition(
            "Example",
            name,
            attributes,
            module.TypeSystem.Object);
        definition.GenericParameters.Add(new GenericParameter("T", definition));
        module.Types.Add(definition);
        return definition;
    }

    private sealed class ThrowingAssemblyResolver : IAssemblyResolver
    {
        public AssemblyDefinition Resolve(AssemblyNameReference name)
        {
            throw new InvalidOperationException(
                $"Resolver failed for '{name.FullName}'.");
        }

        public AssemblyDefinition Resolve(
            AssemblyNameReference name,
            ReaderParameters parameters)
        {
            throw new InvalidOperationException(
                $"Resolver failed for '{name.FullName}'.");
        }

        public void Dispose()
        {
        }
    }
}
