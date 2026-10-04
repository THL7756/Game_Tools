using TableTool.Core.Models;
using TableTool.Core.Parsing;
using TableTool.Core.Validation;
using Xunit;

namespace TableTool.Tests.Validation;

public sealed class TableValidatorTests
{
    [Fact]
    public void ReportsDuplicatePrimaryKeysAndInvalidArrayValues()
    {
        var grid = new RawTableGrid("memory", new[]
        {
            (IReadOnlyList<string?>)["table: Skill"],
            (IReadOnlyList<string?>)["字段说明", "ID", "效果"],
            (IReadOnlyList<string?>)["字段类型", "int", "int()"],
            (IReadOnlyList<string?>)["字段名", "id", "effects"],
            (IReadOnlyList<string?>)["客户端服务器", "", ""],
            (IReadOnlyList<string?>)["默认值", "0", ""],
            (IReadOnlyList<string?>)["1", "1|2"],
            (IReadOnlyList<string?>)["1", "3#4"]
        });

        var document = new TableSchemaParser().Parse(grid);
        var issues = new TableValidator().Validate(document);

        Assert.Contains(issues, issue => issue.Code == ErrorCodes.PrimaryKeyDuplicate);
        Assert.Contains(issues, issue => issue.Code == ErrorCodes.FieldArraySeparatorInvalid);
    }
}
