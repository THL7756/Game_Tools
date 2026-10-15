// 用途：展开当前选择表的引用闭包，并提供缺失引用的来源详情。
// 编写日期：2026-10-08
// 作者：Codex（按用户需求修改）

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Text.Json;

using TableTool.Core.Models;

namespace TableTool.Core.Parsing
{
    public sealed record MissingReferenceDetail(
        string SourceName,
        string FieldName,
        string ReferencedTableName);

    public static class TableSelectionResolver
    {
        public static IReadOnlyList<TableDocument> Expand(
            IEnumerable<TableDocument> documents,
            IEnumerable<string> selectedSourceNames)
        {
            var all = documents.ToArray();
            var selectedSources = new HashSet<string>(selectedSourceNames, StringComparer.OrdinalIgnoreCase);
            var selectedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var document in all)
            {
                if (selectedSources.Contains(document.SourceName))
                    selectedTables.Add(document.Schema.Name);
            }

            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var table in all.Where(document => selectedTables.Contains(document.Schema.Name)))
                {
                    foreach (var field in table.Schema.AllFields)
                    {
                        foreach (var reference in ComplexTypeReferenceWalker.GetReferences(field)
                                     .Where(reference => !reference.IsLocalization
                                         && reference.FieldName.EndsWith("_id", StringComparison.OrdinalIgnoreCase)))
                        {
                            var referencedName = reference.FieldName.Split('.').Last()[..^3];
                            var referenced = all.FirstOrDefault(document =>
                                document.Schema.Name.Equals(referencedName, StringComparison.OrdinalIgnoreCase));
                            if (referenced is not null && selectedTables.Add(referenced.Schema.Name))
                                changed = true;
                        }

                        if (ComplexTypeReferenceWalker.GetReferences(field).Any(reference => reference.IsLocalization))
                        {
                            var localization = all.FirstOrDefault(document =>
                                document.Schema.Name.Equals("localization", StringComparison.OrdinalIgnoreCase));
                            if (localization is not null && selectedTables.Add(localization.Schema.Name))
                                changed = true;
                        }
                    }
                }
            }

            return all.Where(document => selectedTables.Contains(document.Schema.Name)).ToArray();
        }

        public static IReadOnlyList<string> FindMissingReferences(
            IEnumerable<TableDocument> allDocuments,
            IEnumerable<TableDocument> selectedDocuments)
        {
            return FindMissingReferenceDetails(allDocuments, selectedDocuments)
                .Select(detail => detail.ReferencedTableName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static IReadOnlyList<MissingReferenceDetail> FindMissingReferenceDetails(
            IEnumerable<TableDocument> allDocuments,
            IEnumerable<TableDocument> selectedDocuments)
        {
            var available = allDocuments
                .Select(document => document.Schema.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return selectedDocuments
                .SelectMany(document => document.Schema.AllFields.Select(field => (Document: document, Field: field)))
                .SelectMany(item => ComplexTypeReferenceWalker.GetReferences(item.Field)
                    .Where(reference => !reference.IsLocalization
                        && reference.FieldName.EndsWith("_id", StringComparison.OrdinalIgnoreCase))
                    .Select(reference => new MissingReferenceDetail(
                        item.Document.SourceName,
                        reference.FieldName,
                        reference.FieldName.Split('.').Last()[..^3])))
                .Where(detail => !available.Contains(detail.ReferencedTableName))
                .GroupBy(
                    detail => (detail.SourceName, detail.FieldName, detail.ReferencedTableName))
                .Select(group => group.First())
                .ToArray();
        }

        public static string GetSourceFileName(string sourceName)
        {
            var separator = sourceName.IndexOf("::", StringComparison.Ordinal);
            var fileName = separator >= 0 ? sourceName[..separator] : sourceName;
            return Path.GetFileName(fileName);
        }
    }
}
