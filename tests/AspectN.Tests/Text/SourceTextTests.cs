using AspectN.Text;

namespace AspectN.Tests.Text;

public sealed class SourceTextTests
{
    [Fact]
    public void TextSpan_End_Is_Start_Plus_Length()
    {
        Assert.Equal(7, new TextSpan(2, 5).End);
    }

    [Fact]
    public void TextSpan_EqualValues_Are_Equal()
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
    public void SourceText_From_Preserves_Content()
    {
        var source = SourceText.From("method(*)");

        Assert.Equal(9, source.Length);
        Assert.Equal('m', source[0]);
        Assert.Equal("method(*)", source.ToString());
        Assert.Equal("method", source.Substring(new TextSpan(0, 6)));
    }
}
