using System.Text.Json;
using TableTool.Core.Models;

namespace TableTool.Serialization.Reports;

public sealed class ReportWriter
{
    public string WriteJson(string sourceName, IReadOnlyList<ValidationIssue> issues)
    {
        return JsonSerializer.Serialize(new { sourceName, issues }, new JsonSerializerOptions { WriteIndented = true });
    }

    public string WriteHtml(string sourceName, IReadOnlyList<ValidationIssue> issues)
    {
        var rows = string.Join(Environment.NewLine, issues.Select(issue =>
            $"<tr><td>{System.Net.WebUtility.HtmlEncode(issue.Severity.ToString())}</td><td>{System.Net.WebUtility.HtmlEncode(issue.Code)}</td><td>{System.Net.WebUtility.HtmlEncode(issue.Message)}</td></tr>"));
        return $"<html><body><h1>{System.Net.WebUtility.HtmlEncode(sourceName)}</h1><table><tr><th>Severity</th><th>Code</th><th>Message</th></tr>{rows}</table></body></html>";
    }
}
