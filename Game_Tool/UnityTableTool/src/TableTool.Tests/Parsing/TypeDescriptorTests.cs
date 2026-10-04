using TableTool.Core.Parsing;
using Xunit;

namespace TableTool.Tests.Parsing;

public sealed class TypeDescriptorTests
{
    [Theory]
    [InlineData("int", "int", 0)]
    [InlineData("int()", "int", 1)]
    [InlineData("int()()", "int", 2)]
    [InlineData("int()()()", "int", 3)]
    public void ParsesBaseTypeAndArrayDimensions(string text, string baseType, int dimensions)
    {
        var descriptor = TypeDescriptor.Parse(text);

        Assert.Equal(baseType, descriptor.BaseType);
        Assert.Equal(dimensions, descriptor.Dimensions);
    }

    [Fact]
    public void RejectsMoreThanThreeDimensions()
    {
        Assert.Throws<FormatException>(() => TypeDescriptor.Parse("int()()()()"));
    }
}
