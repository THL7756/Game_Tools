# 用途：保存、读取和校验表格解析缓存，源文件或数组配置变化时自动失效。
# 最近修改日期：2026-10-07
# 作者：Codex

import json
import os
from pathlib import Path
from typing import Any

from App.Core.Models import (
    ArraySyntaxConfig,
    FieldDefinition,
    LogicalTable,
    ParsedProject,
    ParsedType,
    SeverityLevel,
    SheetPreview,
    SourceFileModel,
    SourceLocation,
    TableRow,
    ValidationIssue,
)


class TableCacheService:
    # 解析规则和 schema 诊断逻辑发生过变化，旧缓存必须重建，避免显示历史误报。
    CacheVersion = 5

    def __init__(self, CachePath: Path) -> None:
        self.CachePath = CachePath

    def Save(self, Project: ParsedProject, RootDirectory: Path, Syntax: ArraySyntaxConfig) -> None:
        Envelope = {
            "SchemaVersion": self.CacheVersion,
            "SourceDirectory": str(RootDirectory),
            "ArraySyntax": self._SyntaxToDict(Syntax),
            "Files": [self._FileToDict(File) for File in Project.SourceFiles],
            "FileSignatures": {
                str(File.Path): self._Signature(File.Path, File.ModifiedTime)
                for File in Project.SourceFiles
            },
            "LogicalTables": [self._TableToDict(Table) for Table in Project.LogicalTables.values()],
            "Issues": [self._IssueToDict(Issue) for Issue in Project.Issues],
        }
        try:
            self.CachePath.parent.mkdir(parents=True, exist_ok=True)
            TempPath = self.CachePath.with_name(self.CachePath.name + ".tmp")
            TempPath.write_text(json.dumps(Envelope, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
            os.replace(TempPath, self.CachePath)
        except OSError:
            return

    def Load(self, RootDirectory: Path, Syntax: ArraySyntaxConfig) -> ParsedProject | None:
        if not self.CachePath.exists():
            return None
        try:
            Envelope = json.loads(self.CachePath.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            return None
        if Envelope.get("SchemaVersion") != self.CacheVersion:
            return None
        if Path(str(Envelope.get("SourceDirectory", ""))) != RootDirectory:
            return None
        if Envelope.get("ArraySyntax") != self._SyntaxToDict(Syntax):
            return None
        Files = [self._FileFromDict(Value) for Value in Envelope.get("Files", [])]
        if not self._FilesMatch(Files, RootDirectory, Envelope.get("FileSignatures", {})):
            return None
        Tables = {}
        for Value in Envelope.get("LogicalTables", []):
            Table = self._TableFromDict(Value)
            Tables[Table.Name] = Table
        Issues = [self._IssueFromDict(Value) for Value in Envelope.get("Issues", [])]
        return ParsedProject(Files, Tables, Issues)

    def _Signature(self, FilePath: Path, ModifiedTime: float) -> dict[str, float | int]:
        try:
            return {"ModifiedTime": ModifiedTime, "Size": FilePath.stat().st_size}
        except OSError:
            return {"ModifiedTime": ModifiedTime, "Size": -1}

    def _FilesMatch(self, Files: list[SourceFileModel], RootDirectory: Path, Signatures: dict[str, Any]) -> bool:
        CurrentPaths = sorted(
            PathValue
            for PathValue in RootDirectory.rglob("*")
            if PathValue.is_file()
            and PathValue.suffix.lower() in {".xlsx", ".xls"}
            and not PathValue.name.startswith("~$")
        )
        CachedPaths = sorted(File.Path for File in Files)
        if CurrentPaths != CachedPaths:
            return False
        for File in Files:
            try:
                Stat = File.Path.stat()
            except OSError:
                return False
            Signature = Signatures.get(str(File.Path), {})
            if abs(Stat.st_mtime - float(Signature.get("ModifiedTime", File.ModifiedTime))) > 0.0001:
                return False
            if int(Stat.st_size) != int(Signature.get("Size", Stat.st_size)):
                return False
        return True

    def _SyntaxToDict(self, Syntax: ArraySyntaxConfig) -> dict[str, str]:
        return {
            "Level1Delimiter": Syntax.Level1Delimiter,
            "Level2Delimiter": Syntax.Level2Delimiter,
            "Level3Delimiter": Syntax.Level3Delimiter,
            "EscapeCharacter": Syntax.EscapeCharacter,
        }

    def _LocationToDict(self, Location: SourceLocation) -> dict[str, Any]:
        return {"FilePath": str(Location.FilePath), "SheetName": Location.SheetName, "Row": Location.Row, "Column": Location.Column}

    def _LocationFromDict(self, Value: dict[str, Any]) -> SourceLocation:
        return SourceLocation(Path(str(Value.get("FilePath", ""))), str(Value.get("SheetName", "")), int(Value.get("Row", 0)), int(Value.get("Column", 0)))

    def _IssueToDict(self, Issue: ValidationIssue) -> dict[str, Any]:
        return {"Severity": Issue.Severity.value, "Message": Issue.Message, "Location": self._LocationToDict(Issue.Location), "FieldName": Issue.FieldName}

    def _IssueFromDict(self, Value: dict[str, Any]) -> ValidationIssue:
        try:
            Severity = SeverityLevel(str(Value.get("Severity", SeverityLevel.Error.value)))
        except ValueError:
            Severity = SeverityLevel.Error
        return ValidationIssue(Severity, str(Value.get("Message", "")), self._LocationFromDict(Value.get("Location", {})), str(Value.get("FieldName", "")))

    def _TypeToDict(self, Type: ParsedType) -> dict[str, Any]:
        return {"RawText": Type.RawText, "BaseName": Type.BaseName, "Dimensions": Type.Dimensions, "EnumValues": list(Type.EnumValues)}

    def _TypeFromDict(self, Value: dict[str, Any]) -> ParsedType:
        return ParsedType(str(Value.get("RawText", "")), str(Value.get("BaseName", "string")), int(Value.get("Dimensions", 0)), tuple(Value.get("EnumValues", [])))

    def _FieldToDict(self, Field: FieldDefinition) -> dict[str, Any]:
        return {"Name": Field.Name, "Description": Field.Description, "TypeText": Field.TypeText, "Scope": Field.Scope, "DefaultText": Field.DefaultText, "ColumnIndex": Field.ColumnIndex, "ParsedType": self._TypeToDict(Field.ParsedType)}

    def _FieldFromDict(self, Value: dict[str, Any]) -> FieldDefinition:
        return FieldDefinition(str(Value.get("Name", "")), str(Value.get("Description", "")), str(Value.get("TypeText", "")), str(Value.get("Scope", "cs")), str(Value.get("DefaultText", "")), int(Value.get("ColumnIndex", 0)), self._TypeFromDict(Value.get("ParsedType", {})))

    def _SheetToDict(self, Sheet: SheetPreview) -> dict[str, Any]:
        return {
            "SheetName": Sheet.SheetName,
            "TableName": Sheet.TableName,
            "IsSingle": Sheet.IsSingle,
            "Headers": Sheet.Headers,
            "Rows": Sheet.Rows,
            "Fields": [self._FieldToDict(Field) for Field in Sheet.Fields],
            "TypeLabels": Sheet.TypeLabels,
            "ScopeLabels": Sheet.ScopeLabels,
            "RowNumbers": Sheet.RowNumbers,
            "CellIssues": {f"{Row}:{Column}": self._IssueToDict(Issue) for (Row, Column), Issue in Sheet.CellIssues.items()},
            "ParsedRows": Sheet.ParsedRows,
        }

    def _SheetFromDict(self, Value: dict[str, Any]) -> SheetPreview:
        CellIssues = {}
        for Key, Issue in Value.get("CellIssues", {}).items():
            Row, Column = (int(Item) for Item in str(Key).split(":", 1))
            CellIssues[(Row, Column)] = self._IssueFromDict(Issue)
        return SheetPreview(str(Value.get("SheetName", "")), str(Value.get("TableName", "")), bool(Value.get("IsSingle", False)), list(Value.get("Headers", [])), list(Value.get("Rows", [])), [self._FieldFromDict(Item) for Item in Value.get("Fields", [])], list(Value.get("TypeLabels", [])), list(Value.get("ScopeLabels", [])), [int(Item) for Item in Value.get("RowNumbers", [])], CellIssues, list(Value.get("ParsedRows", [])))

    def _FileToDict(self, File: SourceFileModel) -> dict[str, Any]:
        return {"Path": str(File.Path), "ModifiedTime": File.ModifiedTime, "Sheets": [self._SheetToDict(Sheet) for Sheet in File.Sheets], "LogicalTableNames": File.LogicalTableNames}

    def _FileFromDict(self, Value: dict[str, Any]) -> SourceFileModel:
        return SourceFileModel(Path(str(Value.get("Path", ""))), float(Value.get("ModifiedTime", 0)), [self._SheetFromDict(Item) for Item in Value.get("Sheets", [])], list(Value.get("LogicalTableNames", [])))

    def _TableToDict(self, Table: LogicalTable) -> dict[str, Any]:
        return {"Name": Table.Name, "IsSingle": Table.IsSingle, "Fields": [self._FieldToDict(Field) for Field in Table.Fields], "Rows": [{"Values": Row.Values, "Location": self._LocationToDict(Row.Location)} for Row in Table.Rows], "SourceFiles": [str(PathValue) for PathValue in Table.SourceFiles]}

    def _TableFromDict(self, Value: dict[str, Any]) -> LogicalTable:
        Rows = [TableRow(dict(Item.get("Values", {})), self._LocationFromDict(Item.get("Location", {}))) for Item in Value.get("Rows", [])]
        return LogicalTable(str(Value.get("Name", "")), bool(Value.get("IsSingle", False)), [self._FieldFromDict(Item) for Item in Value.get("Fields", [])], Rows, {Path(Item) for Item in Value.get("SourceFiles", [])})
