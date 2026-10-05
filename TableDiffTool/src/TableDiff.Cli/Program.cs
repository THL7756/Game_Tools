// 用途：提供 Excel Diff 和双向替换的命令行入口。
// 编写时间：2026-10-05。
// 作者：Codex。
using TableDiff.Core.Models;
using TableDiff.Core.Services;

if (args.Length < 3 || !args[0].Equals("excel", StringComparison.OrdinalIgnoreCase))
{
    PrintUsage();
    return 2;
}

var command = args[1].ToLowerInvariant();
var leftPath = args[2];
var rightPath = args.Length > 3 ? args[3] : string.Empty;
var key = GetOption(args, "--key");
var output = GetOption(args, "--output");
var reportType = GetOption(args, "--report") ?? "text";

try
{
    var result = ExcelDiffEngine.Compare(leftPath, rightPath, new ExcelDiffOptions { KeyColumn = key });
    if (command == "compare")
    {
        var report = reportType.Equals("json", StringComparison.OrdinalIgnoreCase)
            ? DiffReportWriter.ToJson(result)
            : DiffReportWriter.ToText(result);
        if (string.IsNullOrWhiteSpace(output))
        {
            Console.WriteLine(report);
        }
        else
        {
            File.WriteAllText(output, report);
            Console.WriteLine($"Report written: {output}");
        }

        return result.Entries.Count == 0 ? 0 : 1;
    }

    if (command == "apply")
    {
        var directionText = GetOption(args, "--direction") ?? "left-to-right";
        var direction = directionText.Equals("right-to-left", StringComparison.OrdinalIgnoreCase)
            ? ApplyDirection.RightToLeft
            : ApplyDirection.LeftToRight;
        var apply = ExcelPatchApplier.Apply(leftPath, rightPath, result, direction, result.Entries);
        if (!apply.Succeeded)
        {
            foreach (var conflict in apply.Conflicts)
            {
                Console.Error.WriteLine($"Conflict: {conflict}");
            }

            return 3;
        }

        Console.WriteLine($"Applied {apply.AppliedCount} changes.");
        Console.WriteLine($"Backup: {apply.BackupPath}");
        return 0;
    }
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 4;
}

PrintUsage();
return 2;

static string? GetOption(string[] arguments, string name)
{
    var index = Array.FindIndex(arguments, argument => argument.Equals(name, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
}

static void PrintUsage()
{
    Console.WriteLine("tablediff excel compare <left.xlsx> <right.xlsx> [--key id] [--report text|json] [--output report]");
    Console.WriteLine("tablediff excel apply <left.xlsx> <right.xlsx> --direction left-to-right|right-to-left [--key id]");
}
