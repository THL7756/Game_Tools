import os
import subprocess
from datetime import datetime
from pathlib import Path

from PySide6.QtCore import Qt, QUrl, Signal
from PySide6.QtGui import QCloseEvent, QDesktopServices, QIcon
from PySide6.QtWidgets import (
    QApplication,
    QButtonGroup,
    QDialog,
    QDialogButtonBox,
    QFileDialog,
    QFormLayout,
    QHBoxLayout,
    QLabel,
    QLineEdit,
    QMainWindow,
    QMenu,
    QMessageBox,
    QPushButton,
    QSizeGrip,
    QToolButton,
    QVBoxLayout,
    QWidget,
)

from App.Core.Models import ExportRequest, ExportTargetState, SourceFileModel, ValidationIssue
from App.Core.TableParser import ScanProject
from App.Core.TableValidator import ResolveLogicalTables, ValidateLogicalTables
from App.Services.ExportService import ExportProject
from App.Services.GeneratedFileManifest import GeneratedFileManifest
from App.Services.LanguageService import LanguageService
from App.Services.SettingsService import (
    GetOutputDirectories,
    GetSourceDirectory,
    LoadSettings,
    SaveSettings,
)
from App.UI.IssuesPanel import IssuesPanel
from App.UI.TableDetailPanel import TableDetailPanel
from App.UI.TableListPanel import TableListPanel
from App.UI.ThemeManager import ApplyTheme


class WindowDragBar(QWidget):
    DoubleClicked = Signal()

    def __init__(self) -> None:
        super().__init__()
        self.DragPosition = None
        self.setFixedHeight(32)

    def mousePressEvent(self, Event) -> None:
        if Event.button() == Qt.MouseButton.LeftButton:
            self.DragPosition = Event.globalPosition().toPoint() - self.window().pos()

    def mouseMoveEvent(self, Event) -> None:
        if self.DragPosition is not None and Event.buttons() & Qt.MouseButton.LeftButton:
            self.window().move(Event.globalPosition().toPoint() - self.DragPosition)

    def mouseReleaseEvent(self, Event) -> None:
        self.DragPosition = None

    def mouseDoubleClickEvent(self, Event) -> None:
        self.DoubleClicked.emit()


class SettingsDialog(QDialog):
    def __init__(self, Settings: dict, Language: LanguageService) -> None:
        super().__init__()
        self.Language = Language
        self.Settings = Settings
        self.setWindowTitle(Language.Get("SettingsTitle"))
        self.setMinimumWidth(620)
        Layout = QVBoxLayout(self)
        Form = QFormLayout()
        self.SourceEdit = self._MakePathField(Settings["SourceDirectory"], Form, "SourceDirectory")
        self.ClientTableEdit = self._MakePathField(Settings["Output"]["ClientTableDirectory"], Form, "ClientTableDirectory")
        self.ClientCodeEdit = self._MakePathField(Settings["Output"]["ClientCodeDirectory"], Form, "ClientCodeDirectory")
        self.ServerTableEdit = self._MakePathField(Settings["Output"]["ServerTableDirectory"], Form, "ServerTableDirectory")
        self.ServerCodeEdit = self._MakePathField(Settings["Output"]["ServerCodeDirectory"], Form, "ServerCodeDirectory")
        Layout.addLayout(Form)
        Buttons = QDialogButtonBox(QDialogButtonBox.StandardButton.Save | QDialogButtonBox.StandardButton.Cancel)
        Buttons.button(QDialogButtonBox.StandardButton.Save).setText(Language.Get("Save"))
        Buttons.button(QDialogButtonBox.StandardButton.Cancel).setText(Language.Get("Cancel"))
        Buttons.accepted.connect(self.accept)
        Buttons.rejected.connect(self.reject)
        Layout.addWidget(Buttons)

    def _MakePathField(self, Value: str, Form: QFormLayout, LabelKey: str) -> QLineEdit:
        Row = QWidget()
        Layout = QHBoxLayout(Row)
        Layout.setContentsMargins(0, 0, 0, 0)
        Edit = QLineEdit(Value)
        Browse = QPushButton(self.Language.Get("Choose"))
        Browse.clicked.connect(lambda: self._Browse(Edit))
        Layout.addWidget(Edit, 1)
        Layout.addWidget(Browse)
        Form.addRow(self.Language.Get(LabelKey), Row)
        return Edit

    def _Browse(self, Edit: QLineEdit) -> None:
        Directory = QFileDialog.getExistingDirectory(self, self.Language.Get("SelectFolder"), Edit.text())
        if Directory:
            Edit.setText(Directory)

    def GetSettings(self) -> dict:
        Result = dict(self.Settings)
        Result["SourceDirectory"] = self.SourceEdit.text().strip() or "Data"
        Result["Output"] = {
            "ClientTableDirectory": self.ClientTableEdit.text().strip() or "客户端表格数据",
            "ClientCodeDirectory": self.ClientCodeEdit.text().strip() or "客户端代码",
            "ServerTableDirectory": self.ServerTableEdit.text().strip() or "服务器表格数据",
            "ServerCodeDirectory": self.ServerCodeEdit.text().strip() or "服务器代码",
        }
        return Result


class MainWindow(QMainWindow):
    def __init__(self, ProjectRoot: Path) -> None:
        super().__init__()
        self.ProjectRoot = ProjectRoot
        self.ResourcesDirectory = ProjectRoot / "App" / "Resources"
        self.IconsDirectory = self.ResourcesDirectory / "Icons"
        self.Settings = LoadSettings(ProjectRoot)
        self.Language = LanguageService(ProjectRoot / "Languages", self.Settings["Interface"]["Language"])
        ManifestRoot = Path(os.environ.get("LOCALAPPDATA", Path.home() / "AppData" / "Local"))
        self.Manifest = GeneratedFileManifest(ManifestRoot / "TableTools" / "GeneratedFiles.json")
        self.Project = ScanProject(
            GetSourceDirectory(ProjectRoot, self.Settings),
            self.Settings["ArraySyntax"]["EscapeCharacter"],
        )
        self.setObjectName("Root")
        self.setWindowTitle(self.Language.Get("AppTitle"))
        self.setWindowFlag(Qt.WindowType.FramelessWindowHint)
        self.setMinimumSize(1120, 720)
        self.resize(
            int(self.Settings["Interface"]["WindowWidth"]),
            int(self.Settings["Interface"]["WindowHeight"]),
        )
        if self.Settings["Interface"]["WindowMaximized"]:
            self.showMaximized()
        self._BuildUi()
        self.RefreshProject()

    def _BuildUi(self) -> None:
        Root = QWidget()
        Root.setObjectName("Root")
        RootLayout = QVBoxLayout(Root)
        RootLayout.setContentsMargins(0, 0, 0, 0)
        RootLayout.setSpacing(0)
        self.setCentralWidget(Root)

        self.TitleBar = WindowDragBar()
        self.TitleBar.setObjectName("TitleBar")
        TitleLayout = QHBoxLayout(self.TitleBar)
        TitleLayout.setContentsMargins(16, 0, 12, 0)
        AppIcon = QLabel()
        AppIcon.setPixmap(QIcon(str(self.IconsDirectory / "AppIcon.svg")).pixmap(16, 16))
        TitleLayout.addWidget(AppIcon)
        TitleLayout.addSpacing(8)
        TitleLayout.addWidget(QLabel(self.Language.Get("AppTitle")))
        TitleLayout.addStretch(1)
        self.MinimizeButton = self._WindowButton("WindowMinimize.svg", self.showMinimized)
        self.MaximizeButton = self._WindowButton("WindowMaximize.svg", self._ToggleMaximized)
        self.CloseButton = self._WindowButton("WindowClose.svg", self.close)
        TitleLayout.addWidget(self.MinimizeButton)
        TitleLayout.addWidget(self.MaximizeButton)
        TitleLayout.addWidget(self.CloseButton)
        self.TitleBar.DoubleClicked.connect(self._ToggleMaximized)
        RootLayout.addWidget(self.TitleBar)

        MenuBar = QWidget()
        MenuBar.setObjectName("MenuBar")
        MenuLayout = QHBoxLayout(MenuBar)
        MenuLayout.setContentsMargins(0, 0, 20, 0)
        MenuLayout.setSpacing(0)
        Sidebar = QToolButton()
        Sidebar.clicked.connect(self._ToggleSidebar)
        Sidebar.setIcon(QIcon(str(self.IconsDirectory / "SidebarToggle.svg")))
        Sidebar.setFixedSize(46, 44)
        Sidebar.setToolTip(self.Language.Get("ToolTitle"))
        MenuLayout.addWidget(Sidebar)
        MenuLayout.addWidget(self._MenuButton("FileMenu", [
            (self.Language.Get("Refresh"), self.RefreshProject),
            (self.Language.Get("Settings"), self._OpenSettings),
        ]))
        MenuLayout.addWidget(self._MenuButton("EditMenu", [
            (self.Language.Get("SelectAll"), lambda: self.TableList.SetSelectedPaths({str(File.Path) for File in self.Project.SourceFiles})),
        ]))
        MenuLayout.addWidget(self._MenuButton("ViewMenu", [
            (self.Language.Get("Day"), lambda: self._ChangeTheme("Day")),
            (self.Language.Get("Night"), lambda: self._ChangeTheme("Night")),
            (self.Language.Get("System"), lambda: self._ChangeTheme("System")),
        ]))
        DocsButton = self._MenuButton("DocsMenu", [])
        DocsButton.clicked.connect(lambda: QMessageBox.information(self, self.Language.Get("DocsMenu"), self.Language.Get("DocsMessage")))
        MenuLayout.addWidget(DocsButton)
        MenuLayout.addStretch(1)
        self.ModeLabel = QLabel(self.Language.Get("PageMode"))
        self.ModeLabel.setObjectName("Muted")
        MenuLayout.addWidget(self.ModeLabel)
        MenuLayout.addSpacing(12)
        self.ModeGroup = QButtonGroup(self)
        self.ModeGroup.setExclusive(True)
        for Mode in ("Day", "Night", "System"):
            Button = QPushButton(self.Language.Get(Mode))
            Button.setCheckable(True)
            Button.setFixedHeight(30)
            Button.clicked.connect(lambda Checked, Value=Mode: self._ChangeTheme(Value))
            self.ModeGroup.addButton(Button)
            MenuLayout.addWidget(Button)
            if self.Settings["Interface"]["ThemeMode"] == Mode:
                Button.setChecked(True)
        MenuLayout.addSpacing(10)
        self.LanguageButton = QToolButton()
        self.LanguageButton.setText("简体中文" if self.Language.CurrentCode == "zh-CN" else "English")
        self.LanguageButton.setPopupMode(QToolButton.ToolButtonPopupMode.InstantPopup)
        self.LanguageMenu = QMenu(self.LanguageButton)
        self.LanguageMenu.addAction("简体中文", lambda: self._ChangeLanguage("zh-CN"))
        self.LanguageMenu.addAction("English", lambda: self._ChangeLanguage("en-US"))
        self.LanguageButton.setMenu(self.LanguageMenu)
        MenuLayout.addWidget(self.LanguageButton)
        self.SettingsButton = QPushButton(self.Language.Get("Settings"))
        self.SettingsButton.clicked.connect(self._OpenSettings)
        MenuLayout.addWidget(self.SettingsButton)
        RootLayout.addWidget(MenuBar)

        Body = QHBoxLayout()
        Body.setContentsMargins(0, 0, 0, 0)
        Body.setSpacing(0)
        self.ToolRail = QWidget()
        self.ToolRail.setObjectName("ToolRail")
        self.ToolRail.setFixedWidth(56)
        self.ToolRail.setVisible(self.Settings["Interface"].get("SidebarExpanded", True))
        ToolLayout = QVBoxLayout(self.ToolRail)
        ToolLayout.setContentsMargins(8, 20, 8, 8)
        ToolLayout.setSpacing(8)
        for Index in range(5):
            Tool = QToolButton()
            Tool.setIcon(QIcon(str(self.IconsDirectory / f"ToolbarIcon01.svg") if Index == 0 else str(self.IconsDirectory / f"ToolbarIcon0{Index + 1}.svg")))
            Tool.setFixedSize(40, 40)
            Tool.setCheckable(True)
            Tool.setChecked(Index == 0)
            Tool.setEnabled(Index == 0)
            Tool.setToolTip(self.Language.Get("ToolTitle") if Index == 0 else self.Language.Get("ToolTitle") + f" {Index + 1}")
            if Index == 0:
                Tool.setStyleSheet("background:#22334E;border-color:#4D8DF7;")
            ToolLayout.addWidget(Tool)
        ToolLayout.addStretch(1)
        Body.addWidget(self.ToolRail)

        Workspace = QVBoxLayout()
        Workspace.setContentsMargins(0, 0, 0, 0)
        Workspace.setSpacing(0)
        WorkspaceHeader = QWidget()
        WorkspaceHeader.setObjectName("WorkspaceHeader")
        WorkspaceHeader.setFixedHeight(56)
        HeaderLayout = QHBoxLayout(WorkspaceHeader)
        HeaderLayout.setContentsMargins(20, 0, 20, 0)
        WorkspaceTitle = QLabel(self.Language.Get("ToolTitle"))
        WorkspaceTitle.setObjectName("Title")
        HeaderLayout.addWidget(WorkspaceTitle)
        HeaderLayout.addStretch(1)
        self.SyncLabel = QLabel(self.Language.Get("Synced", Time=datetime.now().strftime("%H:%M")))
        self.SyncLabel.setObjectName("Muted")
        HeaderLayout.addWidget(self.SyncLabel)
        Workspace.addWidget(WorkspaceHeader)

        MainRow = QHBoxLayout()
        MainRow.setContentsMargins(0, 0, 0, 0)
        MainRow.setSpacing(0)
        self.TableList = TableListPanel(
            self.Language,
            set(self.Settings["TableList"]["FavoriteFiles"]),
            list(self.Settings["TableList"]["RecentFiles"]),
            self.IconsDirectory,
        )
        self.TableList.SelectionChanged.connect(self._UpdateIssues)
        self.TableList.CurrentFileChanged.connect(self._CurrentFileChanged)
        self.TableList.OpenPathRequested.connect(self._OpenFilePath)
        self.TableList.FavoriteChanged.connect(lambda PathText, IsFavorite: self._SaveState())
        MainRow.addWidget(self.TableList)

        DetailColumn = QVBoxLayout()
        DetailColumn.setContentsMargins(12, 12, 12, 12)
        DetailColumn.setSpacing(10)
        self.TableDetail = TableDetailPanel(self.Language, self.IconsDirectory)
        self.TableDetail.RefreshRequested.connect(self.RefreshProject)
        self.TableDetail.OpenExcelRequested.connect(self._OpenExcel)
        DetailColumn.addWidget(self.TableDetail, 1)
        self.IssuesPanel = IssuesPanel(self.Language, self.IconsDirectory)
        self.IssuesPanel.SetTargetState(ExportTargetState(**self.Settings["ExportTargets"]))
        self.IssuesPanel.BuildRequested.connect(self._Build)
        DetailColumn.addWidget(self.IssuesPanel)
        MainRow.addLayout(DetailColumn, 1)
        Workspace.addLayout(MainRow, 1)
        Body.addLayout(Workspace, 1)
        RootLayout.addLayout(Body, 1)

        StatusBar = QWidget()
        StatusBar.setObjectName("StatusBar")
        StatusBar.setFixedHeight(24)
        StatusLayout = QHBoxLayout(StatusBar)
        StatusLayout.setContentsMargins(16, 0, 8, 0)
        StatusLayout.setSpacing(8)
        self.PathStatus = QLabel(str(self.ProjectRoot))
        self.PathStatus.setObjectName("Muted")
        StatusLayout.addWidget(self.PathStatus)
        StatusLayout.addStretch(1)
        self.BuildLog = QWidget()
        self.BuildLog.setObjectName("BuildLog")
        BuildLayout = QHBoxLayout(self.BuildLog)
        BuildLayout.setContentsMargins(10, 2, 10, 2)
        BuildLayout.setSpacing(6)
        self.BuildStatus = QLabel("IDLE")
        self.BuildStatus.setObjectName("Muted")
        self.BuildLogTime = QLabel("--:--:--")
        self.BuildLogTime.setObjectName("Subtle")
        BuildLayout.addWidget(self.BuildStatus)
        BuildLayout.addWidget(QLabel(self.Language.Get("Log")))
        BuildLayout.addWidget(self.BuildLogTime)
        StatusLayout.addWidget(self.BuildLog)
        self.VersionLabel = QLabel(self.Language.Get("Version"))
        self.VersionLabel.setObjectName("Muted")
        StatusLayout.addWidget(self.VersionLabel)
        StatusLayout.addWidget(QSizeGrip(self))
        RootLayout.addWidget(StatusBar)

    def _WindowButton(self, IconName: str, Slot) -> QToolButton:
        Button = QToolButton()
        Button.setIcon(QIcon(str(self.IconsDirectory / IconName)))
        Button.setFixedSize(36, 28)
        Button.clicked.connect(Slot)
        return Button

    def _MenuButton(self, TextKey: str, Actions: list[tuple[str, object]]) -> QToolButton:
        Button = QToolButton()
        Button.setText(self.Language.Get(TextKey))
        Button.setPopupMode(QToolButton.ToolButtonPopupMode.InstantPopup)
        Menu = QMenu(Button)
        for Text, Slot in Actions:
            Menu.addAction(Text, Slot)
        Button.setMenu(Menu)
        return Button

    def RefreshProject(self) -> None:
        self.Project = ScanProject(
            GetSourceDirectory(self.ProjectRoot, self.Settings),
            self.Settings["ArraySyntax"]["EscapeCharacter"],
        )
        Selected = {
            PathText
            for PathText in self.Settings["TableList"].get("SelectedFiles", [])
            if any(str(File.Path) == PathText for File in self.Project.SourceFiles)
        }
        self.TableList.SetFiles(
            self.Project.SourceFiles,
            Selected,
            set(self.Settings["TableList"].get("FavoriteFiles", [])),
            list(self.Settings["TableList"].get("RecentFiles", [])),
        )
        self.TableList.SetFilterMode(self.Settings["TableList"].get("FilterMode", "All"))
        self.TableList.SetSearchText(self.Settings["TableList"].get("SearchText", ""))
        self._UpdateIssues()
        if self.Project.SourceFiles:
            self.TableDetail.SetFile(self.Project.SourceFiles[0])

    def _CurrentFileChanged(self, SourceFile: SourceFileModel) -> None:
        self.TableDetail.SetFile(SourceFile)
        self.TableList.MarkRecent(str(SourceFile.Path))
        self._SaveState()

    def _UpdateIssues(self) -> None:
        SelectedPaths = self.TableList.GetSelectedPaths()
        SelectedTables = ResolveLogicalTables(SelectedPaths, self.Project.LogicalTables)
        RelevantFiles = {
            SourceFile
            for Table in SelectedTables.values()
            for SourceFile in Table.SourceFiles
        }
        Issues = [Issue for Issue in self.Project.Issues if Issue.Location.FilePath in RelevantFiles]
        Issues.extend(ValidateLogicalTables(SelectedTables, self.Project.LogicalTables))
        self.IssuesPanel.SetIssues(Issues, len(SelectedPaths))

    def _Build(self) -> None:
        SelectedPaths = self.TableList.GetSelectedPaths()
        if not SelectedPaths:
            QMessageBox.information(self, self.Language.Get("Build"), self.Language.Get("NoSelection"))
            return
        TargetState = self.IssuesPanel.GetTargetState()
        Result = ExportProject(
            self.Project,
            ExportRequest(SelectedPaths, TargetState),
            GetOutputDirectories(self.ProjectRoot, self.Settings),
            self.Manifest,
        )
        Timestamp = datetime.now().strftime("%H:%M:%S")
        self.BuildLogTime.setText(Timestamp)
        if Result.Success:
            self.BuildStatus.setText(self.Language.Get("BuildDone"))
            self.Settings["LastBuild"] = {
                "Status": "Done",
                "Message": Result.Messages[0] if Result.Messages else "",
                "Timestamp": datetime.now().isoformat(),
            }
            self.TableList.SetSelectedPaths(set())
            self._UpdateIssues()
            QMessageBox.information(self, self.Language.Get("Build"), "\n".join(Result.Messages))
        else:
            self.BuildStatus.setText(self.Language.Get("BuildError"))
            self.Settings["LastBuild"] = {
                "Status": "Error",
                "Message": "\n".join(Result.Messages),
                "Timestamp": datetime.now().isoformat(),
            }
            QMessageBox.warning(self, self.Language.Get("Build"), "\n".join(Result.Messages[:8]))
        self._SaveState()

    def _OpenFilePath(self, PathText: str) -> None:
        FilePath = Path(PathText)
        if os.name == "nt":
            subprocess.Popen(["explorer", "/select,", str(FilePath)])
        else:
            QDesktopServices.openUrl(QUrl.fromLocalFile(str(FilePath.parent)))

    def _OpenExcel(self) -> None:
        if self.TableDetail.SourceFile is not None:
            QDesktopServices.openUrl(QUrl.fromLocalFile(str(self.TableDetail.SourceFile.Path)))

    def _OpenSettings(self) -> None:
        Dialog = SettingsDialog(self.Settings, self.Language)
        if Dialog.exec() != QDialog.DialogCode.Accepted:
            return
        self.Settings = Dialog.GetSettings()
        SaveSettings(self.ProjectRoot, self.Settings)
        self.RefreshProject()

    def _ChangeTheme(self, Mode: str) -> None:
        self.Settings["Interface"]["ThemeMode"] = Mode
        ApplyTheme(QApplication.instance(), Mode, self.ResourcesDirectory)
        self._SaveState()

    def _ChangeLanguage(self, LanguageCode: str) -> None:
        self.Settings["Interface"]["Language"] = LanguageCode
        self._SaveState()
        self.Language.Load(LanguageCode)
        self.Settings = LoadSettings(self.ProjectRoot)
        self._BuildUi()
        self.RefreshProject()
        self._SaveState()

    def _ToggleSidebar(self) -> None:
        IsVisible = not self.ToolRail.isVisible()
        self.ToolRail.setVisible(IsVisible)
        self.Settings["Interface"]["SidebarExpanded"] = IsVisible
        self._SaveState()

    def _ToggleMaximized(self) -> None:
        if self.isMaximized():
            self.showNormal()
        else:
            self.showMaximized()

    def _SaveState(self) -> None:
        self.Settings["Interface"]["WindowWidth"] = self.width()
        self.Settings["Interface"]["WindowHeight"] = self.height()
        self.Settings["Interface"]["WindowMaximized"] = self.isMaximized()
        self.Settings["TableList"]["FavoriteFiles"] = sorted(self.TableList.GetFavorites())
        self.Settings["TableList"]["RecentFiles"] = self.TableList.GetRecents()
        self.Settings["TableList"]["SelectedFiles"] = sorted(self.TableList.GetSelectedPaths())
        self.Settings["TableList"]["FilterMode"] = self.TableList.GetFilterMode()
        self.Settings["TableList"]["SearchText"] = self.TableList.GetSearchText()
        self.Settings["ExportTargets"] = {
            "ClientData": self.IssuesPanel.ClientDataCheck.isChecked(),
            "ClientCode": self.IssuesPanel.ClientCodeCheck.isChecked(),
            "ServerData": self.IssuesPanel.ServerDataCheck.isChecked(),
            "ServerCode": False,
        }
        SaveSettings(self.ProjectRoot, self.Settings)

    def closeEvent(self, Event: QCloseEvent) -> None:
        self._SaveState()
        Event.accept()
