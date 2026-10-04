using System.Windows;
using TableTool.Core.Models;
using TableTool.Core.Parsing;
using TableTool.Core.Reading;
using TableTool.Core.Validation;
using TableTool.Gui.Rules;
using TableTool.Serialization;

namespace TableTool.Gui;

public partial class MainWindow : Window
{
    private readonly TableSchemaParser parser = new();
    private readonly TableValidator validator = new();
    private TableDocument? current;
    private IReadOnlyList<ValidationIssue> issues = Array.Empty<ValidationIssue>();

    public MainWindow()
    {
        InitializeComponent();
        TablePathBox.Text = Path.Combine(AppContext.BaseDirectory, "samples", "Skill.csv");
        OutputPathBox.Text = Path.Combine(AppContext.BaseDirectory, "samples", "Exported");
    }

    private void Scan_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var grids = TableFileReader.Read(TablePathBox.Text.Trim());
            current = parser.Parse(grids[0]);
            issues = Array.Empty<ValidationIssue>();
            LogList.Items.Clear();
            LogList.Items.Add($"已读取 {current.Schema.Name}，字段 {current.Schema.Fields.Count}，数据行 {current.Rows.Count}");
        }
        catch (Exception error)
        {
            LogList.Items.Add($"错误：{error.Message}");
        }
    }

    private void Validate_Click(object sender, RoutedEventArgs e)
    {
        if (current is null)
        {
            Scan_Click(sender, e);
            if (current is null) return;
        }
        issues = validator.Validate(current);
        LogList.Items.Clear();
        foreach (var issue in issues)
            LogList.Items.Add($"{issue.Severity} {issue.Code}: {issue.Message} ({issue.SourceName}:{issue.SourceRow}:{issue.SourceColumn})");
        if (issues.Count == 0)
            LogList.Items.Add("校验通过");
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        Validate_Click(sender, e);
        if (current is null || issues.Any(issue => issue.Severity is ValidationSeverity.Error or ValidationSeverity.Fatal))
            return;
        var options = new ExportOptions(OutputPathBox.Text.Trim(), CodeCheckBox.IsChecked == true, JsonCheckBox.IsChecked == true, BytesCheckBox.IsChecked == true);
        var result = new ExportService().Export(current, options);
        LogList.Items.Add($"导出完成，schema hash: {result.SchemaHash}");
        foreach (var file in result.Files)
            LogList.Items.Add(file);
    }
}
