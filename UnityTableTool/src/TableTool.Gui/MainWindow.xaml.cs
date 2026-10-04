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
    private IReadOnlyList<TableDocument> documents = Array.Empty<TableDocument>();
    private IReadOnlyList<ValidationIssue> issues = Array.Empty<ValidationIssue>();
    private bool hasScanErrors;

    public MainWindow()
    {
        InitializeComponent();
        TablePathBox.Text = Path.Combine(AppContext.BaseDirectory, "samples");
        DataOutputPathBox.Text = Path.Combine(AppContext.BaseDirectory, "samples", "Exported", "Data");
        CodeOutputPathBox.Text = Path.Combine(AppContext.BaseDirectory, "samples", "Exported", "Code");
    }

    private void Scan_Click(object sender, RoutedEventArgs e)
    {
        documents = Array.Empty<TableDocument>();
        issues = Array.Empty<ValidationIssue>();
        hasScanErrors = false;
        try
        {
            LogList.Items.Clear();
            var grids = TableFileReader.ReadDirectory(TablePathBox.Text.Trim());
            var parsed = new List<TableDocument>();
            foreach (var grid in grids)
            {
                try
                {
                    parsed.Add(parser.Parse(grid));
                }
                catch (Exception error)
                {
                    hasScanErrors = true;
                    LogList.Items.Add($"错误：{grid.SourceName}: {error.Message}");
                }
            }
            documents = parsed;
            issues = Array.Empty<ValidationIssue>();
            foreach (var document in documents)
                LogList.Items.Add($"已读取 {document.Schema.Name}，字段 {document.Schema.Fields.Count}，数据行 {document.Rows.Count}，{(document.Schema.IsSingleton ? "单例表" : "普通表")}");
        }
        catch (Exception error)
        {
            LogList.Items.Add($"错误：{error.Message}");
        }
    }

    private void Validate_Click(object sender, RoutedEventArgs e)
    {
        if (documents.Count == 0)
        {
            Scan_Click(sender, e);
            if (documents.Count == 0) return;
        }
        if (hasScanErrors)
        {
            LogList.Items.Add("扫描存在错误，修复后重新扫描。");
            return;
        }
        issues = documents.SelectMany(validator.Validate).ToArray();
        LogList.Items.Clear();
        foreach (var issue in issues)
            LogList.Items.Add($"{issue.Severity} {issue.Code}: {issue.Message} ({issue.SourceName}:{issue.SourceRow}:{issue.SourceColumn})");
        if (issues.Count == 0)
            LogList.Items.Add("校验通过");
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        Validate_Click(sender, e);
        if (hasScanErrors || documents.Count == 0 || issues.Any(issue => issue.Severity is ValidationSeverity.Error or ValidationSeverity.Fatal))
            return;
        var options = new ExportOptions(
            DataOutputPathBox.Text.Trim(),
            CodeOutputPathBox.Text.Trim(),
            CodeCheckBox.IsChecked == true,
            JsonCheckBox.IsChecked == true,
            BytesCheckBox.IsChecked == true);
        try
        {
            var result = new ExportService().ExportAll(documents, options);
            LogList.Items.Add($"导出完成，schema hash: {result.SchemaHash}");
            foreach (var file in result.Files)
                LogList.Items.Add(file);
        }
        catch (Exception error)
        {
            LogList.Items.Add($"导出错误：{error.Message}");
        }
    }
}
