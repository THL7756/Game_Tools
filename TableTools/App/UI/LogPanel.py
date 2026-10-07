# 用途：展示持久化日志，并提供筛选、复制、清空和打开日志文件操作。
# 最近修改日期：2026-10-07
# 作者：Codex

from pathlib import Path

from PySide6.QtCore import Qt
from PySide6.QtGui import QGuiApplication
from PySide6.QtWidgets import (
    QComboBox,
    QHBoxLayout,
    QLabel,
    QListWidget,
    QListWidgetItem,
    QMessageBox,
    QPushButton,
    QToolButton,
    QVBoxLayout,
    QWidget,
)

from App.Services.IconService import IconService
from App.Services.LanguageService import LanguageService
from App.Services.LogService import LogService


class LogPanel(QWidget):
    def __init__(self, Language: LanguageService, Icons: IconService, Logs: LogService, Parent=None) -> None:
        super().__init__(Parent)
        self.Language = Language
        self.Icons = Icons
        self.Logs = Logs
        self.IsCollapsed = False
        self.setObjectName("LogPanel")
        self.setMinimumHeight(180)

        Root = QVBoxLayout(self)
        Root.setContentsMargins(12, 10, 12, 10)
        Root.setSpacing(8)
        Header = QHBoxLayout()
        self.TitleLabel = QLabel()
        self.TitleLabel.setObjectName("PanelTitle")
        Header.addWidget(self.TitleLabel)
        Header.addStretch(1)
        self.Filter = QComboBox()
        self._BuildFilterOptions()
        self.Filter.currentTextChanged.connect(self._Refresh)
        Header.addWidget(self.Filter)
        self.CopyButton = self._IconButton("Copy", "CopyLogTooltip", self._Copy)
        self.ClearButton = self._IconButton("Trash", "ClearLogTooltip", self._Clear)
        self.OpenButton = self._IconButton("ExternalFile", "OpenLogTooltip", self._Open)
        self.CollapseButton = self._IconButton("ChevronDown", "CollapseLogTooltip", self._Toggle)
        Header.addWidget(self.CopyButton)
        Header.addWidget(self.ClearButton)
        Header.addWidget(self.OpenButton)
        Header.addWidget(self.CollapseButton)
        Root.addLayout(Header)

        self.List = QListWidget()
        self.List.setObjectName("LogList")
        self.List.setSelectionMode(QListWidget.SelectionMode.ExtendedSelection)
        Root.addWidget(self.List, 1)
        self.EmptyLabel = QLabel()
        self.EmptyLabel.setObjectName("EmptyState")
        self.EmptyLabel.setAlignment(Qt.AlignmentFlag.AlignCenter)
        Root.addWidget(self.EmptyLabel)
        self.Logs.EntryAdded.connect(self._OnEntry)
        self.UpdateTexts()
        self._Refresh()

    def _IconButton(self, IconName: str, TooltipKey: str, Slot) -> QToolButton:
        Button = QToolButton()
        Button.setIcon(self.Icons.Get(IconName))
        Button.setIconSize(self.Icons.Pixmap(IconName, 18).size())
        Button.setFixedSize(30, 30)
        Button.clicked.connect(Slot)
        Button.setProperty("IconButton", True)
        Button.setToolTip(self.Language.Get(TooltipKey))
        return Button

    def UpdateTexts(self) -> None:
        self.TitleLabel.setText(self.Language.Get("Log"))
        CurrentLevel = self.Filter.currentData() or "ALL"
        self._BuildFilterOptions(CurrentLevel)
        self.EmptyLabel.setText(self.Language.Get("NoLogEntries"))
        self.CopyButton.setToolTip(self.Language.Get("CopyLogTooltip"))
        self.ClearButton.setToolTip(self.Language.Get("ClearLogTooltip"))
        self.OpenButton.setToolTip(self.Language.Get("OpenLogTooltip"))
        self.CollapseButton.setToolTip(
            self.Language.Get("ExpandLogTooltip" if self.IsCollapsed else "CollapseLogTooltip")
        )

    def _OnEntry(self, Entry: dict) -> None:
        self._Refresh()

    def _Refresh(self) -> None:
        Level = self.Filter.currentData() or "ALL"
        self.List.clear()
        for Entry in self.Logs.Read(500):
            if Level != "ALL" and str(Entry.get("Level", "INFO")) != Level:
                continue
            Timestamp = str(Entry.get("Timestamp", ""))[11:19]
            Message = self.Language.LocalizeMessage(str(Entry.get("Message", "")))
            Text = f"{Timestamp}  {Entry.get('Level', 'INFO'):<7}  {Message}"
            Item = QListWidgetItem(Text)
            Item.setData(Qt.ItemDataRole.UserRole, Entry)
            self.List.addItem(Item)
        self.List.scrollToBottom()
        self.EmptyLabel.setVisible(self.List.count() == 0)
        self.List.setVisible(self.List.count() > 0)

    def _BuildFilterOptions(self, CurrentLevel: str = "ALL") -> None:
        self.Filter.blockSignals(True)
        self.Filter.clear()
        Options = (
            ("ALL", "LogAll"),
            ("INFO", "LogInfo"),
            ("WARNING", "LogWarning"),
            ("ERROR", "LogError"),
        )
        for Value, Key in Options:
            self.Filter.addItem(self.Language.Get(Key), Value)
        Index = max(0, self.Filter.findData(CurrentLevel))
        self.Filter.setCurrentIndex(Index)
        self.Filter.blockSignals(False)

    def _Copy(self) -> None:
        SelectedItems = self.List.selectedItems()
        Items = SelectedItems if SelectedItems else [self.List.item(Index) for Index in range(self.List.count())]
        Lines = [Item.text() for Item in Items]
        QGuiApplication.clipboard().setText("\n".join(Lines))

    def _Clear(self) -> None:
        Answer = QMessageBox.question(self, self.Language.Get("ClearLog"), self.Language.Get("ClearLogConfirm"))
        if Answer == QMessageBox.StandardButton.Yes:
            self.Logs.Clear()
            self._Refresh()

    def _Open(self) -> None:
        from PySide6.QtCore import QUrl
        from PySide6.QtGui import QDesktopServices

        self.Logs.LogPath.parent.mkdir(parents=True, exist_ok=True)
        QDesktopServices.openUrl(QUrl.fromLocalFile(str(self.Logs.LogPath)))

    def _Toggle(self) -> None:
        self.IsCollapsed = not self.IsCollapsed
        self.List.setVisible(not self.IsCollapsed)
        self.EmptyLabel.setVisible(not self.IsCollapsed and self.List.count() == 0)
        if self.IsCollapsed:
            self.setMinimumHeight(44)
            self.setMaximumHeight(44)
            self.CollapseButton.setToolTip(self.Language.Get("ExpandLogTooltip"))
        else:
            self.setMinimumHeight(180)
            self.setMaximumHeight(16777215)
            self.CollapseButton.setToolTip(self.Language.Get("CollapseLogTooltip"))
        self.CollapseButton.setIcon(self.Icons.Get("ChevronUp" if self.IsCollapsed else "ChevronDown"))
