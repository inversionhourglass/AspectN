using AspectN.Evaluation;
using Xunit;

namespace AspectN.Reflection.Tests;

public sealed class ArrayShapeAssignabilityTests
{
    [Fact]
    public void IsAssignableTo_NonVectorToVectorRankOne_ReturnsNotMatched()
    {
        var typeSystem = new ReflectionTypeSystem();
        var nonVector = typeSystem.Mapper.MapType(typeof(int).MakeArrayType(1));
        var vector = typeSystem.Mapper.MapType(typeof(int[]));
        var stringNonVector = typeSystem.Mapper.MapType(
            typeof(string).MakeArrayType(1));
        var stringVector = typeSystem.Mapper.MapType(typeof(string[]));

        // 与运行时一致：typeof(int[]).IsAssignableFrom(typeof(int).MakeArrayType(1)) 为 false。
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(nonVector, vector).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(stringNonVector, stringVector).Status);
    }

    [Fact]
    public void IsAssignableTo_VectorToNonVectorRankOne_ReturnsMatched()
    {
        var typeSystem = new ReflectionTypeSystem();
        var vector = typeSystem.Mapper.MapType(typeof(int[]));
        var nonVector = typeSystem.Mapper.MapType(typeof(int).MakeArrayType(1));

        // 方向相反时成立：typeof(int[*]).IsAssignableFrom(typeof(int[])) 为 true，
        // 与 Cecil 的 IsArrayIdentityAdmissible 一致。
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(vector, nonVector).Status);
    }

    [Fact]
    public void IsAssignableTo_SameShapeArrays_ReturnMatched()
    {
        var typeSystem = new ReflectionTypeSystem();
        var vector = typeSystem.Mapper.MapType(typeof(int[]));
        var nonVector = typeSystem.Mapper.MapType(typeof(string).MakeArrayType(1));

        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(vector, vector).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(nonVector, nonVector).Status);
    }

    [Fact]
    public void IsAssignableTo_NonVectorRankOne_RejectsGenericCollectionInterfaces()
    {
        var typeSystem = new ReflectionTypeSystem();
        var nonVector = typeSystem.Mapper.MapType(typeof(int).MakeArrayType(1));
        var vector = typeSystem.Mapper.MapType(typeof(int[]));
        var enumerable = typeSystem.Mapper.MapType(typeof(IEnumerable<int>));
        var nonGeneric = typeSystem.Mapper.MapType(
            typeof(System.Collections.IEnumerable));

        // 泛型集合接口仅由 SZ 向量实现；非泛型接口任意形状的数组都实现。
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(nonVector, enumerable).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(vector, enumerable).Status);
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(nonVector, nonGeneric).Status);
    }

    [Fact]
    public void IsAssignableTo_MultidimensionalArrays_PreserveIdentityOnly()
    {
        var typeSystem = new ReflectionTypeSystem();
        var stringGrid = typeSystem.Mapper.MapType(typeof(string[,]));
        var objectGrid = typeSystem.Mapper.MapType(typeof(object[,]));
        var stringCube = typeSystem.Mapper.MapType(typeof(string[,,]));

        // 多维数组与其它数组之间没有名义关系（与 Cecil 一致）；同元素同秩为恒等。
        // 向量形状判定不得波及多维数组的同秩关系。
        Assert.Equal(
            MatchStatus.Matched,
            typeSystem.IsAssignableTo(stringGrid, stringGrid).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(stringGrid, objectGrid).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            typeSystem.IsAssignableTo(stringGrid, stringCube).Status);
    }

    [Fact]
    public void TypePattern_CompositionalElementPlus_PreservesMultidimensionalCovariance()
    {
        var typeSystem = new ReflectionTypeSystem();

        // 多维数组协变经元素位置的 + 组合表达（规范 12.4），不受向量形状判定影响。
        Assert.Equal(
            MatchStatus.Matched,
            TypePattern("object+[,]", typeof(string[,]), typeSystem).Status);
        Assert.Equal(
            MatchStatus.NotMatched,
            TypePattern("object+[,]", typeof(string[,,]), typeSystem).Status);
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
}
