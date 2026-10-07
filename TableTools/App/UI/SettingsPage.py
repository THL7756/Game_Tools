# 用途：提供与主工作区同窗口显示的设置页面。
# 最近修改日期：2026-10-07
# 作者：Codex

from copy import deepcopy
from pathlib import Path

from PySide6.QtCore import Signal
from PySide6.QtWidgets import (
    QButtonGroup,
    QComboBox,
    QFormLayout,
    QGroupBox,
    QHBoxLayout,
    QLabel,
    QLineEdit,
    QPushButton,
    QRadioButton,
    QTabWidget,
    QVBoxLayout,
    QWidget,
    QFileDialog,
)

from App.Services.LanguageService import LanguageService
from App.Services.IconService import IconService
from App.Services.SettingsService import DefaultSettings


class SettingsPage(QWidget):
    BackRequested = Signal()
    SaveRequested = Signal(object)
    ThemeChanged = Signal(str)
    AccentChanged = Signal(str)

    AccentColors = ("#4D8DF7", "#46B5A5", "#D28A3D", "#B56DE6")

    def __init__(self, Settings: dict, Language: LanguageService, Icons: IconService | None = None) -> None:
        super().__init__()
        self.Settings = deepcopy(Settings)
        self.Language = Language
        self.Icons = Icons
        Root = QVBoxLayout(self)
        Root.setContentsMargins(20, 16, 20, 20)
        Root.setSpacing(14)

        Header = QHBoxLayout()
        self.BackButton = QPushButton()
        self.BackButton.setObjectName("Secondary")
        if self.Icons is not None:
            self.BackButton.setIcon(self.Icons.Get("Back"))
        self.BackButton.setToolTip(self.Language.Get("BackToTool"))
        self.BackButton.clicked.connect(self.BackRequested.emit)
        Header.addWidget(self.BackButton)
        self.TitleLabel = QLabel()
        self.TitleLabel.setObjectName("Title")
        Header.addWidget(self.TitleLabel)
        Header.addStretch(1)
        Root.addLayout(Header)

        self.SettingsTabs = QTabWidget()
        self.SettingsTabs.setObjectName("SettingsTabs")
        self.SettingsTabs.setDocumentMode(True)

        Appearance = QGroupBox()
        self.AppearanceGroup = Appearance
        AppearanceLayout = QFormLayout(Appearance)
        self.ThemeGroup = QButtonGroup(self)
        self.ThemeButtons: dict[str, QRadioButton] = {}
        ThemeRow = QHBoxLayout()
        for Mode in ("Day", "Night", "System"):
            Button = QRadioButton()
            self.ThemeButtons[Mode] = Button
            self.ThemeGroup.addButton(Button)
            Button.clicked.connect(lambda Checked=False, Value=Mode: self._SetTheme(Value))
            ThemeRow.addWidget(Button)
            if self.Settings["Interface"].get("ThemeMode") == Mode:
                Button.setChecked(True)
        self.ThemeLabel = QLabel()
        AppearanceLayout.addRow(self.ThemeLabel, ThemeRow)
        AccentRow = QHBoxLayout()
        self.AccentGroup = QButtonGroup(self)
        self.AccentButtons: dict[str, QPushButton] = {}
        CurrentAccent = self.Settings["Interface"].get("AccentColor", self.AccentColors[0])
        for Color in self.AccentColors:
            Button = QPushButton()
            Button.setCheckable(True)
            Button.setFixedSize(34, 28)
            Button.setToolTip(Color)
            Button.setStyleSheet(f"background:{Color}; border: 2px solid transparent; border-radius: 5px;")
            Button.clicked.connect(lambda Checked, Value=Color: self._SetAccent(Value))
            self.AccentGroup.addButton(Button)
            self.AccentButtons[Color] = Button
            AccentRow.addWidget(Button)
            if Color == CurrentAccent:
                Button.setChecked(True)
        self._RefreshAccentButtons(CurrentAccent)
        AccentRow.addStretch(1)
        self.AccentLabel = QLabel()
        AppearanceLayout.addRow(self.AccentLabel, AccentRow)
        self.LanguageGroup = QButtonGroup(self)
        self.LanguageButtons: dict[str, QRadioButton] = {}
        LanguageRow = QHBoxLayout()
        for Code, Label in (("zh-CN", "简体中文"), ("en-US", "English")):
            Button = QRadioButton(Label)
            self.LanguageButtons[Code] = Button
            self.LanguageGroup.addButton(Button)
            LanguageRow.addWidget(Button)
            if self.Settings["Interface"].get("Language", "zh-CN") == Code:
                Button.setChecked(True)
        LanguageRow.addStretch(1)
        self.LanguageLabel = QLabel()
        AppearanceLayout.addRow(self.LanguageLabel, LanguageRow)
        AppearancePage = QWidget()
        AppearancePageLayout = QVBoxLayout(AppearancePage)
        AppearancePageLayout.setContentsMargins(12, 0, 12, 0)
        AppearancePageLayout.addWidget(Appearance)
        AppearancePageLayout.addStretch(1)
        self.SettingsTabs.addTab(AppearancePage, "")

        Paths = QGroupBox()
        self.PathsGroup = Paths
        PathForm = QFormLayout(Paths)
        self.PathEdits: dict[str, QLineEdit] = {}
        self.PathLabels: dict[str, QLabel] = {}
        ProjectEdit = QLineEdit(str(Path.cwd()))
        ProjectEdit.setReadOnly(True)
        ProjectEdit.setToolTip(str(Path.cwd()))
        ProjectLabel = QLabel()
        PathForm.addRow(ProjectLabel, ProjectEdit)
        self.ProjectDirectoryLabel = ProjectLabel
        PathDefinitions = (
            ("SourceDirectory", "SourceDirectory"),
            ("ClientTableDirectory", "ClientTableDirectory"),
            ("ClientCodeDirectory", "ClientCodeDirectory"),
            ("ServerTableDirectory", "ServerTableDirectory"),
            ("ServerCodeDirectory", "ServerCodeDirectory"),
        )
        for Key, LabelKey in PathDefinitions:
            Value = self.Settings["SourceDirectory"] if Key == "SourceDirectory" else self.Settings["Output"].get(Key, "")
            Edit = QLineEdit(Value)
            Browse = QPushButton()
            if self.Icons is not None:
                Browse.setIcon(self.Icons.Get("Folder"))
            Browse.setText(self.Language.Get("Choose"))
            Browse.setToolTip(self.Language.Get("SelectFolder"))
            Browse.clicked.connect(lambda Checked=False, Target=Edit: self._Browse(Target))
            Row = QWidget()
            RowLayout = QHBoxLayout(Row)
            RowLayout.setContentsMargins(0, 0, 0, 0)
            RowLayout.addWidget(Edit, 1)
            RowLayout.addWidget(Browse)
            Label = QLabel()
            PathForm.addRow(Label, Row)
            self.PathEdits[Key] = Edit
            self.PathLabels[Key] = Label
        PathsPage = QWidget()
        PathsPageLayout = QVBoxLayout(PathsPage)
        PathsPageLayout.setContentsMargins(12, 0, 12, 0)
        PathsPageLayout.addWidget(Paths)
        PathsPageLayout.addStretch(1)
        self.SettingsTabs.addTab(PathsPage, "")
        SyntaxGroup = QGroupBox()
        self.SyntaxGroup = SyntaxGroup
        SyntaxForm = QFormLayout(SyntaxGroup)
        self.SyntaxForm = SyntaxForm
        self.SyntaxEdits: dict[str, QLineEdit] = {}
        for Key in ("Level1Delimiter", "Level2Delimiter", "Level3Delimiter", "EscapeCharacter"):
            Edit = QLineEdit(str(self.Settings.get("ArraySyntax", {}).get(Key, "")))
            Edit.setMaxLength(2)
            SyntaxForm.addRow(QLabel(), Edit)
            self.SyntaxEdits[Key] = Edit
        SyntaxPage = QWidget()
        SyntaxPageLayout = QVBoxLayout(SyntaxPage)
        SyntaxPageLayout.setContentsMargins(12, 0, 12, 0)
        SyntaxPageLayout.addWidget(SyntaxGroup)
        SyntaxPageLayout.addStretch(1)
        self.SettingsTabs.addTab(SyntaxPage, "")

        AboutPage = QWidget()
        AboutLayout = QVBoxLayout(AboutPage)
        AboutLayout.setContentsMargins(12, 0, 12, 0)
        self.AboutTitle = QLabel()
        self.AboutTitle.setObjectName("PanelTitle")
        self.AboutText = QLabel()
        self.AboutText.setWordWrap(True)
        self.AboutText.setObjectName("Muted")
        AboutLayout.addWidget(self.AboutTitle)
        AboutLayout.addWidget(self.AboutText)
        AboutLayout.addStretch(1)
        self.SettingsTabs.addTab(AboutPage, "")
        self.SettingsTabs.tabBar().setVisible(False)
        SettingsBody = QHBoxLayout()
        SettingsBody.setContentsMargins(0, 0, 0, 0)
        SettingsBody.setSpacing(10)
        SettingsNav = QWidget()
        SettingsNav.setObjectName("SettingsNav")
        SettingsNav.setFixedWidth(150)
        SettingsNavLayout = QVBoxLayout(SettingsNav)
        SettingsNavLayout.setContentsMargins(0, 0, 0, 0)
        SettingsNavLayout.setSpacing(6)
        self.SettingsNavButtons: list[QPushButton] = []
        self.SettingsNavGroup = QButtonGroup(self)
        NavIcons = ("Theme", "Folder", "Table", "Info")
        for Index in range(4):
            Button = QPushButton()
            Button.setObjectName("SettingsNavButton")
            Button.setCheckable(True)
            if self.Icons is not None:
                Button.setIcon(self.Icons.Get(NavIcons[Index]))
            Button.clicked.connect(lambda Checked=False, Value=Index: self.SettingsTabs.setCurrentIndex(Value))
            self.SettingsNavGroup.addButton(Button, Index)
            self.SettingsNavButtons.append(Button)
            SettingsNavLayout.addWidget(Button)
        SettingsNavLayout.addStretch(1)
        self.SettingsTabs.currentChanged.connect(self._SyncSettingsNavigation)
        SettingsBody.addWidget(SettingsNav)
        SettingsBody.addWidget(self.SettingsTabs, 1)
        Root.addLayout(SettingsBody, 1)

        Footer = QHBoxLayout()
        self.ErrorLabel = QLabel()
        self.ErrorLabel.setObjectName("StatusError")
        self.ErrorLabel.setWordWrap(True)
        Footer.addWidget(self.ErrorLabel, 1)
        Footer.addStretch(1)
        self.CancelButton = QPushButton()
        if self.Icons is not None:
            self.CancelButton.setIcon(self.Icons.Get("Cancel"))
        self.CancelButton.setToolTip(self.Language.Get("Cancel"))
        self.CancelButton.clicked.connect(self.BackRequested.emit)
        self.ResetButton = QPushButton()
        if self.Icons is not None:
            self.ResetButton.setIcon(self.Icons.Get("Refresh"))
        self.ResetButton.setToolTip(self.Language.Get("ResetDefaults"))
        self.ResetButton.clicked.connect(self._ResetDefaults)
        self.SaveButton = QPushButton()
        self.SaveButton.setObjectName("Primary")
        if self.Icons is not None:
            self.SaveButton.setIcon(self.Icons.Get("Save"))
        self.SaveButton.setToolTip(self.Language.Get("Save"))
        self.SaveButton.clicked.connect(self._Save)
        Footer.addWidget(self.ResetButton)
        Footer.addWidget(self.CancelButton)
        Footer.addWidget(self.SaveButton)
        Root.addLayout(Footer)
        self.UpdateTexts()

    def SetSettings(self, Settings: dict) -> None:
        """Replace the editable snapshot when the page is opened again."""
        self.Settings = deepcopy(Settings)
        Interface = self.Settings.get("Interface", {})
        ThemeMode = Interface.get("ThemeMode", "Night")
        for Mode, Button in self.ThemeButtons.items():
            Button.setChecked(Mode == ThemeMode)

        AccentColor = Interface.get("AccentColor", self.AccentColors[0])
        if AccentColor not in self.AccentButtons:
            AccentColor = self.AccentColors[0]
        self._RefreshAccentButtons(AccentColor)

        LanguageCode = Interface.get("Language", "zh-CN")
        for Code, Button in self.LanguageButtons.items():
            Button.setChecked(Code == LanguageCode)

        self.PathEdits["SourceDirectory"].setText(str(self.Settings.get("SourceDirectory", "Data")))
        Output = self.Settings.get("Output", {})
        for Key in ("ClientTableDirectory", "ClientCodeDirectory", "ServerTableDirectory", "ServerCodeDirectory"):
            self.PathEdits[Key].setText(str(Output.get(Key, "")))

        Syntax = self.Settings.get("ArraySyntax", {})
        for Key, Edit in self.SyntaxEdits.items():
            Edit.setText(str(Syntax.get(Key, "")))
        self.ErrorLabel.clear()

    def _SetAccent(self, Color: str) -> None:
        self.Settings["Interface"]["AccentColor"] = Color
        self._RefreshAccentButtons(Color)
        self.AccentChanged.emit(Color)

    def _SetTheme(self, Mode: str) -> None:
        self.Settings["Interface"]["ThemeMode"] = Mode
        self.ThemeChanged.emit(Mode)

    def _RefreshAccentButtons(self, ActiveColor: str) -> None:
        for Color, Button in self.AccentButtons.items():
            Button.setChecked(Color == ActiveColor)
            BorderColor = "#FFFFFF" if Color == ActiveColor else "transparent"
            Button.setStyleSheet(
                f"background:{Color}; border: 2px solid {BorderColor}; border-radius: 5px;"
            )

    def _Browse(self, Edit: QLineEdit) -> None:
        Directory = QFileDialog.getExistingDirectory(self, self.Language.Get("SelectFolder"), Edit.text())
        if Directory:
            Edit.setText(Directory)

    def _Save(self) -> None:
        SyntaxValues = {Key: Edit.text() for Key, Edit in self.SyntaxEdits.items()}
        if any(len(Value) != 1 for Value in SyntaxValues.values()):
            self.ErrorLabel.setText(self.Language.Get("InvalidArraySyntax"))
            return
        if len(set(SyntaxValues.values())) != len(SyntaxValues):
            self.ErrorLabel.setText(self.Language.Get("ArraySyntaxMustBeUnique"))
            return
        self.ErrorLabel.clear()
        for Mode, Button in self.ThemeButtons.items():
            if Button.isChecked():
                self.Settings["Interface"]["ThemeMode"] = Mode
        self.Settings["SourceDirectory"] = self.PathEdits["SourceDirectory"].text().strip() or "Data"
        for Code, Button in self.LanguageButtons.items():
            if Button.isChecked():
                self.Settings["Interface"]["Language"] = Code
        ExistingOutput = self.Settings.get("Output", {})
        self.Settings["Output"] = {
            Key: self.PathEdits[Key].text().strip() or ExistingOutput.get(Key, Key)
            for Key in ("ClientTableDirectory", "ClientCodeDirectory", "ServerTableDirectory", "ServerCodeDirectory")
        }
        self.Settings["ArraySyntax"] = SyntaxValues
        self.SaveRequested.emit(deepcopy(self.Settings))

    def _ResetDefaults(self) -> None:
        """Reset only the editable settings snapshot; persistence happens on Save."""
        Interface = deepcopy(self.Settings.get("Interface", {}))
        TableList = deepcopy(self.Settings.get("TableList", {}))
        ExportTargets = deepcopy(self.Settings.get("ExportTargets", {}))
        self.Settings = deepcopy(DefaultSettings)
        self.Settings["Interface"].update({Key: Value for Key, Value in Interface.items() if Key in {"WindowWidth", "WindowHeight", "WindowMaximized"}})
        self.Settings["TableList"] = TableList
        self.Settings["ExportTargets"] = ExportTargets
        self.SetSettings(self.Settings)
        self.SettingsTabs.setCurrentIndex(0)
        self.ThemeChanged.emit(self.Settings["Interface"]["ThemeMode"])
        self.AccentChanged.emit(self.Settings["Interface"]["AccentColor"])

    def UpdateTexts(self) -> None:
        self.TitleLabel.setText(self.Language.Get("SettingsTitle"))
        self.BackButton.setText(self.Language.Get("BackToTool"))
        self.BackButton.setToolTip(self.Language.Get("BackToTool"))
        self.CancelButton.setText(self.Language.Get("Cancel"))
        self.CancelButton.setToolTip(self.Language.Get("Cancel"))
        self.ResetButton.setText(self.Language.Get("ResetDefaults"))
        self.ResetButton.setToolTip(self.Language.Get("ResetDefaults"))
        self.SaveButton.setText(self.Language.Get("Save"))
        self.SaveButton.setToolTip(self.Language.Get("Save"))
        self.ThemeLabel.setText(self.Language.Get("ThemeMode"))
        self.AccentLabel.setText(self.Language.Get("AccentColor"))
        self.ThemeButtons["Day"].setText(self.Language.Get("Day"))
        self.ThemeButtons["Night"].setText(self.Language.Get("Night"))
        self.ThemeButtons["System"].setText(self.Language.Get("System"))
        self.LanguageButtons["zh-CN"].setText(self.Language.Get("SimplifiedChinese"))
        self.LanguageButtons["en-US"].setText(self.Language.Get("English"))
        self._SetGroupTitle(self.AppearanceGroup, self.Language.Get("Appearance"))
        self._SetGroupTitle(self.PathsGroup, self.Language.Get("OutputDirectories"))
        self._SetGroupTitle(self.SyntaxGroup, self.Language.Get("ArraySyntax"))
        self.SettingsTabs.setTabText(0, self.Language.Get("SettingsAppearance"))
        self.SettingsTabs.setTabText(1, self.Language.Get("SettingsProjectPaths"))
        self.SettingsTabs.setTabText(2, self.Language.Get("SettingsArraySyntax"))
        self.SettingsTabs.setTabText(3, self.Language.Get("SettingsAbout"))
        NavTexts = (
            self.Language.Get("SettingsAppearance"),
            self.Language.Get("SettingsProjectPaths"),
            self.Language.Get("SettingsArraySyntax"),
            self.Language.Get("SettingsAbout"),
        )
        for Button, Text in zip(self.SettingsNavButtons, NavTexts):
            Button.setText(Text)
        self.AboutTitle.setText(self.Language.Get("SettingsAbout"))
        self.AboutText.setText(self.Language.Get("AboutText"))
        self.LanguageLabel.setText(self.Language.Get("Language"))
        SyntaxLabels = {
            "Level1Delimiter": "ArrayLevel1",
            "Level2Delimiter": "ArrayLevel2",
            "Level3Delimiter": "ArrayLevel3",
            "EscapeCharacter": "ArrayEscape",
        }
        for Key, Edit in self.SyntaxEdits.items():
            Label = self.SyntaxForm.labelForField(Edit)
            if Label is not None:
                Label.setText(self.Language.Get(SyntaxLabels[Key]))
        Labels = {
            "ProjectDirectory": "ProjectDirectory",
            "SourceDirectory": "SourceDirectory",
            "ClientTableDirectory": "ClientTableDirectory",
            "ClientCodeDirectory": "ClientCodeDirectory",
            "ServerTableDirectory": "ServerTableDirectory",
            "ServerCodeDirectory": "ServerCodeDirectory",
        }
        self.ProjectDirectoryLabel.setText(self.Language.Get("ProjectDirectory"))
        for Key, Label in self.PathLabels.items():
            Label.setText(self.Language.Get(Labels[Key]))
        for Edit in self.PathEdits.values():
            Parent = Edit.parentWidget()
            if Parent is not None:
                for Button in Parent.findChildren(QPushButton):
                    Button.setText(self.Language.Get("Choose"))
                    Button.setToolTip(self.Language.Get("SelectFolder"))
        self._SyncSettingsNavigation(self.SettingsTabs.currentIndex())

    def _SetGroupTitle(self, Group: QGroupBox, Title: str) -> None:
        Group.setTitle(Title)

    def _SyncSettingsNavigation(self, Index: int) -> None:
        for ButtonIndex, Button in enumerate(self.SettingsNavButtons):
            Button.setChecked(ButtonIndex == Index)
