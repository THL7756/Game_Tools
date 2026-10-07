from datetime import datetime
from pathlib import Path

from PySide6.QtCore import Signal
from PySide6.QtGui import QIcon
from PySide6.QtWidgets import (
    QHBoxLayout,
    QLabel,
    QPushButton,
    QTabBar,
    QTableWidget,
    QTableWidgetItem,
    QVBoxLayout,
    QWidget,
)

from App.Core.Models import SheetPreview, SourceFileModel
from App.Services.LanguageService import LanguageService


class TableDetailPanel(QWidget):
    RefreshRequested = Signal()
    OpenExcelRequested = Signal()

    def __init__(self, Language: LanguageService, IconsDirectory: Path) -> None:
        super().__init__()
        self.Language = Language
        self.IconsDirectory = IconsDirectory
        self.SourceFile: SourceFileModel | None = None
        Root = QVBoxLayout(self)
        Root.setContentsMargins(12, 12, 12, 12)
        Root.setSpacing(10)

        Header = QWidget()
        Header.setObjectName("DetailHeader")
        HeaderLayout = QHBoxLayout(Header)
        HeaderLayout.setContentsMargins(12, 10, 12, 10)
        HeaderLayout.setSpacing(8)
        self.TableIcon = QLabel()
        self.TableIcon.setPixmap(QIcon(str(IconsDirectory / "Table.svg")).pixmap(22, 22))
        HeaderLayout.addWidget(self.TableIcon)
        TitleColumn = QVBoxLayout()
        TitleColumn.setSpacing(2)
        self.TitleLabel = QLabel()
        self.TitleLabel.setObjectName("TableTitle")
        self.MetaLabel = QLabel()
        self.MetaLabel.setObjectName("Muted")
        self.PathLabel = QLabel()
        self.PathLabel.setObjectName("Subtle")
        TitleColumn.addWidget(self.TitleLabel)
        TitleColumn.addWidget(self.MetaLabel)
        TitleColumn.addWidget(self.PathLabel)
        HeaderLayout.addLayout(TitleColumn, 1)
        self.SyncLabel = QLabel()
        self.SyncLabel.setObjectName("Muted")
        HeaderLayout.addWidget(self.SyncLabel)
        self.RefreshButton = QPushButton()
        self.RefreshButton.setIcon(QIcon(str(IconsDirectory / "ExternalLinkAlt.svg")))
        self.RefreshButton.clicked.connect(self.RefreshRequested.emit)
        self.ExcelButton = QPushButton()
        self.ExcelButton.setIcon(QIcon(str(IconsDirectory / "ExternalLinkExcel.svg")))
        self.ExcelButton.clicked.connect(self.OpenExcelRequested.emit)
        HeaderLayout.addWidget(self.RefreshButton)
        HeaderLayout.addWidget(self.ExcelButton)
        Root.addWidget(Header)

        self.SheetTabs = QTabBar()
        self.SheetTabs.setExpanding(False)
        self.SheetTabs.currentChanged.connect(self._ShowSheet)
        Root.addWidget(self.SheetTabs)

        self.Table = QTableWidget()
        self.Table.setAlternatingRowColors(True)
        self.Table.setEditTriggers(QTableWidget.EditTrigger.NoEditTriggers)
        self.Table.setSelectionBehavior(QTableWidget.SelectionBehavior.SelectRows)
        self.Table.verticalHeader().setVisible(False)
        self.Table.horizontalHeader().setStretchLastSection(True)
        self.Table.setWordWrap(False)
        Root.addWidget(self.Table, 1)
        self.PreviewLabel = QLabel()
        self.PreviewLabel.setObjectName("Muted")
        Root.addWidget(self.PreviewLabel)
        self.UpdateTexts()

    def UpdateTexts(self) -> None:
        self.RefreshButton.setText(self.Language.Get("Refresh"))
        self.RefreshButton.setToolTip(self.Language.Get("RefreshTooltip"))
        self.ExcelButton.setText(self.Language.Get("OpenInExcel"))
        self.ExcelButton.setToolTip(self.Language.Get("ExcelTooltip"))
        self.PreviewLabel.setText(self.Language.Get("ReadOnlyPreview"))
        if self.SourceFile is not None:
            self._UpdateHeader(self.SourceFile)

    def SetFile(self, SourceFile: SourceFileModel) -> None:
        self.SourceFile = SourceFile
        self._UpdateHeader(SourceFile)
        self.SheetTabs.blockSignals(True)
        while self.SheetTabs.count() > 0:
            self.SheetTabs.removeTab(0)
        for Sheet in SourceFile.Sheets:
            self.SheetTabs.addTab(Sheet.SheetName)
        self.SheetTabs.blockSignals(False)
        if SourceFile.Sheets:
            self.SheetTabs.setCurrentIndex(0)
            self._ShowSheet(0)
        else:
            self.Table.clear()
            self.Table.setRowCount(0)
            self.Table.setColumnCount(0)

    def _UpdateHeader(self, SourceFile: SourceFileModel) -> None:
        self.TitleLabel.setText(SourceFile.Path.stem)
        Modified = datetime.fromtimestamp(SourceFile.ModifiedTime).strftime("%H:%M")
        self.MetaLabel.setText(self.Language.Get("Modified", Time=Modified))
        self.PathLabel.setText(str(SourceFile.Path))
        self.SyncLabel.setText(self.Language.Get("Synced", Time=datetime.now().strftime("%H:%M")))

    def _ShowSheet(self, Index: int) -> None:
        if self.SourceFile is None or Index < 0 or Index >= len(self.SourceFile.Sheets):
            return
        self.PopulateSheet(self.SourceFile.Sheets[Index])

    def PopulateSheet(self, Sheet: SheetPreview) -> None:
        self.Table.clear()
        self.Table.setColumnCount(len(Sheet.Headers))
        self.Table.setHorizontalHeaderLabels(Sheet.Headers)
        self.Table.setRowCount(len(Sheet.Rows))
        for RowIndex, Row in enumerate(Sheet.Rows):
            for ColumnIndex, Value in enumerate(Row):
                Item = QTableWidgetItem(Value)
                self.Table.setItem(RowIndex, ColumnIndex, Item)
        self.Table.resizeColumnsToContents()
        if self.Table.columnCount() > 0:
            self.Table.horizontalHeader().setStretchLastSection(True)
