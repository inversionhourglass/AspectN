using AspectN.Evaluation;
using AspectN.Metadata;
using Mono.Cecil;

namespace AspectN.Cecil.Tests;

public sealed class CecilArrayShapeModelTests
{
    [Fact]
    public void MapType_RankOneShapes_PreservesShapeAndDetachedRelations()
    {
        using var module = ModuleDefinition.CreateModule("ArrayShapes", ModuleKind.Dll);
        using var system = new CecilTypeSystem(module, new CecilMappingOptions(leaveOpen: true));
        var vectorReference = new ArrayType(module.TypeSystem.Int32);
        var generalReference = new ArrayType(module.TypeSystem.Int32);
        generalReference.Dimensions[0] = new ArrayDimension(0, null);
        var vector = Assert.IsType<ArrayTypeSignature>(system.Mapper.MapType(vectorReference));
        var general = Assert.IsType<ArrayTypeSignature>(system.Mapper.MapType(generalReference));

        Assert.Equal(ArrayShape.Vector, vector.Shape);
        Assert.Equal(ArrayShape.General, general.Shape);
        Assert.Equal(MatchStatus.NotMatched, system.AreEqual(vector, general).Status);
        Assert.Equal(MatchStatus.Matched, system.IsAssignableTo(new ArrayTypeSignature(vector.ElementType, 1),
            new ArrayTypeSignature(general.ElementType, 1, ArrayShape.General)).Status);
        Assert.Equal(MatchStatus.NotMatched, system.IsAssignableTo(new ArrayTypeSignature(general.ElementType, 1, ArrayShape.General),
            new ArrayTypeSignature(vector.ElementType, 1)).Status);
    }
}
