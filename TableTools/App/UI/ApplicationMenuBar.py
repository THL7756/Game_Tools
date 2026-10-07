# 用途：构建 Figma 顶部组合栏、原生菜单和全局显示设置入口。
# 最近修改日期：2026-10-07
# 作者：Codex

from typing import Callable

from PySide6.QtCore import QSize, Qt, Signal
from PySide6.QtGui import QAction, QKeySequence
from PySide6.QtWidgets import (
    QButtonGroup,
    QComboBox,
    QHBoxLayout,
    QLabel,
    QMenuBar,
    QPushButton,
    QSizePolicy,
    QToolButton,
    QWidget,
)

from App.Services.IconService import IconService
from App.Services.LanguageService import LanguageService


class ApplicationMenuBar(QWidget):
    LogToggled = Signal(bool)
    SidebarToggled = Signal()
    LanguageChanged = Signal(str)
    ThemeChanged = Signal(str)
    SettingsRequested = Signal()

    def __init__(self, Language: LanguageService, Icons: IconService, Parent=None) -> None:
        super().__init__(Parent)
        self.Language = Language
        self.Icons = Icons
        self.Actions: dict[str, QAction] = {}
        self.ThemeButtons: dict[str, QToolButton] = {}
        self.ThemeGroup = QButtonGroup(self)
        self.setObjectName("ApplicationMenuBar")
        self.setFixedHeight(44)

        Root = QHBoxLayout(self)
        Root.setContentsMargins(8, 0, 12, 0)
        Root.setSpacing(8)

        self.SidebarButton = QToolButton()
        self.SidebarButton.setObjectName("SidebarToggle")
        self.SidebarButton.setCheckable(True)
        self.SidebarButton.setIcon(self.Icons.Get("Sidebar"))
        self.SidebarButton.setIconSize(QSize(18, 18))
        self.SidebarButton.setFixedSize(30, 30)
        self.SidebarButton.setToolTip(self.Language.Get("ToggleSidebar"))
        self.SidebarButton.clicked.connect(self.SidebarToggled.emit)
        Root.addWidget(self.SidebarButton)

        self.NativeMenuBar = QMenuBar()
        self.NativeMenuBar.setNativeMenuBar(False)
        self.NativeMenuBar.setSizePolicy(QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Preferred)
        Root.addWidget(self.NativeMenuBar, 1)

        self.PageModeLabel = QLabel()
        self.PageModeLabel.setObjectName("MenuSubtle")
        Root.addWidget(self.PageModeLabel)

        self.ThemeGroup.setExclusive(True)
        for Mode, IconName in (("Day", "Sun"), ("Night", "Moon"), ("System", "Monitor")):
            Button = QToolButton()
            Button.setObjectName("ThemeSegment")
            Button.setCheckable(True)
            Button.setIcon(self.Icons.Get(IconName))
            Button.setIconSize(QSize(16, 16))
            Button.setToolButtonStyle(Qt.ToolButtonStyle.ToolButtonTextBesideIcon)
            Button.setProperty("ThemeMode", Mode)
            Button.clicked.connect(lambda Checked=False, Value=Mode: self.ThemeChanged.emit(Value))
            self.ThemeGroup.addButton(Button)
            self.ThemeButtons[Mode] = Button
            Root.addWidget(Button)

        self.LanguageCombo = QComboBox()
        self.LanguageCombo.setObjectName("LanguageCombo")
        self.LanguageCombo.setFixedWidth(112)
        self.LanguageCombo.addItem(self.Icons.Get("SimplifiedChinese"), self.Language.Get("SimplifiedChinese"), "zh-CN")
        self.LanguageCombo.addItem(self.Icons.Get("English"), self.Language.Get("English"), "en-US")
        self.LanguageCombo.currentIndexChanged.connect(self._LanguageIndexChanged)
        Root.addWidget(self.LanguageCombo)

        self.SettingsButton = QPushButton()
        self.SettingsButton.setObjectName("MenuSettings")
        self.SettingsButton.setIcon(self.Icons.Get("Settings"))
        self.SettingsButton.setToolTip(self.Language.Get("Settings"))
        self.SettingsButton.setAccessibleName(self.Language.Get("Settings"))
        self.SettingsButton.clicked.connect(self.SettingsRequested.emit)
        Root.addWidget(self.SettingsButton)
        self.Build()

    def _Action(self, Key: str, Slot: Callable, Shortcut: str | None = None, Checkable: bool = False) -> QAction:
        Action = QAction(self.Language.Get(Key), self.NativeMenuBar)
        Action.setIcon(self.Icons.Get(Key))
        Action.setCheckable(Checkable)
        if Shortcut:
            Action.setShortcut(QKeySequence(Shortcut))
        Action.triggered.connect(Slot)
        self.Actions[Key] = Action
        return Action

    def Build(self) -> None:
        self.NativeMenuBar.clear()
        self.Actions.clear()
        ParentWindow = self.window()
        FileMenu = self.NativeMenuBar.addMenu(self.Language.Get("FileMenu"))
        FileMenu.addAction(self._Action("RefreshList", ParentWindow.RefreshProject, "F5"))
        FileMenu.addAction(self._Action("OpenSourceDirectory", ParentWindow._OpenSourceDirectory))
        FileMenu.addAction(self._Action("OpenInExcel", ParentWindow._OpenExcel))
        FileMenu.addSeparator()
        FileMenu.addAction(self._Action("Settings", ParentWindow._OpenSettings, "Ctrl+,"))
        FileMenu.addAction(self._Action("Exit", ParentWindow.close, "Alt+F4"))

        EditMenu = self.NativeMenuBar.addMenu(self.Language.Get("EditMenu"))
        EditMenu.addAction(self._Action("SelectAll", ParentWindow._SelectAllFiles, "Ctrl+A"))
        EditMenu.addAction(self._Action("ClearSelection", ParentWindow._ClearSelection))
        EditMenu.addAction(self._Action("ClearSearch", ParentWindow._ClearSearch, "Ctrl+L"))
        EditMenu.addSeparator()
        EditMenu.addAction(self._Action("FavoriteCurrent", ParentWindow._FavoriteCurrent))
        EditMenu.addAction(self._Action("ClearFavorites", ParentWindow._ClearFavorites))

        ViewMenu = self.NativeMenuBar.addMenu(self.Language.Get("ViewMenu"))
        ThemeMenu = ViewMenu.addMenu(self.Language.Get("ThemeMode"))
        for Mode in ("Day", "Night", "System"):
            ThemeMenu.addAction(self._Action(Mode, lambda Checked=False, Value=Mode: self.ThemeChanged.emit(Value), Checkable=True))
        LanguageMenu = ViewMenu.addMenu(self.Language.Get("Language"))
        LanguageMenu.addAction(self._Action("SimplifiedChinese", lambda Checked=False: self.LanguageChanged.emit("zh-CN"), Checkable=True))
        LanguageMenu.addAction(self._Action("English", lambda Checked=False: self.LanguageChanged.emit("en-US"), Checkable=True))
        ViewMenu.addAction(
            self._Action(
                "ToggleSidebar",
                lambda Checked=False: self.SidebarToggled.emit(),
                Checkable=True,
            )
        )
        ViewMenu.addAction(self._Action("ToggleLog", self._ToggleLog, Checkable=True))

        DocsMenu = self.NativeMenuBar.addMenu(self.Language.Get("DocsMenu"))
        DocsMenu.addAction(self._Action("Readme", lambda: ParentWindow._OpenDocument("README.md")))
        DocsMenu.addAction(self._Action("TableRules", lambda: ParentWindow._OpenDocument("表格规则.md")))
        DocsMenu.addAction(self._Action("TableContentRules", lambda: ParentWindow._OpenDocument("表格内容规则.md")))
        self._UpdateTopBarTexts()

    def _ToggleLog(self, Checked: bool) -> None:
        self.LogToggled.emit(Checked)

    def _LanguageIndexChanged(self, Index: int) -> None:
        Code = self.LanguageCombo.itemData(Index)
        if Code:
            self.LanguageChanged.emit(str(Code))

    def _UpdateTopBarTexts(self) -> None:
        self.PageModeLabel.setText(self.Language.Get("PageMode"))
        self.SettingsButton.setText(self.Language.Get("Settings"))
        self.SettingsButton.setToolTip(self.Language.Get("Settings"))
        self.SettingsButton.setAccessibleName(self.Language.Get("Settings"))
        self.SidebarButton.setToolTip(self.Language.Get("ToggleSidebar"))
        self.LanguageCombo.setItemText(0, self.Language.Get("SimplifiedChinese"))
        self.LanguageCombo.setItemText(1, self.Language.Get("English"))
        for Mode, Button in self.ThemeButtons.items():
            Button.setText(self.Language.Get(Mode))

    def UpdateTexts(self) -> None:
        CurrentTheme = next((Mode for Mode, Button in self.ThemeButtons.items() if Button.isChecked()), "Night")
        CurrentLanguage = self.LanguageCombo.currentData() or self.Language.CurrentCode
        self.Build()
        self.SetThemeMode(CurrentTheme)
        self.SetLanguageCode(str(CurrentLanguage))

    def SetContext(self, HasCurrentFile: bool, HasSelection: bool, HasSearch: bool = False) -> None:
        # 菜单栏加入中央布局后 parent() 会变成布局容器，window() 才始终指向主窗口。
        HostWindow = self.window()
        for Key in ("OpenInExcel", "FavoriteCurrent"):
            if Key in self.Actions:
                self.Actions[Key].setEnabled(HasCurrentFile)
        if "ClearSelection" in self.Actions:
            self.Actions["ClearSelection"].setEnabled(HasSelection)
        if "ClearSearch" in self.Actions:
            self.Actions["ClearSearch"].setEnabled(HasSearch)
        if "SelectAll" in self.Actions:
            Project = getattr(HostWindow, "Project", None)
            self.Actions["SelectAll"].setEnabled(bool(Project is not None and Project.SourceFiles))
        if "ClearFavorites" in self.Actions:
            TableList = getattr(HostWindow, "TableList", None)
            self.Actions["ClearFavorites"].setEnabled(bool(TableList is not None and TableList.GetFavorites()))

    def SetThemeMode(self, Mode: str) -> None:
        for Name in ("Day", "Night", "System"):
            if Name in self.ThemeButtons:
                self.ThemeButtons[Name].setChecked(Name == Mode)
            Action = self.Actions.get(Name)
            if Action is not None:
                Action.setChecked(Name == Mode)

    def SetLanguageCode(self, Code: str) -> None:
        self.LanguageCombo.blockSignals(True)
        Index = self.LanguageCombo.findData(Code)
        if Index >= 0:
            self.LanguageCombo.setCurrentIndex(Index)
        self.LanguageCombo.blockSignals(False)
        for Name, Value in (("SimplifiedChinese", "zh-CN"), ("English", "en-US")):
            Action = self.Actions.get(Name)
            if Action is not None:
                Action.setChecked(Value == Code)

    def SetSidebarVisible(self, Visible: bool) -> None:
        self.SidebarButton.setChecked(Visible)
        Action = self.Actions.get("ToggleSidebar")
        if Action is not None:
            Action.setChecked(Visible)
