using AspectN.Evaluation;
using AspectN.Metadata;
using AspectN.TypeSystem;

namespace AspectN.Tests.Metadata;

public sealed class ArrayShapeTests
{
    private static readonly TypeSignature _Element = new NamedTypeSignature(new TypeIdentity(
        new AssemblyIdentity("Example"), "Example", "Item", 0, TypeKind.Class));

    [Theory]
    [InlineData(1, ArrayShape.Vector)]
    [InlineData(2, ArrayShape.General)]
    [InlineData(3, ArrayShape.General)]
    public void Constructor_DefaultShape_PreservesSourceCompatibility(int rank, ArrayShape expected)
    {
        Assert.Equal(expected, new ArrayTypeSignature(_Element, rank).Shape);
    }

    [Theory]
    [InlineData(0, ArrayShape.Vector)]
    [InlineData(-1, ArrayShape.General)]
    [InlineData(2, ArrayShape.Vector)]
    [InlineData(1, (ArrayShape)2)]
    public void Constructor_InvalidRankOrShape_Throws(int rank, ArrayShape shape)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ArrayTypeSignature(_Element, rank, shape));
    }

    [Fact]
    public void AreEqual_RankOneShapes_AreDistinctInNeutralModel()
    {
        var vector = new ArrayTypeSignature(_Element, 1, ArrayShape.Vector);
        var general = new ArrayTypeSignature(_Element, 1, ArrayShape.General);

        Assert.Equal(MatchStatus.NotMatched, StructuralTypeComparer.AreEqual(vector, general).Status);
        Assert.Equal(MatchStatus.Matched, StructuralTypeComparer.AreEqual(general,
            new ArrayTypeSignature(_Element, 1, ArrayShape.General)).Status);
    }
}
