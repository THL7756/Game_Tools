# 用途：为表格预览提供只读模型、字段元信息和单元格问题状态。
# 最近修改日期：2026-10-07
# 作者：Codex

from PySide6.QtCore import QAbstractTableModel, QModelIndex, Qt
from PySide6.QtGui import QColor, QFont
from PySide6.QtWidgets import QApplication

from App.Core.Models import SeverityLevel, SheetPreview


class PreviewTableModel(QAbstractTableModel):
    def __init__(self, Parent=None) -> None:
        super().__init__(Parent)
        self.Preview: SheetPreview | None = None

    def SetPreview(self, Preview: SheetPreview | None) -> None:
        self.beginResetModel()
        self.Preview = Preview
        self.endResetModel()

    def rowCount(self, Parent: QModelIndex = QModelIndex()) -> int:
        if Parent.isValid() or self.Preview is None:
            return 0
        return len(self.Preview.Rows) + 1

    def columnCount(self, Parent: QModelIndex = QModelIndex()) -> int:
        if Parent.isValid() or self.Preview is None:
            return 0
        return len(self.Preview.Headers)

    def data(self, Index: QModelIndex, Role: int = Qt.ItemDataRole.DisplayRole):
        if not Index.isValid() or self.Preview is None:
            return None
        Row = Index.row()
        Column = Index.column()
        Application = QApplication.instance()
        IsLight = bool(Application and Application.palette().window().color().lightness() > 150)
        if Role == Qt.ItemDataRole.DisplayRole:
            if Row == 0:
                TypeLabel = self.Preview.TypeLabels[Column] if Column < len(self.Preview.TypeLabels) else ""
                ScopeLabel = self.Preview.ScopeLabels[Column] if Column < len(self.Preview.ScopeLabels) else ""
                return " · ".join(Item for Item in (TypeLabel, ScopeLabel) if Item)
            if Row - 1 >= len(self.Preview.Rows) or Column >= len(self.Preview.Rows[Row - 1]):
                return ""
            return self.Preview.Rows[Row - 1][Column]
        if Role == Qt.ItemDataRole.BackgroundRole:
            Issue = self.Preview.CellIssues.get((Row - 1, Column)) if Row > 0 else None
            if Issue is not None:
                Color = "#FFF4D8" if IsLight and Issue.Severity == SeverityLevel.Warning else "#FBE7EA" if IsLight else "#3A2920" if Issue.Severity == SeverityLevel.Warning else "#42252B"
                return QColor(Color)
            if Row > 0 and any(IssueRow == Row - 1 for IssueRow, _ in self.Preview.CellIssues):
                return QColor("#FFF9F9" if IsLight else "#2A1F23")
            if Row == 0:
                return QColor("#EDF1F5" if IsLight else "#272B32")
            if Row % 2 == 0:
                return QColor("#F6F8FA" if IsLight else "#1B1E23")
        if Role == Qt.ItemDataRole.ForegroundRole:
            Issue = self.Preview.CellIssues.get((Row - 1, Column)) if Row > 0 else None
            if Issue is not None:
                return QColor("#8A641F" if IsLight and Issue.Severity == SeverityLevel.Warning else "#A33A48" if IsLight else "#F5D98B" if Issue.Severity == SeverityLevel.Warning else "#FFB7BD")
            if Row == 0:
                return QColor("#687383" if IsLight else "#939DAD")
            return QColor("#20242A" if IsLight else "#E2E6ED")
        if Role == Qt.ItemDataRole.ToolTipRole and Row > 0:
            Issue = self.Preview.CellIssues.get((Row - 1, Column))
            RawValue = ""
            ParsedValue = ""
            if Row - 1 < len(self.Preview.Rows) and Column < len(self.Preview.Rows[Row - 1]):
                RawValue = self.Preview.Rows[Row - 1][Column]
            if Row - 1 < len(self.Preview.ParsedRows) and Column < len(self.Preview.ParsedRows[Row - 1]):
                ParsedValue = self.Preview.ParsedRows[Row - 1][Column]
            Status = "错误" if Issue is not None and self._IsChinese() else "Error" if Issue is not None else "有效" if self._IsChinese() else "Valid"
            Details = f"Raw: {RawValue} | Parsed: {ParsedValue} | Status: {Status}"
            if Issue is not None:
                Details += f" | {Issue.Format()}"
            return Details
        if Role == Qt.ItemDataRole.ToolTipRole and Row == 0 and Column < len(self.Preview.Fields):
            Field = self.Preview.Fields[Column]
            ColumnLabel = "源列" if self._IsChinese() else "Source column"
            return f"{Field.Description or '-'} | {ColumnLabel}: {Field.ColumnIndex}"
        if Role == Qt.ItemDataRole.FontRole:
            Font = QFont("Cascadia Mono", 10)
            if Row == 0:
                Font.setPointSize(9)
            return Font
        return None

    def headerData(self, Section: int, Orientation: Qt.Orientation, Role: int = Qt.ItemDataRole.DisplayRole):
        if self.Preview is None:
            return None
        if Orientation == Qt.Orientation.Horizontal:
            if Section >= len(self.Preview.Headers):
                return ""
            if Role == Qt.ItemDataRole.ToolTipRole and Section < len(self.Preview.Fields):
                Field = self.Preview.Fields[Section]
                Details = [Field.Name, Field.TypeText, Field.Scope, f"Column {Field.ColumnIndex}"]
                if Field.Description:
                    Details.insert(1, Field.Description)
                return "\n".join(Details)
            if Role != Qt.ItemDataRole.DisplayRole:
                return None
            return self.Preview.Headers[Section]
        if Role == Qt.ItemDataRole.ToolTipRole:
            if Section == 0:
                return "字段类型和作用域" if self._IsChinese() else "Field type and scope"
            RowIndex = Section - 1
            if RowIndex < len(self.Preview.RowNumbers):
                return (
                    f"源行 {self.Preview.RowNumbers[RowIndex]}"
                    if self._IsChinese()
                    else f"Source row {self.Preview.RowNumbers[RowIndex]}"
                )
            return None
        if Role != Qt.ItemDataRole.DisplayRole:
            return None
        if Section == 0:
            return "类型" if self._IsChinese() else "Type"
        RowIndex = Section - 1
        if RowIndex < len(self.Preview.RowNumbers):
            return str(self.Preview.RowNumbers[RowIndex])
        return str(RowIndex + 1)

    @staticmethod
    def _IsChinese() -> bool:
        Application = QApplication.instance()
        return bool(Application and Application.property("TableToolsLanguage") == "zh-CN")

    def flags(self, Index: QModelIndex) -> Qt.ItemFlag:
        if not Index.isValid():
            return Qt.ItemFlag.NoItemFlags
        return Qt.ItemFlag.ItemIsEnabled | Qt.ItemFlag.ItemIsSelectable
