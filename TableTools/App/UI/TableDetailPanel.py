# 用途：显示当前 Excel 文件、Sheet、字段元信息和完整只读预览。
# 最近修改日期：2026-10-07
# 作者：Codex

from datetime import datetime
from pathlib import Path

from PySide6.QtCore import QModelIndex, Signal
from PySide6.QtGui import QIcon
from PySide6.QtWidgets import (
    QHBoxLayout,
    QHeaderView,
    QLabel,
    QPushButton,
    QTabBar,
    QTableView,
    QVBoxLayout,
    QWidget,
)

from App.Core.Models import SheetPreview, SourceFileModel
from App.Services.IconService import IconService
from App.Services.LanguageService import LanguageService
from App.UI.PreviewTableModel import PreviewTableModel


class TableDetailPanel(QWidget):
    RefreshRequested = Signal()
    OpenExcelRequested = Signal()
    CurrentSheetChanged = Signal(str)

    def __init__(self, Language: LanguageService, IconsDirectory: Path) -> None:
        super().__init__()
        self.Language = Language
        self.IconsDirectory = IconsDirectory
        self.Icons = IconService(IconsDirectory)
        self.SourceFile: SourceFileModel | None = None
        self.CurrentSheetName = ""
        self.Model = PreviewTableModel(self)
        Root = QVBoxLayout(self)
        Root.setContentsMargins(12, 12, 12, 12)
        Root.setSpacing(10)

        Header = QWidget()
        Header.setObjectName("DetailHeader")
        HeaderLayout = QHBoxLayout(Header)
        HeaderLayout.setContentsMargins(12, 10, 12, 10)
        HeaderLayout.setSpacing(8)
        self.TableIcon = QLabel()
        self.TableIcon.setPixmap(self.Icons.Pixmap("Table", 22))
        HeaderLayout.addWidget(self.TableIcon)
        TitleColumn = QVBoxLayout()
        TitleColumn.setSpacing(2)
        self.TitleLabel = QLabel()
        self.TitleLabel.setObjectName("TableTitle")
        self.MetaLabel = QLabel()
        self.MetaLabel.setObjectName("Muted")
        self.PathLabel = QLabel()
        self.PathLabel.setObjectName("Subtle")
        self.LogicalTableLabel = QLabel()
        self.LogicalTableLabel.setObjectName("Subtle")
        TitleColumn.addWidget(self.TitleLabel)
        TitleColumn.addWidget(self.MetaLabel)
        TitleColumn.addWidget(self.PathLabel)
        TitleColumn.addWidget(self.LogicalTableLabel)
        HeaderLayout.addLayout(TitleColumn, 1)
        self.SyncLabel = QLabel()
        self.SyncLabel.setObjectName("Muted")
        HeaderLayout.addWidget(self.SyncLabel)
        self.RefreshButton = QPushButton()
        self.RefreshButton.setIcon(self.Icons.Get("Refresh"))
        self.RefreshButton.clicked.connect(self.RefreshRequested.emit)
        self.ExcelButton = QPushButton()
        self.ExcelButton.setIcon(self.Icons.Get("Excel"))
        self.ExcelButton.clicked.connect(self.OpenExcelRequested.emit)
        self.RefreshButton.setEnabled(False)
        self.ExcelButton.setEnabled(False)
        HeaderLayout.addWidget(self.RefreshButton)
        HeaderLayout.addWidget(self.ExcelButton)
        Root.addWidget(Header)

        self.SheetTabs = QTabBar()
        self.SheetTabs.setExpanding(False)
        self.SheetTabs.currentChanged.connect(self._ShowSheet)
        Root.addWidget(self.SheetTabs)

        self.Table = QTableView()
        self.Table.setModel(self.Model)
        self.Table.setEditTriggers(QTableView.EditTrigger.NoEditTriggers)
        self.Table.setSelectionBehavior(QTableView.SelectionBehavior.SelectRows)
        self.Table.setSelectionMode(QTableView.SelectionMode.SingleSelection)
        self.Table.setAlternatingRowColors(True)
        self.Table.verticalHeader().setVisible(False)
        self.Table.verticalHeader().setDefaultSectionSize(28)
        self.Table.horizontalHeader().setSectionResizeMode(QHeaderView.ResizeMode.Interactive)
        self.Table.horizontalHeader().setStretchLastSection(True)
        self.Table.setWordWrap(False)
        self.Table.clicked.connect(self._ShowCellDetails)
        Root.addWidget(self.Table, 1)
        self.PreviewLabel = QLabel()
        self.PreviewLabel.setObjectName("Muted")
        Root.addWidget(self.PreviewLabel)
        self.CellDetailsLabel = QLabel()
        self.CellDetailsLabel.setObjectName("PreviewDetails")
        self.CellDetailsLabel.setWordWrap(True)
        self.CellDetailsLabel.setMinimumHeight(32)
        Root.addWidget(self.CellDetailsLabel)
        self.UpdateTexts()

    def UpdateTexts(self) -> None:
        self.RefreshButton.setText(self.Language.Get("RefreshContent"))
        self.RefreshButton.setToolTip(self.Language.Get("RefreshTooltip"))
        self.ExcelButton.setText(self.Language.Get("OpenInExcel"))
        self.ExcelButton.setToolTip(self.Language.Get("ExcelTooltip"))
        self.PreviewLabel.setText(self.Language.Get("ReadOnlyPreview"))
        if self.SourceFile is not None:
            self._UpdateHeader(self.SourceFile)
        if not self.CellDetailsLabel.text():
            self.CellDetailsLabel.setText(self.Language.Get("PreviewDetailsEmpty"))

    def SetFile(self, SourceFile: SourceFileModel) -> None:
        PreviousSheet = self.CurrentSheetName
        self.SourceFile = SourceFile
        self.RefreshButton.setEnabled(True)
        self.ExcelButton.setEnabled(True)
        self._UpdateHeader(SourceFile)
        self.SheetTabs.blockSignals(True)
        while self.SheetTabs.count() > 0:
            self.SheetTabs.removeTab(self.SheetTabs.count() - 1)
        for Sheet in SourceFile.Sheets:
            self.SheetTabs.addTab(Sheet.SheetName)
        TargetIndex = 0
        for Index in range(self.SheetTabs.count()):
            if self.SheetTabs.tabText(Index) == PreviousSheet:
                TargetIndex = Index
                break
        self.SheetTabs.blockSignals(False)
        if SourceFile.Sheets:
            self.SheetTabs.setCurrentIndex(TargetIndex)
            self._ShowSheet(TargetIndex)
        else:
            self.CurrentSheetName = ""
            self.Model.SetPreview(None)
            self.PreviewLabel.setText(self.Language.Get("NoPreview"))
            self.CellDetailsLabel.setText(self.Language.Get("PreviewDetailsEmpty"))

    def ClearFile(self) -> None:
        self.SourceFile = None
        self.CurrentSheetName = ""
        self.TitleLabel.clear()
        self.MetaLabel.clear()
        self.PathLabel.clear()
        self.LogicalTableLabel.clear()
        self.SyncLabel.clear()
        self.RefreshButton.setEnabled(False)
        self.ExcelButton.setEnabled(False)
        self.SheetTabs.blockSignals(True)
        while self.SheetTabs.count() > 0:
            self.SheetTabs.removeTab(self.SheetTabs.count() - 1)
        self.SheetTabs.blockSignals(False)
        self.Model.SetPreview(None)
        self.PreviewLabel.setText(self.Language.Get("NoPreview"))
        self.CellDetailsLabel.setText(self.Language.Get("PreviewDetailsEmpty"))

    def _UpdateHeader(self, SourceFile: SourceFileModel) -> None:
        Modified = datetime.fromtimestamp(SourceFile.ModifiedTime).strftime("%H:%M")
        self.TitleLabel.setText(SourceFile.Path.stem)
        self.MetaLabel.setText(self.Language.Get("Modified", Time=Modified))
        self.PathLabel.setText(str(SourceFile.Path))
        self.LogicalTableLabel.setText("、".join(SourceFile.LogicalTableNames))
        self.SyncLabel.setText(self.Language.Get("Synced", Time=datetime.now().strftime("%H:%M")))

    def _ShowSheet(self, Index: int) -> None:
        if self.SourceFile is None or Index < 0 or Index >= len(self.SourceFile.Sheets):
            return
        Sheet = self.SourceFile.Sheets[Index]
        self.CurrentSheetName = Sheet.SheetName
        self.PopulateSheet(Sheet)
        self.CurrentSheetChanged.emit(Sheet.SheetName)

    def PopulateSheet(self, Sheet: SheetPreview) -> None:
        self.Model.SetPreview(Sheet)
        self.Table.resizeColumnsToContents()
        for Column in range(self.Model.columnCount()):
            Width = self.Table.columnWidth(Column)
            self.Table.setColumnWidth(Column, min(max(Width, 100), 360))
        if not Sheet.Rows:
            self.PreviewLabel.setText(self.Language.Get("NoPreview"))
            self.CellDetailsLabel.setText(self.Language.Get("PreviewDetailsEmpty"))
        else:
            self.PreviewLabel.setText(
                self.Language.Get("PreviewSummary", Rows=len(Sheet.Rows), Columns=len(Sheet.Headers))
            )
        if self.Model.rowCount() > 0 and self.Model.columnCount() > 0:
            self._ShowCellDetails(self.Model.index(1 if self.Model.rowCount() > 1 else 0, 0))

    def _ShowCellDetails(self, Index: QModelIndex) -> None:
        if self.SourceFile is None or not Index.isValid() or self.SheetTabs.currentIndex() < 0:
            return
        Sheet = self.SourceFile.Sheets[self.SheetTabs.currentIndex()]
        Column = Index.column()
        Row = Index.row()
        if Sheet.IsSingle:
            if Row <= 0 or Row - 1 >= len(Sheet.Rows) or Column >= len(Sheet.Headers):
                self.CellDetailsLabel.setText(self.Language.Get("PreviewDetailsEmpty"))
                return
            SourceRow = Sheet.RowNumbers[Row - 1] if Row - 1 < len(Sheet.RowNumbers) else Row
            RawValue = Sheet.Rows[Row - 1][Column] if Column < len(Sheet.Rows[Row - 1]) else ""
            ParsedValue = Sheet.ParsedRows[Row - 1][Column] if Row - 1 < len(Sheet.ParsedRows) and Column < len(Sheet.ParsedRows[Row - 1]) else ""
            FieldName = Sheet.Rows[Row - 1][0] if Sheet.Rows[Row - 1] else ""
            TypeText = Sheet.Rows[Row - 1][1] if len(Sheet.Rows[Row - 1]) > 1 else ""
            Description = ""
            Scope = ""
            SourceColumn = ""
        else:
            if Column >= len(Sheet.Fields):
                return
            Field = Sheet.Fields[Column]
            FieldName = Field.Name
            TypeText = Field.TypeText
            Description = Field.Description
            Scope = Field.Scope
            SourceColumn = str(Field.ColumnIndex)
            SourceRow = Sheet.RowNumbers[Row - 1] if Row > 0 and Row - 1 < len(Sheet.RowNumbers) else 0
            RawValue = Sheet.Rows[Row - 1][Column] if Row > 0 and Row - 1 < len(Sheet.Rows) and Column < len(Sheet.Rows[Row - 1]) else ""
            ParsedValue = Sheet.ParsedRows[Row - 1][Column] if Row > 0 and Row - 1 < len(Sheet.ParsedRows) and Column < len(Sheet.ParsedRows[Row - 1]) else ""
        Issue = Sheet.CellIssues.get((Row - 1, Column)) if Row > 0 else None
        StatusKey = "PreviewStatusError" if Issue is not None else "PreviewStatusValid"
        self.CellDetailsLabel.setText(
            self.Language.Get(
                "PreviewDetails",
                Field=FieldName,
                Type=TypeText,
                Description=Description or self.Language.Get("PreviewDetailsNone"),
                Scope=Scope or self.Language.Get("PreviewDetailsNone"),
                SourceRow=SourceRow or self.Language.Get("PreviewDetailsNone"),
                SourceColumn=SourceColumn or self.Language.Get("PreviewDetailsNone"),
                Raw=RawValue,
                Parsed=ParsedValue or self.Language.Get("PreviewDetailsNone"),
                Status=self.Language.Get(StatusKey),
            )
        )
