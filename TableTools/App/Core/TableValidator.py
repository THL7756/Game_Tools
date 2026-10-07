from pathlib import Path
from typing import Any

from App.Core.Models import (
    LogicalTable,
    SeverityLevel,
    ValidationIssue,
)


def ResolveLogicalTables(
    SelectedFiles: set[Path],
    LogicalTables: dict[str, LogicalTable],
) -> dict[str, LogicalTable]:
    SelectedNames = {
        TableName
        for TableName, Table in LogicalTables.items()
        if any(str(SourceFile) in SelectedFiles for SourceFile in Table.SourceFiles)
    }
    ReferenceMap = BuildReferenceMap(LogicalTables)
    ResolvedNames: set[str] = set()
    Pending = list(SelectedNames)
    while Pending:
        TableName = Pending.pop()
        if TableName in ResolvedNames:
            continue
        ResolvedNames.add(TableName)
        Table = LogicalTables.get(TableName)
        if Table is None:
            continue
        for Field in Table.Fields:
            TargetName = ReferenceMap.get(Field.Name.lower())
            if TargetName and TargetName not in ResolvedNames:
                Pending.append(TargetName)
    return {Name: LogicalTables[Name] for Name in ResolvedNames if Name in LogicalTables}


def BuildReferenceMap(LogicalTables: dict[str, LogicalTable]) -> dict[str, str]:
    return {
        (TableName.lower() + "_id"): TableName
        for TableName in LogicalTables
    }


def ValidateLogicalTables(
    LogicalTables: dict[str, LogicalTable],
    AllLogicalTables: dict[str, LogicalTable],
) -> list[ValidationIssue]:
    Issues: list[ValidationIssue] = []
    ReferenceMap = BuildReferenceMap(AllLogicalTables)
    KeySets: dict[str, set[str]] = {}

    for TableName, Table in LogicalTables.items():
        if Table.IsSingle:
            continue
        PrimaryField = Table.PrimaryFieldName
        SeenKeys: dict[str, Any] = {}
        KeySet: set[str] = set()
        for Row in Table.Rows:
            Key = Row.Values.get(PrimaryField)
            KeyText = "" if Key is None else str(Key)
            if not KeyText:
                Issues.append(
                    ValidationIssue(
                        SeverityLevel.Error,
                        "主键为空",
                        Row.Location,
                        PrimaryField,
                    )
                )
                continue
            if KeyText in SeenKeys:
                Issues.append(
                    ValidationIssue(
                        SeverityLevel.Error,
                        f"主键重复：{KeyText}",
                        Row.Location,
                        PrimaryField,
                    )
                )
            else:
                SeenKeys[KeyText] = Row
                KeySet.add(KeyText)
        KeySets[TableName] = KeySet

    for TableName, Table in LogicalTables.items():
        for Row in Table.Rows:
            for Field in Table.Fields:
                TargetName = ReferenceMap.get(Field.Name.lower())
                if not TargetName:
                    continue
                Value = Row.Values.get(Field.Name)
                ValueText = "" if Value is None else str(Value)
                if not ValueText:
                    continue
                TargetTable = AllLogicalTables.get(TargetName)
                if TargetTable is None or TargetTable.IsSingle:
                    Issues.append(
                        ValidationIssue(
                            SeverityLevel.Error,
                            f"引用目标 {TargetName} 不存在或不是普通表",
                            Row.Location,
                            Field.Name,
                        )
                    )
                    continue
                TargetKeys = KeySets.get(TargetName)
                if TargetKeys is None:
                    TargetKeys = {
                        str(TargetRow.Values.get(TargetTable.PrimaryFieldName))
                        for TargetRow in TargetTable.Rows
                    }
                if ValueText not in TargetKeys:
                    Issues.append(
                        ValidationIssue(
                            SeverityLevel.Error,
                            f"引用的 {TargetName} id 未找到：{ValueText}",
                            Row.Location,
                            Field.Name,
                        )
                    )
    return Issues


def HasBlockingIssue(Issues: list[ValidationIssue]) -> bool:
    return any(Issue.Severity == SeverityLevel.Error for Issue in Issues)
