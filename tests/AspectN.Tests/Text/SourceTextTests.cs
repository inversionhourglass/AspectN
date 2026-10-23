using AspectN.Text;

namespace AspectN.Tests.Text;

public sealed class SourceTextTests
{
    [Fact]
    public void End_ValidSpan_ReturnsStartPlusLength()
    {
        Assert.Equal(7, new TextSpan(2, 5).End);
    }

    [Fact]
    public void Equals_IdenticalComponents_ReturnsTrue()
    {
        Assert.Equal(new TextSpan(2, 5), new TextSpan(2, 5));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void TextSpan_NegativeComponent_Throws(int start, int length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextSpan(start, length));
    }

    [Fact]
    public void From_NonEmptyText_PreservesContent()
    {
        var source = SourceText.From("method(*)");

        Assert.Equal(9, source.Length);
        Assert.Equal('m', source[0]);
        Assert.Equal("method(*)", source.ToString());
        Assert.Equal("method", source.Substring(new TextSpan(0, 6)));
    }
}
