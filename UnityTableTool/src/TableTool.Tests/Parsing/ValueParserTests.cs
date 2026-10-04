using TableTool.Core.Parsing;
using Xunit;

namespace TableTool.Tests.Parsing;

public sealed class ValueParserTests
{
    [Fact]
    public void ParsesOneDimensionalIntArrayUsingHash()
    {
        var type = TypeDescriptor.Parse("int()");

        var value = ValueParser.Parse("1#2#3", type);

        Assert.Equal(new[] { 1, 2, 3 }, (int[])value);
    }

    [Fact]
    public void ParsesTwoDimensionalIntArrayUsingHashThenPipe()
    {
        var type = TypeDescriptor.Parse("int()()");

        var value = ValueParser.Parse("1|2#3|4", type);

        Assert.Equal(new[] { new[] { 1, 2 }, new[] { 3, 4 } }, (int[][])value);
    }

    [Fact]
    public void ParsesThreeDimensionalIntArrayUsingHashPipeThenUnderscore()
    {
        var type = TypeDescriptor.Parse("int()()()");

        var value = ValueParser.Parse("1|2_3#4|5_6", type);

        Assert.Equal(new[] { new[] { new[] { 1, 2 }, new[] { 3 } }, new[] { new[] { 4, 5 }, new[] { 6 } } }, (int[][][])value);
    }

    [Fact]
    public void RejectsHighDimensionalSeparatorForOneDimensionalType()
    {
        var type = TypeDescriptor.Parse("int()");

        var error = Assert.Throws<FormatException>(() => ValueParser.Parse("1|2", type));

        Assert.Contains("separator", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
