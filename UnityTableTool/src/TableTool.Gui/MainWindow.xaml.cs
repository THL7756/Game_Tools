using System.IO;
using System.Windows;
using System.Windows.Controls;
using TableTool.Core.Models;
using TableTool.Core.Parsing;
using TableTool.Core.Reading;
using TableTool.Core.Validation;
using TableTool.Gui.Rules;
using TableTool.Serialization;
using Microsoft.Win32;

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

    private void BrowseTablePath_Click(object sender, RoutedEventArgs e) => BrowseFolder(TablePathBox);
    private void BrowseDataOutputPath_Click(object sender, RoutedEventArgs e) => BrowseFolder(DataOutputPathBox);
    private void BrowseCodeOutputPath_Click(object sender, RoutedEventArgs e) => BrowseFolder(CodeOutputPathBox);

    private void BrowseFolder(TextBox target)
    {
        var dialog = new OpenFolderDialog();
        if (Directory.Exists(target.Text.Trim()))
            dialog.FolderName = target.Text.Trim();
        if (dialog.ShowDialog(this) == true)
            target.Text = dialog.FolderName;
    }

    private void Scan_Click(object sender, RoutedEventArgs e)
    {
        documents = Array.Empty<TableDocument>();
        issues = Array.Empty<ValidationIssue>();
        hasScanErrors = false;
        try
        {
            LogList.Items.Clear();
            var root = TablePathBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(root))
                throw new ArgumentException("请选择表根目录。");
            var grids = TableFileReader.ReadDirectory(root);
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
            documents = TableDocumentMerger.Merge(parsed);
            if (documents.Count == 0 && !hasScanErrors)
                LogList.Items.Add("未发现可解析的表格。");
            foreach (var document in documents)
                LogList.Items.Add($"已读取 {document.Schema.Name}，字段 {document.Schema.Fields.Count}，数据行 {document.Rows.Count}，{(document.Schema.IsSingleton ? "单例表" : "普通表")}");
        }
        catch (Exception error)
        {
            hasScanErrors = true;
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
        if (!EnsureOutputPaths())
            return;
        if (JsonCheckBox.IsChecked != true && BytesCheckBox.IsChecked != true && CodeCheckBox.IsChecked != true)
        {
            LogList.Items.Add("请至少选择一种导出格式。");
            return;
        }

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
            LogList.Items.Clear();
            LogList.Items.Add($"导出完成，schema hash: {result.SchemaHash}");
            foreach (var file in result.Files)
                LogList.Items.Add(file);
        }
        catch (Exception error)
        {
            LogList.Items.Add($"导出错误：{error.Message}");
        }
    }

    private bool EnsureOutputPaths()
    {
        var source = TablePathBox.Text.Trim();
        var data = DataOutputPathBox.Text.Trim();
        var code = CodeOutputPathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(data) || string.IsNullOrWhiteSpace(code))
        {
            LogList.Items.Add("表根目录、输出数据目录和输出代码目录都必须填写。");
            return false;
        }

        try
        {
            var sourcePath = Path.GetFullPath(source);
            var dataPath = Path.GetFullPath(data);
            var codePath = Path.GetFullPath(code);
            if (string.Equals(dataPath, codePath, StringComparison.OrdinalIgnoreCase))
            {
                LogList.Items.Add("输出数据目录和输出代码目录不能相同。");
                return false;
            }
            if (IsSameOrAncestor(dataPath, sourcePath) || IsSameOrAncestor(codePath, sourcePath))
            {
                LogList.Items.Add("输出目录不能等于表根目录或其父目录，以免删除源表。");
                return false;
            }
            if (IsSameOrAncestor(dataPath, codePath) || IsSameOrAncestor(codePath, dataPath))
            {
                LogList.Items.Add("输出数据目录和输出代码目录不能互相包含。");
                return false;
            }
            return true;
        }
        catch (Exception error)
        {
            LogList.Items.Add($"路径错误：{error.Message}");
            return false;
        }
    }

    private static bool IsSameOrAncestor(string candidate, string root) =>
        string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase)
        || root.StartsWith(candidate + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        || root.StartsWith(candidate + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
