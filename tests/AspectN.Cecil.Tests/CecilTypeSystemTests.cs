using AspectN.Diagnostics;
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
    public void IsAssignableTo_ArrayCandidate_RequiresExactElements()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var baseType = AddClass(module, "Base", module.TypeSystem.Object);
        var derivedType = AddClass(module, "Derived", baseType);
        var derivedArray = typeSystem.Mapper.MapType(
            new ArrayType(derivedType, 1));
        var baseArray = typeSystem.Mapper.MapType(new ArrayType(baseType, 1));

        // + 只含名义关系：数组之间没有协变，元素不同即不可赋值；同元素数组为恒等。
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(derivedArray, baseArray).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(baseArray, derivedArray).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(
                derivedArray,
                typeSystem.Mapper.MapType(new ArrayType(baseType, 2))).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(derivedArray, derivedArray).Status);
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
    public void TypePattern_UnqualifiedArrayTargets_MatchSyntheticRelations()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var derivedType = AddClass(module, "Derived", module.TypeSystem.Object);
        var derivedArray = typeSystem.Mapper.MapType(
            new ArrayType(derivedType, 1));
        var multiDimensional = typeSystem.Mapper.MapType(
            new ArrayType(module.TypeSystem.Int32, 2));

        // 非限定目标名（空命名空间）走与非限定类目标一致的统一比较规则。
        Assert.Equal(
            MatchStatus.Matched,
            TypePattern("Array+", derivedArray, typeSystem));
        Assert.Equal(
            MatchStatus.Matched,
            TypePattern("System.Array+", derivedArray, typeSystem));
        Assert.Equal(
            MatchStatus.Matched,
            TypePattern(
                "System.Collections.Generic.IEnumerable<Derived>+",
                derivedArray,
                typeSystem));
        Assert.Equal(
            MatchStatus.NotMatched,
            TypePattern(
                "System.Collections.Generic.IEnumerable<System.Int32>+",
                multiDimensional,
                typeSystem));
    }

    [Fact]
    public void TypePattern_WildcardAssignable_MatchesInheritanceClosure()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var baseType = AddClass(module, "Element", module.TypeSystem.Object);
        var derivedType = AddClass(module, "SpecificElement", baseType);
        var derived = typeSystem.Mapper.MapType(derivedType);

        // 通配与 .. 目标沿闭包用模式求值，与 Reflection/Roslyn 一致。
        Assert.Equal(
            MatchStatus.Matched,
            TypePattern("*Element+", derived, typeSystem));
        Assert.Equal(
            MatchStatus.Matched,
            TypePattern("Example..Element+", derived, typeSystem));
        Assert.Equal(
            MatchStatus.NotMatched,
            TypePattern("*Missing+", derived, typeSystem));
    }

    [Fact]
    public void TypePattern_ResolutionFailureKeepsKnownInterfaceBranch()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var missing = new AssemblyNameReference(
            "Missing.Contracts",
            new Version(1, 0, 0, 0));
        module.AssemblyReferences.Add(missing);
        var missingBase = new TypeReference(
            "Missing",
            "BaseProvider",
            module,
            missing);
        var known = new TypeDefinition(
            "Fixture",
            "IKnown",
            TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract,
            module.TypeSystem.Object);
        module.Types.Add(known);
        var derived = new TypeDefinition(
            "Fixture",
            "Derived",
            TypeAttributes.Public | TypeAttributes.Class,
            missingBase);
        derived.Interfaces.Add(new InterfaceImplementation(known));
        module.Types.Add(derived);

        var mapped = typeSystem.Mapper.MapType(derived);

        // 缺失基类的解析失败只影响其自身展开，不阻断队列中已声明的已知接口分支。
        Assert.Equal(
            MatchStatus.Matched,
            TypePattern("Fixture.IKnown+", mapped, typeSystem));
        Assert.Equal(
            MatchStatus.Matched,
            TypePattern("*Known+", mapped, typeSystem));
        // 缺失基类之上的关系仍不可知。
        Assert.Equal(
            MatchStatus.Unknown,
            TypePattern("*Other+", mapped, typeSystem));

        // 具体目标路径同样保留已知分支；无确定匹配且有未解析分支时返回 Unknown。
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(
                mapped,
                typeSystem.Mapper.MapType(known)).Status);
        var unrelated = AddClass(module, "Unrelated", module.TypeSystem.Object);
        Assert.Equal(
            MatchStatus.Unknown,
            typeSystem.IsAssignableTo(
                mapped,
                typeSystem.Mapper.MapType(unrelated)).Status);

        // 闭包同时包含已知基类引用与已知接口，并携带解析失败标记。
        var closure = typeSystem.GetInheritanceClosure(mapped);
        Assert.NotNull(closure.Failure);
        Assert.Equal(
            2,
            closure.Ancestors.Count(a =>
                Assert.IsType<NamedTypeSignature>(a).Identity.FullMetadataName
                    is "Missing.BaseProvider" or "Fixture.IKnown"));
    }

    [Fact]
    public void TypePattern_ResolutionFailureKeepsSiblingInterfaces()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var missing = new AssemblyNameReference(
            "Missing.Contracts",
            new Version(1, 0, 0, 0));
        module.AssemblyReferences.Add(missing);
        var missingInterface = new TypeReference(
            "Missing",
            "IProvider",
            module,
            missing);
        var known = new TypeDefinition(
            "Fixture",
            "IKnownA",
            TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract,
            module.TypeSystem.Object);
        module.Types.Add(known);

        // 接口声明顺序相反的两个候选必须得到一致结果。
        var missingFirst = AddClass(module, "MissingFirst", module.TypeSystem.Object);
        missingFirst.Interfaces.Add(
            new InterfaceImplementation(missingInterface));
        missingFirst.Interfaces.Add(new InterfaceImplementation(known));
        var knownFirst = AddClass(module, "KnownFirst", module.TypeSystem.Object);
        knownFirst.Interfaces.Add(new InterfaceImplementation(known));
        knownFirst.Interfaces.Add(
            new InterfaceImplementation(missingInterface));

        foreach (var candidate in new[] { missingFirst, knownFirst })
        {
            var mapped = typeSystem.Mapper.MapType(candidate);

            Assert.Equal(
                MatchStatus.Matched,
                TypePattern("*KnownA+", mapped, typeSystem));
            // 缺失接口的引用本身是已知祖先，模式可先命中再终止展开。
            Assert.Equal(
                MatchStatus.Matched,
                TypePattern("*Provider+", mapped, typeSystem));
            Assert.Equal(
                MatchStatus.Unknown,
                TypePattern("*Other+", mapped, typeSystem));

            var closure = typeSystem.GetInheritanceClosure(mapped);
            Assert.NotNull(closure.Failure);
            Assert.Equal(
                2,
                closure.Ancestors.Count(a =>
                    Assert.IsType<NamedTypeSignature>(a).Identity.FullMetadataName
                        is "Missing.IProvider" or "Fixture.IKnownA"));
        }
    }

    [Fact]
    public void TypePattern_WildcardAssignable_PartialClosureReturnsUnknown()
    {
        var module = TestModuleFactory.Create();
        using var typeSystem = new CecilTypeSystem(module);
        var missing = new AssemblyNameReference(
            "Missing.Contracts",
            new Version(1, 0, 0, 0));
        module.AssemblyReferences.Add(missing);
        var missingBase = new TypeReference(
            "Missing",
            "BaseProvider",
            module,
            missing);
        var derived = new TypeDefinition(
            "Fixture",
            "Derived",
            TypeAttributes.Public | TypeAttributes.Class,
            missingBase);
        module.Types.Add(derived);

        var mapped = typeSystem.Mapper.MapType(derived);

        // 已知基类引用先产出再解析：具体与通配目标都能命中它。
        Assert.Equal(
            MatchStatus.Matched,
            TypePattern("Missing.BaseProvider+", mapped, typeSystem));
        Assert.Equal(
            MatchStatus.Matched,
            TypePattern("*Provider+", mapped, typeSystem));

        // 缺失基类的上层祖先不可知：未命中时返回 Unknown 并携带解析诊断，否定模式正确传播。
        var indeterminate = MatchPattern("*Other+", mapped, typeSystem);
        Assert.Equal(MatchStatus.Unknown, indeterminate.Status);
        Assert.Contains(
            indeterminate.Diagnostics,
            diagnostic => diagnostic.Code == DiagnosticCodes.ResolutionFailure);
        Assert.Equal(
            MatchStatus.Unknown,
            TypePattern("!*Other+", mapped, typeSystem));
        Assert.Equal(
            MatchStatus.NotMatched,
            TypePattern("!*Provider+", mapped, typeSystem));

        // 闭包本身：已知基类已产出，同时携带解析失败标记。
        var closure = typeSystem.GetInheritanceClosure(mapped);
        var ancestor = Assert.Single(closure.Ancestors);
        var named = Assert.IsType<NamedTypeSignature>(ancestor);
        Assert.Equal("Missing.BaseProvider", named.Identity.FullMetadataName);
        Assert.NotNull(closure.Failure);
    }

    private static MatchStatus TypePattern(
        string source,
        TypeSignature candidate,
        CecilTypeSystem typeSystem)
    {
        var compilation = AspectN.AspectNCompilation.Create(
            AspectN.AspectNParser.ParseTypePattern(source));
        Assert.True(compilation.TryGetTypePattern(out var pattern),
            string.Join(Environment.NewLine, compilation.Diagnostics.Select(diagnostic => diagnostic.Message)));

        return pattern!.Match(candidate, typeSystem).Status;
    }

    private static MatchResult MatchPattern(
        string source,
        TypeSignature candidate,
        CecilTypeSystem typeSystem)
    {
        var compilation = AspectN.AspectNCompilation.Create(
            AspectN.AspectNParser.ParseTypePattern(source));
        Assert.True(compilation.TryGetTypePattern(out var pattern));
        return pattern!.Match(candidate, typeSystem);
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

        // SZ 数组实现的泛型集合接口由运行时合成，实参与元素完全一致；变型实例化不属于名义关系。
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
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(
                derivedArray,
                GenericTarget(
                    typeSystem,
                    module,
                    "IEnumerable",
                    baseType)).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
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
        // 元素不同的向量 → 非向量一维没有名义关系；同元素时为恒等（向量性差异不阻止恒等）。
        var derivedVector = typeSystem.Mapper.MapType(
            new ArrayType(derivedType, 1));
        var baseNonVector = new ArrayType(baseType, 1);
        baseNonVector.Dimensions[0] = new ArrayDimension(1, null);
        var stringVector = typeSystem.Mapper.MapType(
            new ArrayType(module.TypeSystem.String, 1));
        var stringNonVector = new ArrayType(module.TypeSystem.String, 1);
        stringNonVector.Dimensions[0] = new ArrayDimension(1, null);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(
                derivedVector,
                typeSystem.Mapper.MapType(baseNonVector)).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(
                stringVector,
                typeSystem.Mapper.MapType(stringNonVector)).Status);
        // 非泛型接口由任意秩的数组实现，与向量性无关。
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(
                nonVectorSignature,
                NamedTarget(typeSystem, module, "System.Collections", "IEnumerable")).Status);
    }

    [Fact]
    public void IsAssignableTo_MultidimensionalReferenceArray_RejectsCovariance()
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

        // 多维数组与其它数组之间没有名义关系；同元素同秩为恒等。
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(derivedGrid, baseGrid).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(baseGrid, derivedGrid).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(derivedGrid, derivedGrid).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(numberGrid, baseGrid).Status);
    }

    [Fact]
    public void MapMethod_NonVectorArrayReturnType_PreservesShape()
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
        var mapped = typeSystem.Mapper.MapMethod(nonVectorMethod);
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
        var vectorMapped = typeSystem.Mapper.MapMethod(vectorMethod);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(vectorMapped.ReturnType.Type, target).Status);
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
            diagnostic => diagnostic.Code == DiagnosticCodes.ResolutionFailure);
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
            diagnostic => diagnostic.Code == DiagnosticCodes.ResolutionFailure);
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
