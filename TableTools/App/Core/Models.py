from dataclasses import dataclass, field
from enum import Enum
from pathlib import Path
from typing import Any


class SeverityLevel(Enum):
    Error = "Error"
    Warning = "Warning"
    Info = "Info"


@dataclass(frozen=True)
class SourceLocation:
    FilePath: Path
    SheetName: str
    Row: int = 0
    Column: int = 0


@dataclass
class ValidationIssue:
    Severity: SeverityLevel
    Message: str
    Location: SourceLocation
    FieldName: str = ""

    def Format(self) -> str:
        Position = f"{self.Location.FilePath.name} · {self.Location.SheetName}"
        if self.Location.Row > 0:
            Position += f" · 第{self.Location.Row}行"
        if self.FieldName:
            Position += f" · {self.FieldName} 字段"
        return f"{Position} · 问题：{self.Message}"


@dataclass(frozen=True)
class ParsedType:
    RawText: str
    BaseName: str
    Dimensions: int = 0
    EnumValues: tuple[str, ...] = ()


@dataclass
class FieldDefinition:
    Name: str
    Description: str
    TypeText: str
    Scope: str
    DefaultText: str
    ColumnIndex: int
    ParsedType: ParsedType


@dataclass
class TableRow:
    Values: dict[str, Any]
    Location: SourceLocation


@dataclass
class TableSlice:
    Name: str
    IsSingle: bool
    Fields: list[FieldDefinition]
    Rows: list[TableRow]
    Location: SourceLocation


@dataclass
class LogicalTable:
    Name: str
    IsSingle: bool
    Fields: list[FieldDefinition]
    Rows: list[TableRow]
    SourceFiles: set[Path] = field(default_factory=set)

    @property
    def PrimaryFieldName(self) -> str:
        return self.Fields[0].Name if self.Fields else ""


@dataclass
class SheetPreview:
    SheetName: str
    TableName: str
    IsSingle: bool
    Headers: list[str]
    Rows: list[list[str]]
    Fields: list[FieldDefinition]


@dataclass
class SourceFileModel:
    Path: Path
    ModifiedTime: float
    Sheets: list[SheetPreview]
    LogicalTableNames: list[str]


@dataclass
class ParsedProject:
    SourceFiles: list[SourceFileModel]
    LogicalTables: dict[str, LogicalTable]
    Issues: list[ValidationIssue]


@dataclass
class ExportTargetState:
    ClientData: bool = True
    ClientCode: bool = True
    ServerData: bool = True
    ServerCode: bool = False


@dataclass
class ExportRequest:
    SelectedFiles: set[Path]
    Targets: ExportTargetState


@dataclass
class ExportResult:
    Success: bool
    WrittenFiles: list[Path] = field(default_factory=list)
    Messages: list[str] = field(default_factory=list)
