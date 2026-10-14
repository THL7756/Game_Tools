// 用途：提供 Unity Editor 内的配置表列表、刷新、选择和打表窗口。
// 编写日期：2026-10-14
// 作者：Codex（按用户需求修改）

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using TableTool.Core.Models;
using UnityEditor;
using UnityEngine;

namespace Company.UnityTableTool.Editor;

public sealed class UnityTableToolWindow : EditorWindow
{
    private UnityTableToolSettings? settings;
    private UnityTableCatalog? catalog;
    private Vector2 tableScroll;
    private string status = "请选择设置资产，然后点击“刷新表列表”。";

    [MenuItem("Tools/UnityTableTool/Table Tool")]
    public static void Open()
    {
        var window = GetWindow<UnityTableToolWindow>();
        window.titleContent = new GUIContent("Unity Table Tool");
        window.minSize = new Vector2(620, 440);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Unity Table Tool", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "表扫描只在点击刷新或打表时执行，避免界面重绘和切换造成重复加载。",
            MessageType.Info);
        settings = (UnityTableToolSettings?)EditorGUILayout.ObjectField(
            "设置资产",
            settings,
            typeof(UnityTableToolSettings),
            false);

        using (new EditorGUI.DisabledScope(settings is null))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("刷新表列表", GUILayout.Height(28)))
                RefreshCatalog();
            if (GUILayout.Button("全选", GUILayout.Height(28)))
                SetSelection(true);
            if (GUILayout.Button("清空选择", GUILayout.Height(28)))
                SetSelection(false);
            if (GUILayout.Button("开始打表", GUILayout.Height(28)))
                BuildTables();
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(6);
        if (catalog is not null)
        {
            EditorGUILayout.LabelField(
                $"表文件：{catalog.Tables.Count} · 已选择：{catalog.Tables.Count(table => table.IsSelected)}",
                EditorStyles.miniLabel);
            tableScroll = EditorGUILayout.BeginScrollView(tableScroll);
            foreach (var table in catalog.Tables)
            {
                EditorGUILayout.BeginHorizontal();
                table.IsSelected = EditorGUILayout.Toggle(table.IsSelected, GUILayout.Width(18));
                EditorGUILayout.LabelField(table.DisplayName);
                EditorGUILayout.LabelField(
                    $"{table.Sheets.Count} Sheet · {table.SourcePath}",
                    EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField(status, EditorStyles.wordWrappedLabel);
    }

    private void RefreshCatalog()
    {
        if (settings is null)
            return;

        try
        {
            var selectedKeys = catalog?.Tables
                .Where(table => table.IsSelected)
                .Select(table => table.FileKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            catalog = UnityTableToolEditorApi.LoadCatalog(settings);
            if (selectedKeys is not null)
            {
                foreach (var table in catalog.Tables)
                    table.IsSelected = selectedKeys.Contains(table.FileKey);
            }
            status = FormatCatalogStatus(catalog);
            Repaint();
        }
        catch (Exception error)
        {
            status = $"刷新失败：{error.Message}";
            Debug.LogException(error);
        }
    }

    private void BuildTables()
    {
        if (settings is null)
            return;
        RefreshCatalog();
        if (catalog is null)
            return;

        var result = UnityTableToolEditorApi.Build(settings, catalog);
        var errors = result.Issues
            .Where(issue => issue.Severity is ValidationSeverity.Error or ValidationSeverity.Fatal)
            .Select(issue => $"{issue.SourceName}：{issue.Message}")
            .Concat(result.Errors)
            .ToArray();
        if (errors.Length > 0)
        {
            status = $"打表失败：{errors.Length} 个错误。";
            Debug.LogError(string.Join(Environment.NewLine, errors));
            EditorUtility.DisplayDialog("UnityTableTool", status, "确定");
            return;
        }

        status = $"打表完成：{result.RowCount} 行，{result.Files.Count} 个文件。";
        Debug.Log($"[UnityTableTool] {status}");
        EditorUtility.DisplayDialog("UnityTableTool", status, "确定");
    }

    private void SetSelection(bool selected)
    {
        if (catalog is null)
            return;
        foreach (var table in catalog.Tables)
            table.IsSelected = selected;
        Repaint();
    }

    private static string FormatCatalogStatus(UnityTableCatalog value)
    {
        var messages = new List<string>();
        if (value.Errors.Count > 0)
            messages.Add($"错误 {value.Errors.Count}");
        if (value.Infos.Count > 0)
            messages.Add($"信息 {value.Infos.Count}");
        return $"已读取 {value.Tables.Count} 个表文件。{(messages.Count == 0 ? string.Empty : " " + string.Join("，", messages) + "。")}";
    }
}
