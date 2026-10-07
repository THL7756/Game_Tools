from pathlib import Path
from typing import Any

from App.Core.Models import (
    FieldDefinition,
    LogicalTable,
    ParsedProject,
    SeverityLevel,
    SheetPreview,
    SourceFileModel,
    SourceLocation,
    TableSlice,
    TableRow,
    ValidationIssue,
)
from App.Core.TypeParser import ParseFieldType, ParseValue, TypeParseError


def ScanProject(RootDirectory: Path, EscapeCharacter: str = "\\") -> ParsedProject:
    Issues: list[ValidationIssue] = []
    SourceFiles: list[SourceFileModel] = []
    LogicalTables: dict[str, LogicalTable] = {}
    SourcePaths = sorted(
        Path for Path in RootDirectory.rglob("*")
        if Path.is_file()
        and Path.suffix.lower() in {".xlsx", ".xls"}
        and not Path.name.startswith("~$")
    )

    for FilePath in SourcePaths:
        try:
            Sheets = ReadWorkbookSheets(FilePath)
        except Exception as Error:
            Issues.append(
                ValidationIssue(
                    SeverityLevel.Error,
                    f"表格文件无法读取：{Error}",
                    SourceLocation(FilePath, ""),
                )
            )
            SourceFiles.append(SourceFileModel(FilePath, FilePath.stat().st_mtime, [], []))
            continue

        FileSlices: list[TableSlice] = []
        Previews: list[SheetPreview] = []
        for SheetName, Matrix in Sheets:
            Slice, SheetIssues = ParseTableSlice(FilePath, SheetName, Matrix, EscapeCharacter)
            Issues.extend(SheetIssues)
            if Slice is not None:
                FileSlices.append(Slice)
                Previews.append(BuildSheetPreview(Slice, Matrix))
            else:
                Previews.append(
                    SheetPreview(SheetName, "", False, [], [], [])
                )

        TableNames: list[str] = []
        for Slice in FileSlices:
            if Slice.Name not in TableNames:
                TableNames.append(Slice.Name)
            MergeTableSlice(Slice, LogicalTables, Issues)

        SourceFiles.append(
            SourceFileModel(
                FilePath,
                FilePath.stat().st_mtime,
                Previews,
                TableNames,
            )
        )

    return ParsedProject(SourceFiles, LogicalTables, Issues)


def ReadWorkbookSheets(FilePath: Path) -> list[tuple[str, list[list[Any]]]]:
    if FilePath.suffix.lower() == ".xlsx":
        import openpyxl

        Workbook = openpyxl.load_workbook(FilePath, read_only=True, data_only=False)
        try:
            Result: list[tuple[str, list[list[Any]]]] = []
            for Worksheet in Workbook.worksheets:
                Matrix = [list(Row) for Row in Worksheet.iter_rows(values_only=True)]
                Result.append((Worksheet.title, TrimMatrix(Matrix)))
            return Result
        finally:
            Workbook.close()

    if FilePath.suffix.lower() == ".xls":
        import xlrd

        Workbook = xlrd.open_workbook(FilePath, on_demand=True)
        try:
            Result = []
            for SheetIndex in range(Workbook.nsheets):
                Worksheet = Workbook.sheet_by_index(SheetIndex)
                Matrix = [
                    [Worksheet.cell_value(RowIndex, ColumnIndex) for ColumnIndex in range(Worksheet.ncols)]
                    for RowIndex in range(Worksheet.nrows)
                ]
                Result.append((Worksheet.name, TrimMatrix(Matrix)))
            return Result
        finally:
            Workbook.release_resources()

    raise ValueError(f"不支持的文件类型 {FilePath.suffix}")


def TrimMatrix(Matrix: list[list[Any]]) -> list[list[Any]]:
    while Matrix and all(Cell is None or str(Cell).strip() == "" for Cell in Matrix[-1]):
        Matrix.pop()
    if not Matrix:
        return []
    MaxColumn = max((len(Row) for Row in Matrix), default=0)
    while MaxColumn > 0 and all(
        len(Row) < MaxColumn or Row[MaxColumn - 1] is None or str(Row[MaxColumn - 1]).strip() == ""
        for Row in Matrix
    ):
        MaxColumn -= 1
    return [list(Row[:MaxColumn]) + [None] * max(0, MaxColumn - len(Row)) for Row in Matrix]


def ParseTableSlice(
    FilePath: Path,
    SheetName: str,
    Matrix: list[list[Any]],
    EscapeCharacter: str,
) -> tuple[TableSlice | None, list[ValidationIssue]]:
    Location = SourceLocation(FilePath, SheetName)
    Issues: list[ValidationIssue] = []
    if not Matrix or not Matrix[0]:
        Issues.append(ValidationIssue(SeverityLevel.Error, "Sheet 为空或缺少 table 定义", Location))
        return None, Issues

    Metadata = str(Matrix[0][0] or "")
    TableName = ""
    IsSingle = False
    for Line in Metadata.splitlines():
        Normalized = Line.strip()
        if Normalized.lower().startswith("table:"):
            TableName = Normalized.split(":", 1)[1].strip()
        elif Normalized.lower() == "type:single":
            IsSingle = True

    if not TableName:
        Issues.append(ValidationIssue(SeverityLevel.Error, "缺少 table: 逻辑表名", Location))
        return None, Issues

    if IsSingle:
        return ParseSingleSlice(FilePath, SheetName, TableName, Matrix, EscapeCharacter)
    return ParseNormalSlice(FilePath, SheetName, TableName, Matrix, EscapeCharacter)


def ParseNormalSlice(
    FilePath: Path,
    SheetName: str,
    TableName: str,
    Matrix: list[list[Any]],
    EscapeCharacter: str,
) -> tuple[TableSlice | None, list[ValidationIssue]]:
    Location = SourceLocation(FilePath, SheetName)
    Issues: list[ValidationIssue] = []
    if len(Matrix) < 6:
        Issues.append(ValidationIssue(SeverityLevel.Error, "普通表需要六行定义区", Location))
        return None, Issues

    Fields: list[FieldDefinition] = []
    MaxColumn = max(len(Row) for Row in Matrix[:6])
    for ColumnIndex in range(2, MaxColumn + 1):
        Name = CellText(Matrix, 4, ColumnIndex)
        if not Name or Name.startswith("##"):
            continue
        TypeText = CellText(Matrix, 3, ColumnIndex)
        try:
            ParsedType = ParseFieldType(TypeText)
        except TypeParseError as Error:
            Issues.append(
                ValidationIssue(
                    SeverityLevel.Error,
                    str(Error),
                    SourceLocation(FilePath, SheetName, 3, ColumnIndex),
                    Name,
                )
            )
            continue

        Scope = NormalizeScope(CellText(Matrix, 5, ColumnIndex), FilePath, SheetName, ColumnIndex, Name, Issues)
        Fields.append(
            FieldDefinition(
                Name,
                CellText(Matrix, 2, ColumnIndex),
                TypeText,
                Scope,
                CellText(Matrix, 6, ColumnIndex),
                ColumnIndex,
                ParsedType,
            )
        )

    if not Fields:
        Issues.append(ValidationIssue(SeverityLevel.Error, "普通表没有有效字段", Location))
        return None, Issues

    Rows: list[TableRow] = []
    for RowOffset in range(6, len(Matrix)):
        SourceRow = RowOffset + 1
        RowLabel = CellText(Matrix, SourceRow, 1)
        if RowLabel.startswith("##"):
            continue
        if IsEmptyRow(Matrix[RowOffset]):
            continue
        Values: dict[str, Any] = {}
        RowHasError = False
        for Field in Fields:
            RawValue = GetCell(Matrix, SourceRow, Field.ColumnIndex)
            ValueText = "" if RawValue is None else str(RawValue).strip()
            if ValueText == "":
                ValueText = Field.DefaultText
            try:
                Values[Field.Name] = ParseValue(ValueText, Field.ParsedType, EscapeCharacter)
            except TypeParseError as Error:
                RowHasError = True
                Issues.append(
                    ValidationIssue(
                        SeverityLevel.Error,
                        str(Error),
                        SourceLocation(FilePath, SheetName, SourceRow, Field.ColumnIndex),
                        Field.Name,
                    )
                )
        if not RowHasError:
            Rows.append(TableRow(Values, SourceLocation(FilePath, SheetName, SourceRow)))

    return TableSlice(TableName, False, Fields, Rows, Location), Issues


def ParseSingleSlice(
    FilePath: Path,
    SheetName: str,
    TableName: str,
    Matrix: list[list[Any]],
    EscapeCharacter: str,
) -> tuple[TableSlice | None, list[ValidationIssue]]:
    Location = SourceLocation(FilePath, SheetName)
    Issues: list[ValidationIssue] = []
    if len(Matrix) < 5:
        Issues.append(ValidationIssue(SeverityLevel.Error, "单例表缺少语义定义区", Location))
        return None, Issues

    SemanticColumns: dict[str, int] = {}
    DescriptionColumn = -1
    MaxColumn = max(len(Row) for Row in Matrix[:5])
    for ColumnIndex in range(2, MaxColumn + 1):
        SemanticName = CellText(Matrix, 4, ColumnIndex).lower()
        if SemanticName in {"id", "type", "data", "cs"}:
            SemanticColumns[SemanticName] = ColumnIndex
        elif not SemanticName and DescriptionColumn < 0 and CellText(Matrix, 2, ColumnIndex):
            DescriptionColumn = ColumnIndex

    Missing = [Name for Name in ("id", "type", "data", "cs") if Name not in SemanticColumns]
    if Missing:
        Issues.append(
            ValidationIssue(
                SeverityLevel.Error,
                "单例表缺少语义列：" + "、".join(Missing),
                Location,
            )
        )
        return None, Issues

    Fields: list[FieldDefinition] = []
    Rows: list[TableRow] = []
    SeenNames: set[str] = set()
    for RowOffset in range(5, len(Matrix)):
        SourceRow = RowOffset + 1
        RowLabel = CellText(Matrix, SourceRow, 1)
        if RowLabel.startswith("##") or IsEmptyRow(Matrix[RowOffset]):
            continue

        Name = CellText(Matrix, SourceRow, SemanticColumns["id"])
        if not Name or Name.startswith("##"):
            continue
        if Name in SeenNames:
            Issues.append(
                ValidationIssue(
                    SeverityLevel.Error,
                    "单例表字段重复",
                    SourceLocation(FilePath, SheetName, SourceRow, SemanticColumns["id"]),
                    Name,
                )
            )
            continue

        TypeText = CellText(Matrix, SourceRow, SemanticColumns["type"])
        try:
            ParsedType = ParseFieldType(TypeText)
        except TypeParseError as Error:
            Issues.append(
                ValidationIssue(
                    SeverityLevel.Error,
                    str(Error),
                    SourceLocation(FilePath, SheetName, SourceRow, SemanticColumns["type"]),
                    Name,
                )
            )
            continue

        Scope = NormalizeScope(
            CellText(Matrix, SourceRow, SemanticColumns["cs"]),
            FilePath,
            SheetName,
            SemanticColumns["cs"],
            Name,
            Issues,
        )
        RawValue = GetCell(Matrix, SourceRow, SemanticColumns["data"])
        try:
            Value = ParseValue(RawValue, ParsedType, EscapeCharacter)
        except TypeParseError as Error:
            Issues.append(
                ValidationIssue(
                    SeverityLevel.Error,
                    str(Error),
                    SourceLocation(FilePath, SheetName, SourceRow, SemanticColumns["data"]),
                    Name,
                )
            )
            continue

        Description = ""
        if DescriptionColumn > 0:
            Description = CellText(Matrix, SourceRow, DescriptionColumn)
        Fields.append(
            FieldDefinition(
                Name,
                Description,
                TypeText,
                Scope,
                "",
                SemanticColumns["id"],
                ParsedType,
            )
        )
        Rows.append(
            TableRow(
                {Name: Value},
                SourceLocation(FilePath, SheetName, SourceRow, SemanticColumns["data"]),
            )
        )
        SeenNames.add(Name)

    if not Fields:
        Issues.append(ValidationIssue(SeverityLevel.Error, "单例表没有有效字段", Location))
        return None, Issues
    return TableSlice(TableName, True, Fields, Rows, Location), Issues


def NormalizeScope(
    RawScope: str,
    FilePath: Path,
    SheetName: str,
    ColumnIndex: int,
    FieldName: str,
    Issues: list[ValidationIssue],
) -> str:
    Scope = RawScope.strip().lower()
    if Scope not in {"", "c", "s", "cs"}:
        Issues.append(
            ValidationIssue(
                SeverityLevel.Error,
                f"客户端服务器标记无效：{RawScope}",
                SourceLocation(FilePath, SheetName, 5, ColumnIndex),
                FieldName,
            )
        )
        return "cs"
    return Scope or "cs"


def MergeTableSlice(
    Slice: TableSlice,
    LogicalTables: dict[str, LogicalTable],
    Issues: list[ValidationIssue],
) -> None:
    Existing = LogicalTables.get(Slice.Name)
    NewSignature = [(Field.Name, Field.TypeText, Field.Scope) for Field in Slice.Fields]
    if Existing is None:
        LogicalTables[Slice.Name] = LogicalTable(
            Slice.Name,
            Slice.IsSingle,
            list(Slice.Fields),
            list(Slice.Rows),
            {Slice.Location.FilePath},
        )
        return

    ExistingSignature = [(Field.Name, Field.TypeText, Field.Scope) for Field in Existing.Fields]
    if Existing.IsSingle != Slice.IsSingle or ExistingSignature != NewSignature:
        Issues.append(
            ValidationIssue(
                SeverityLevel.Error,
                "同名逻辑表 schema 不一致，禁止合并",
                Slice.Location,
                Slice.Name,
            )
        )
        return

    Existing.Rows.extend(Slice.Rows)
    Existing.SourceFiles.add(Slice.Location.FilePath)


def BuildSheetPreview(Slice: TableSlice, Matrix: list[list[Any]]) -> SheetPreview:
    Headers = [Field.Name for Field in Slice.Fields]
    PreviewRows: list[list[str]] = []
    for Row in Slice.Rows[:200]:
        PreviewRows.append([FormatPreviewValue(Row.Values.get(Field.Name)) for Field in Slice.Fields])
    return SheetPreview(
        Slice.Location.SheetName,
        Slice.Name,
        Slice.IsSingle,
        Headers,
        PreviewRows,
        list(Slice.Fields),
    )


def FormatPreviewValue(Value: Any) -> str:
    if isinstance(Value, bool):
        return "true" if Value else "false"
    if isinstance(Value, list):
        return str(Value).replace("'", "")
    if isinstance(Value, dict):
        return ", ".join(f"{Key}={Item}" for Key, Item in Value.items())
    return "" if Value is None else str(Value)


def CellText(Matrix: list[list[Any]], Row: int, Column: int) -> str:
    Value = GetCell(Matrix, Row, Column)
    return "" if Value is None else str(Value).strip()


def GetCell(Matrix: list[list[Any]], Row: int, Column: int) -> Any:
    if Row < 1 or Column < 1 or Row > len(Matrix):
        return None
    MatrixRow = Matrix[Row - 1]
    return MatrixRow[Column - 1] if Column <= len(MatrixRow) else None


def IsEmptyRow(Row: list[Any]) -> bool:
    return all(Value is None or str(Value).strip() == "" for Value in Row)
