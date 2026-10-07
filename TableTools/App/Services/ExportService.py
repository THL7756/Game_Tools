# 用途：根据校验结果导出客户端/服务器 JSON 和客户端 C# 文件。
# 最近修改日期：2026-10-07
# 作者：Codex

import json
from datetime import datetime, timezone
from pathlib import Path

from App.Core.Models import (
    ExportRequest,
    ExportResult,
    LogicalTable,
    ParsedProject,
)
from App.Core.TableValidator import HasBlockingIssue, NormalizePath, ResolveLogicalTables, ValidateLogicalTables
from App.Services.CSharpGenerator import GenerateClientCSharp
from App.Services.GeneratedFileManifest import GeneratedFileManifest


def ExportProject(
    Project: ParsedProject,
    Request: ExportRequest,
    OutputDirectories: dict[str, Path],
    Manifest: GeneratedFileManifest,
) -> ExportResult:
    SelectedTables = ResolveLogicalTables(Request.SelectedFiles, Project.LogicalTables)
    if not SelectedTables:
        return ExportResult(False, [], ["请先勾选源文件"])

    RelevantFiles = {
        SourceFile
        for Table in SelectedTables.values()
        for SourceFile in Table.SourceFiles
    }
    RelevantKeys = {NormalizePath(FilePath) for FilePath in RelevantFiles}
    Issues = [
        Issue
        for Issue in Project.Issues
        if NormalizePath(Issue.Location.FilePath) in RelevantKeys
    ]
    Issues.extend(ValidateLogicalTables(SelectedTables, Project.LogicalTables))
    if HasBlockingIssue(Issues):
        return ExportResult(False, [], [Issue.Format() for Issue in Issues if Issue.Severity.value == "Error"])

    Result = ExportResult(True)
    if Request.Targets.ClientData:
        ExportJsonTables(SelectedTables, "c", OutputDirectories["ClientData"], "ClientData", Manifest, Result)
    if Request.Targets.ServerData:
        ExportJsonTables(SelectedTables, "s", OutputDirectories["ServerData"], "ServerData", Manifest, Result)
    if Request.Targets.ClientCode:
        ExportClientCode(SelectedTables, "c", OutputDirectories["ClientCode"], Manifest, Result)
    if Request.Targets.ServerCode:
        Result.Success = False
        Result.Messages.append("服务器代码生成尚未实现，本次未生成服务器代码文件")

    if not Result.WrittenFiles and Result.Success:
        Result.Success = False
        Result.Messages.append("当前目标没有需要导出的内容")
    Result.Messages.insert(0, f"已处理 {len(SelectedTables)} 个逻辑表，生成 {len(Result.WrittenFiles)} 个文件")
    return Result


def ExportJsonTables(
    Tables: dict[str, LogicalTable],
    Side: str,
    OutputDirectory: Path,
    Target: str,
    Manifest: GeneratedFileManifest,
    Result: ExportResult,
) -> None:
    for TableName, Table in sorted(Tables.items()):
        Projected = ProjectTable(Table, Side)
        if Projected is None:
            continue
        Data = BuildJsonData(Projected)
        Content = json.dumps(Data, ensure_ascii=False, indent=2) + "\n"
        OutputPath = OutputDirectory / f"{TableName}.json"
        Manifest.ReplaceGeneratedFile(
            Target,
            TableName,
            "Json",
            OutputPath,
            Content,
            OutputDirectory,
        )
        Result.WrittenFiles.append(OutputPath)


def ExportClientCode(
    Tables: dict[str, LogicalTable],
    Side: str,
    OutputDirectory: Path,
    Manifest: GeneratedFileManifest,
    Result: ExportResult,
) -> None:
    for TableName, Table in sorted(Tables.items()):
        Projected = ProjectTable(Table, Side)
        if Projected is None:
            continue
        Content = GenerateClientCSharp(Projected)
        OutputPath = OutputDirectory / f"{TableName}_config.cs"
        Manifest.ReplaceGeneratedFile(
            "ClientCode",
            TableName,
            "CSharp",
            OutputPath,
            Content,
            OutputDirectory,
        )
        Result.WrittenFiles.append(OutputPath)


def ProjectTable(Table: LogicalTable, Side: str) -> LogicalTable | None:
    Fields = [
        Field
        for Field in Table.Fields
        if Field.Scope in {"cs", Side}
    ]
    if not Fields:
        return None
    FieldNames = {Field.Name for Field in Fields}
    Rows = [
        type(Row)(
            {Name: Value for Name, Value in Row.Values.items() if Name in FieldNames},
            Row.Location,
        )
        for Row in Table.Rows
    ]
    return LogicalTable(Table.Name, Table.IsSingle, Fields, Rows, set(Table.SourceFiles))


def BuildJsonData(Table: LogicalTable) -> dict:
    if Table.IsSingle:
        Data: dict[str, object] = {}
        for Row in Table.Rows:
            Data.update(Row.Values)
        return Data
    return {"Rows": [dict(Row.Values) for Row in Table.Rows]}


def BuildExportTimestamp() -> str:
    return datetime.now(timezone.utc).isoformat()
