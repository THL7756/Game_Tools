# 用途：装配 TableTools 主窗口、页面状态、菜单、缓存、日志、监听和导出流程。
# 最近修改日期：2026-10-07
# 作者：Codex

import os
import subprocess
import time
from datetime import datetime
from pathlib import Path

from PySide6.QtCore import QSize, QUrl, Qt, Signal
from PySide6.QtGui import QCloseEvent, QDesktopServices
from PySide6.QtWidgets import (
    QApplication,
    QHBoxLayout,
    QLabel,
    QMainWindow,
    QMessageBox,
    QPushButton,
    QStackedWidget,
    QToolButton,
    QVBoxLayout,
    QWidget,
)

from App.Core.Models import ArraySyntaxConfig, ExportRequest, ExportResult, ExportTargetState, SourceFileModel
from App.Core.TableParser import ScanProject
from App.Core.TableValidator import NormalizePath, ResolveLogicalTables, ValidateLogicalTables
from App.Services.ExportService import ExportProject
from App.Services.GeneratedFileManifest import GeneratedFileManifest
from App.Services.IconService import IconService
from App.Services.LanguageService import LanguageService
from App.Services.LogService import LogService
from App.Services.SettingsService import GetOutputDirectories, GetSourceDirectory, LoadSettings, SaveSettings
from App.Services.SourceDirectoryWatcher import SourceDirectoryWatcher
from App.Services.TableCacheService import TableCacheService
from App.UI.ApplicationMenuBar import ApplicationMenuBar
from App.UI.IssuesPanel import IssuesPanel
from App.UI.LogPanel import LogPanel
from App.UI.SettingsPage import SettingsPage
from App.UI.StatusPanel import StatusPanel
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


class MainWindow(QMainWindow):
    def __init__(self, ProjectRoot: Path) -> None:
        super().__init__()
        self.ProjectRoot = ProjectRoot
        self.ResourcesDirectory = ProjectRoot / "App" / "Resources"
        self.IconsDirectory = self.ResourcesDirectory / "Icons"
        self.Settings = LoadSettings(ProjectRoot)
        self.Language = LanguageService(ProjectRoot / "Languages", self.Settings["Interface"]["Language"])
        QApplication.instance().setProperty("TableToolsLanguage", self.Language.CurrentCode)
        self.Logs = LogService(ProjectRoot / "tabletools.log", self)
        self.Icons = IconService(self.IconsDirectory, lambda Message: self.Logs.Write("WARNING", Message))
        self.Cache = TableCacheService(ProjectRoot / "table_data.json")
        ManifestRoot = Path(os.environ.get("LOCALAPPDATA", Path.home() / "AppData" / "Local"))
        self.Manifest = GeneratedFileManifest(ManifestRoot / "TableTools" / "GeneratedFiles.json")
        self.CurrentPage = "Config"
        self.CurrentPath = ""
        self.CurrentSheet = ""
        self.SettingsPreviewSnapshot: tuple[str, str] | None = None
        self.Project = self._LoadProject()
        self.Watcher = SourceDirectoryWatcher(self)
        self.Watcher.Changed.connect(self._OnSourceChanged)
        self.Watcher.PathChanged.connect(self._OnSourcePathChanged)
        self.setObjectName("Root")
        self.setWindowTitle(self.Language.Get("AppTitle"))
        self.setWindowIcon(self.Icons.Get("AppIcon"))
        self.setWindowFlag(Qt.WindowType.FramelessWindowHint)
        self.setMinimumSize(1120, 720)
        self.resize(int(self.Settings["Interface"]["WindowWidth"]), int(self.Settings["Interface"]["WindowHeight"]))
        if self.Settings["Interface"].get("WindowMaximized"):
            self.showMaximized()
        self._BuildUi()
        self.Logs.Write("INFO", "应用启动", SourceDirectory=str(GetSourceDirectory(ProjectRoot, self.Settings)))
        self.RefreshProject()

    def _GetSyntax(self) -> ArraySyntaxConfig:
        Syntax = self.Settings.get("ArraySyntax", {})
        return ArraySyntaxConfig(str(Syntax.get("Level1Delimiter", "#")), str(Syntax.get("Level2Delimiter", "|")), str(Syntax.get("Level3Delimiter", ";")), str(Syntax.get("EscapeCharacter", "\\")))

    def _LoadProject(self):
        RootDirectory = GetSourceDirectory(self.ProjectRoot, self.Settings)
        try:
            Cached = self.Cache.Load(RootDirectory, self._GetSyntax())
        except (OSError, TypeError, ValueError, KeyError) as Error:
            Cached = None
            self.Logs.Write("WARNING", "解析缓存无法读取，将重新扫描", Error=str(Error))
        if Cached is not None:
            self.Logs.Write("INFO", "已加载解析缓存", FileCount=len(Cached.SourceFiles))
            return Cached
        self.Logs.Write("INFO", "开始扫描表格", SourceDirectory=str(RootDirectory))
        Project = ScanProject(RootDirectory, self._GetSyntax())
        self.Cache.Save(Project, RootDirectory, self._GetSyntax())
        self.Logs.Write("INFO", "表格扫描完成", FileCount=len(Project.SourceFiles), IssueCount=len(Project.Issues))
        return Project

    def _BuildUi(self) -> None:
        self.setWindowTitle(self.Language.Get("AppTitle"))
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
        AppIcon.setPixmap(self.Icons.Pixmap("AppIcon", 16))
        AppIcon.setAttribute(Qt.WidgetAttribute.WA_TransparentForMouseEvents, True)
        TitleLayout.addWidget(AppIcon)
        TitleLayout.addSpacing(8)
        TitleLabel = QLabel(self.Language.Get("AppTitle"))
        TitleLabel.setObjectName("WindowTitle")
        TitleLabel.setAttribute(Qt.WidgetAttribute.WA_TransparentForMouseEvents, True)
        TitleLayout.addWidget(TitleLabel)
        TitleLayout.addStretch(1)
        self.MinimizeButton = self._WindowButton("WindowMinimize", self.showMinimized, "Minimize")
        self.MaximizeButton = self._WindowButton("WindowMaximize", self._ToggleMaximized, "Maximize")
        self.CloseButton = self._WindowButton("WindowClose", self.close, "Close")
        TitleLayout.addWidget(self.MinimizeButton)
        TitleLayout.addWidget(self.MaximizeButton)
        TitleLayout.addWidget(self.CloseButton)
        self._UpdateMaximizeButton()
        self.TitleBar.DoubleClicked.connect(self._ToggleMaximized)
        RootLayout.addWidget(self.TitleBar)

        self.MenuBar = ApplicationMenuBar(self.Language, self.Icons, self)
        self.MenuBar.LogToggled.connect(self._SetLogVisible)
        self.MenuBar.SidebarToggled.connect(self._ToggleSidebar)
        self.MenuBar.LanguageChanged.connect(self._ChangeLanguage)
        self.MenuBar.ThemeChanged.connect(self._ChangeTheme)
        self.MenuBar.SettingsRequested.connect(self._OpenSettings)
        self.MenuBar.SetContext(False, False)
        self.MenuBar.SetThemeMode(self.Settings["Interface"].get("ThemeMode", "Night"))
        self.MenuBar.SetLanguageCode(self.Language.CurrentCode)
        self.MenuBar.SetSidebarVisible(self.Settings["Interface"].get("SidebarExpanded", True))
        RootLayout.addWidget(self.MenuBar)

        Body = QHBoxLayout()
        Body.setContentsMargins(0, 0, 0, 0)
        Body.setSpacing(0)
        self.ToolRail = self._BuildToolRail()
        Body.addWidget(self.ToolRail)

        self.WorkspaceStack = QStackedWidget()
        self.ConfigPage = self._BuildConfigPage()
        self.SettingsPage = SettingsPage(self.Settings, self.Language, self.Icons)
        self.SettingsPage.BackRequested.connect(self._ShowConfigPage)
        self.SettingsPage.SaveRequested.connect(self._SaveSettingsPage)
        self.SettingsPage.ThemeChanged.connect(self._PreviewSettingsTheme)
        self.SettingsPage.AccentChanged.connect(self._PreviewSettingsAccent)
        self.WorkspaceStack.addWidget(self.ConfigPage)
        self.WorkspaceStack.addWidget(self.SettingsPage)
        Body.addWidget(self.WorkspaceStack, 1)
        RootLayout.addLayout(Body, 1)

        self.LogPanel = LogPanel(self.Language, self.Icons, self.Logs, self)
        self.LogPanel.setVisible(False)
        RootLayout.addWidget(self.LogPanel)
        self.StatusBar = StatusPanel(str(self.ProjectRoot), self.Language.Get("Version"), self.Icons, self)
        self.StatusBar.LogButton.setIcon(self.Icons.Get("Log"))
        self.StatusBar.LogButton.setText("")
        self.StatusBar.LogButton.setToolTip(self.Language.Get("OpenLogTooltip"))
        self.StatusBar.LogRequested.connect(lambda: self._SetLogVisible(not self.LogPanel.isVisible()))
        RootLayout.addWidget(self.StatusBar)

    def _BuildToolRail(self) -> QWidget:
        Rail = QWidget()
        Rail.setObjectName("ToolRail")
        Rail.setFixedWidth(176)
        Rail.setVisible(self.Settings["Interface"].get("SidebarExpanded", True))
        Layout = QVBoxLayout(Rail)
        Layout.setContentsMargins(12, 20, 12, 16)
        Layout.setSpacing(8)
        self.ToolButtons: list[QToolButton] = []
        ToolNames = ("ToolTitle", "ProjectHelper", "AssetTool", "AudioTool", "PromptRepository")
        ToolIcons = ("TableTool", "FolderKanban", "Images", "AudioLines", "NotebookText")
        for Index, NameKey in enumerate(ToolNames):
            Tool = QToolButton()
            Tool.setObjectName("ToolNavigationButton")
            Tool.setIcon(self.Icons.Get(ToolIcons[Index]))
            Tool.setIconSize(QSize(20, 20))
            Tool.setText(self.Language.Get(NameKey))
            Tool.setToolButtonStyle(Qt.ToolButtonStyle.ToolButtonTextBesideIcon)
            Tool.setFixedHeight(40)
            Tool.setCheckable(True)
            Tool.setChecked(Index == 0)
            Tool.setEnabled(Index == 0)
            if Index == 0:
                Tool.clicked.connect(self._ShowConfigPage)
            else:
                Tool.setToolTip(self.Language.Get("ToolUnavailable"))
            self.ToolButtons.append(Tool)
            Layout.addWidget(Tool)
        Layout.addStretch(1)
        return Rail

    def _BuildConfigPage(self) -> QWidget:
        Page = QWidget()
        Workspace = QVBoxLayout(Page)
        Workspace.setContentsMargins(0, 0, 0, 0)
        Workspace.setSpacing(0)
        Header = QWidget()
        Header.setObjectName("WorkspaceHeader")
        Header.setFixedHeight(56)
        HeaderLayout = QHBoxLayout(Header)
        HeaderLayout.setContentsMargins(20, 0, 20, 0)
        self.WorkspaceTitle = QLabel(self.Language.Get("ToolTitle"))
        self.WorkspaceTitle.setObjectName("Title")
        HeaderLayout.addWidget(self.WorkspaceTitle)
        HeaderLayout.addStretch(1)
        self.SyncLabel = QLabel()
        self.SyncLabel.setObjectName("Muted")
        HeaderLayout.addWidget(self.SyncLabel)
        self.RefreshListButton = QPushButton()
        self.RefreshListButton.setText(self.Language.Get("RefreshList"))
        self.RefreshListButton.setIcon(self.Icons.Get("Refresh"))
        self.RefreshListButton.setToolTip(self.Language.Get("RefreshTooltip"))
        self.RefreshListButton.clicked.connect(self.RefreshProject)
        HeaderLayout.addWidget(self.RefreshListButton)
        self.OpenSourceButton = QPushButton()
        self.OpenSourceButton.setText(self.Language.Get("OpenSourceDirectory"))
        self.OpenSourceButton.setIcon(self.Icons.Get("Folder"))
        self.OpenSourceButton.setToolTip(self.Language.Get("OpenSourceDirectory"))
        self.OpenSourceButton.clicked.connect(self._OpenSourceDirectory)
        HeaderLayout.addWidget(self.OpenSourceButton)
        Workspace.addWidget(Header)

        MainRow = QHBoxLayout()
        MainRow.setContentsMargins(0, 0, 0, 0)
        MainRow.setSpacing(0)
        self.TableList = TableListPanel(self.Language, set(self.Settings["TableList"].get("FavoriteFiles", [])), list(self.Settings["TableList"].get("RecentFiles", [])), self.IconsDirectory)
        self.TableList.SelectionChanged.connect(self._UpdateIssues)
        self.TableList.SelectionChanged.connect(self._SaveState)
        self.TableList.SelectionChanged.connect(
            lambda: self.Logs.Write("INFO", "源文件选择已更新", FileCount=len(self.TableList.GetSelectedPaths()))
        )
        self.TableList.StateChanged.connect(self._OnTableListStateChanged)
        self.TableList.CurrentFileChanged.connect(self._CurrentFileChanged)
        self.TableList.OpenPathRequested.connect(self._OpenFilePath)
        self.TableList.FavoriteChanged.connect(lambda PathText, IsFavorite: self._SaveState())
        self.TableList.SearchEdit.textChanged.connect(lambda Text: self.MenuBar.SetContext(self.TableDetail.SourceFile is not None, bool(self.TableList.GetSelectedPaths()), bool(Text.strip())))
        self.TableList.setFixedWidth(260)
        MainRow.addWidget(self.TableList)

        DetailColumn = QVBoxLayout()
        DetailColumn.setContentsMargins(12, 12, 12, 12)
        DetailColumn.setSpacing(10)
        self.TableDetail = TableDetailPanel(self.Language, self.IconsDirectory)
        self.TableDetail.RefreshRequested.connect(self.RefreshProject)
        self.TableDetail.OpenExcelRequested.connect(self._OpenExcel)
        self.TableDetail.CurrentSheetChanged.connect(self._SetCurrentSheet)
        DetailColumn.addWidget(self.TableDetail, 1)
        self.IssuesPanel = IssuesPanel(self.Language, self.IconsDirectory)
        self.IssuesPanel.SetTargetState(ExportTargetState(**self.Settings["ExportTargets"]))
        self.IssuesPanel.BuildRequested.connect(self._Build)
        self.IssuesPanel.TargetChanged.connect(self._OnTargetChanged)
        DetailColumn.addWidget(self.IssuesPanel)
        MainRow.addLayout(DetailColumn, 1)
        Workspace.addLayout(MainRow, 1)
        return Page

    def _SetCurrentSheet(self, SheetName: str) -> None:
        self.CurrentSheet = SheetName

    def _OnTableListStateChanged(self) -> None:
        self._SaveState()
        if hasattr(self, "MenuBar"):
            self.MenuBar.SetContext(
                self.TableDetail.SourceFile is not None,
                bool(self.TableList.GetSelectedPaths()),
                bool(self.TableList.GetSearchText().strip()),
            )

    def _OnTargetChanged(self, TargetState: ExportTargetState) -> None:
        self.Logs.Write(
            "INFO",
            "导出目标已更新",
            ClientData=TargetState.ClientData,
            ClientCode=TargetState.ClientCode,
            ServerData=TargetState.ServerData,
        )
        self._SaveState()

    def _WindowButton(self, IconName: str, Slot, TooltipKey: str) -> QToolButton:
        Button = QToolButton()
        Button.setObjectName("WindowControl")
        Button.setIcon(self.Icons.Get(IconName))
        Button.setFixedSize(36, 28)
        Button.setToolTip(self.Language.Get(TooltipKey))
        Button.setAccessibleName(self.Language.Get(TooltipKey))
        Button.clicked.connect(Slot)
        return Button

    def RefreshProject(self) -> None:
        self._SetBusy(True)
        QApplication.processEvents()
        PreviousPath = self.CurrentPath or getattr(self.TableList, "CurrentPath", "")
        self.Project = self._LoadProject()
        PreviousKey = self.TableList._PathKey(PreviousPath) if PreviousPath else ""
        PreviousStillExists = any(
            self.TableList._PathKey(str(File.Path)) == PreviousKey
            for File in self.Project.SourceFiles
        )
        Selected = set(self.Settings["TableList"].get("SelectedFiles", []))
        self.TableList.SetFiles(self.Project.SourceFiles, Selected, set(self.Settings["TableList"].get("FavoriteFiles", [])), list(self.Settings["TableList"].get("RecentFiles", [])))
        self.TableList.SetFilterMode(self.Settings["TableList"].get("FilterMode", "All"))
        self.TableList.SetSearchText(self.Settings["TableList"].get("SearchText", ""))
        if PreviousPath and self.TableList._FindRow(PreviousPath) >= 0:
            self.TableList._ActivatePath(PreviousPath)
        elif not PreviousPath and self.Project.SourceFiles:
            self.TableList._ActivatePath(str(self.Project.SourceFiles[0].Path))
        self._UpdateIssues()
        OutputDirectories = tuple(GetOutputDirectories(self.ProjectRoot, self.Settings).values())
        self.Watcher.SetRootDirectory(GetSourceDirectory(self.ProjectRoot, self.Settings), OutputDirectories)
        self.SyncLabel.setText(self.Language.Get("Synced", Time=datetime.now().strftime("%H:%M:%S")))
        self._SetBusy(False)
        if PreviousPath and not PreviousStillExists:
            self.StatusBar.SetStatus(self.Language.Get("CurrentFileRemoved"), "Error", datetime.now().strftime("%H:%M:%S"))
            self.Logs.Write("WARNING", "当前文件已删除，已切换到可用文件", FilePath=PreviousPath)

    def _SetBusy(self, IsBusy: bool) -> None:
        if hasattr(self, "RefreshListButton"):
            self.RefreshListButton.setEnabled(not IsBusy)
        if hasattr(self, "StatusBar"):
            self.StatusBar.SetStatus(self.Language.Get("Refreshing") if IsBusy else self.Language.Get("Ready"), "Busy" if IsBusy else "Idle")

    def _OnSourceChanged(self) -> None:
        self.Logs.Write("INFO", "检测到源目录变化，自动刷新")
        self.RefreshProject()

    def _OnSourcePathChanged(self, PathText: str) -> None:
        ChangedPath = Path(PathText)
        self.Logs.Write(
            "INFO",
            "源表文件变化" if ChangedPath.suffix.lower() in {".xlsx", ".xls"} else "源目录变化",
            FilePath=PathText,
            Exists=ChangedPath.exists(),
        )

    def _CurrentFileChanged(self, SourceFile: SourceFileModel | None) -> None:
        if SourceFile is None:
            self.CurrentPath = ""
            self.CurrentSheet = ""
            self.TableDetail.ClearFile()
            self.MenuBar.SetContext(False, bool(self.TableList.GetSelectedPaths()), bool(self.TableList.GetSearchText()))
            self._UpdateIssues()
            self._SaveState()
            return
        self.CurrentPath = str(SourceFile.Path)
        self.TableDetail.SetFile(SourceFile)
        self.MenuBar.SetContext(True, bool(self.TableList.GetSelectedPaths()), bool(self.TableList.GetSearchText()))
        self._SaveState()

    def _UpdateIssues(self) -> None:
        SelectedPaths = {Path(PathText) for PathText in self.TableList.GetSelectedPaths()}
        SelectedTables = ResolveLogicalTables(SelectedPaths, self.Project.LogicalTables)
        RelevantFiles = SelectedPaths | {
            SourceFile
            for Table in SelectedTables.values()
            for SourceFile in Table.SourceFiles
        }
        RelevantKeys = {NormalizePath(FilePath) for FilePath in RelevantFiles}
        Issues = [
            Issue for Issue in self.Project.Issues
            if NormalizePath(Issue.Location.FilePath) in RelevantKeys
        ]
        Issues.extend(ValidateLogicalTables(SelectedTables, self.Project.LogicalTables))
        self.IssuesPanel.SetIssues(Issues, len(SelectedPaths))
        self.MenuBar.SetContext(self.TableDetail.SourceFile is not None, bool(SelectedPaths), bool(self.TableList.GetSearchText()))

    def _Build(self) -> None:
        SelectedPaths = {Path(PathText) for PathText in self.TableList.GetSelectedPaths()}
        if not SelectedPaths:
            self._ShowInfo("Build", "NoSelection")
            return
        if not self.IssuesPanel.CanBuild():
            BlockingMessages = [
                Issue.Format()
                for Issue in self.IssuesPanel.Issues
                if Issue.Severity.value == "Error"
            ]
            if BlockingMessages:
                self.Logs.Write("WARNING", "打表阻断", Messages=BlockingMessages[:8])
                self.StatusBar.SetStatus(self.Language.Get("BuildError"), "Error", datetime.now().strftime("%H:%M:%S"))
            self.IssuesPanel.UpdateBuildState()
            return
        self.IssuesPanel.BuildButton.setEnabled(False)
        self.IssuesPanel.BuildButton.setText(self.Language.Get("Building"))
        self.StatusBar.SetStatus(self.Language.Get("Building"), "Busy", datetime.now().strftime("%H:%M:%S"))
        QApplication.processEvents()
        StartedAt = time.perf_counter()
        self.Logs.Write("INFO", "开始打表", FileCount=len(SelectedPaths))
        try:
            Result = ExportProject(self.Project, ExportRequest(SelectedPaths, self.IssuesPanel.GetTargetState()), GetOutputDirectories(self.ProjectRoot, self.Settings), self.Manifest)
        except (OSError, ValueError, TypeError, KeyError) as Error:
            Result = ExportResult(False, [], [f"导出失败：{Error}"])
        Timestamp = datetime.now().strftime("%H:%M:%S")
        ElapsedSeconds = round(time.perf_counter() - StartedAt, 2)
        if Result.Success:
            self.StatusBar.SetStatus(self.Language.Get("BuildDone"), "Success", Timestamp)
            WrittenFiles = [str(PathValue) for PathValue in Result.WrittenFiles]
            self.Logs.Write("INFO", "打表完成", Files=WrittenFiles, ElapsedSeconds=ElapsedSeconds)
            self.TableList.SetSelectedPaths(set())
            self._UpdateIssues()
            Detail = "\n".join(self.Language.LocalizeMessage(Message) for Message in Result.Messages)
            Detail += "\n" + self.Language.Get("GeneratedFiles", Files="\n".join(WrittenFiles))
            Detail += "\n" + self.Language.Get("Elapsed", Seconds=ElapsedSeconds)
            self._ShowInfo(
                "Build",
                "ExportSucceeded",
                Detail,
            )
        else:
            self.StatusBar.SetStatus(self.Language.Get("BuildError"), "Error", Timestamp)
            self.Logs.Write("ERROR", "打表失败", Messages=Result.Messages, ElapsedSeconds=ElapsedSeconds)
            self._ShowInfo(
                "Build",
                "ExportFailed",
                "\n".join(self.Language.LocalizeMessage(Message) for Message in Result.Messages[:8]),
                Warning=True,
            )
        self.IssuesPanel.BuildButton.setText(self.Language.Get("Build"))
        self.IssuesPanel.UpdateBuildState()
        self._SaveState()

    def _ShowInfo(self, TitleKey: str, MessageKey: str, Detail: str = "", Warning: bool = False) -> None:
        Message = self.Language.Get(MessageKey)
        if Detail:
            Message += "\n" + Detail
        (QMessageBox.warning if Warning else QMessageBox.information)(self, self.Language.Get(TitleKey), Message)

    def _SelectAllFiles(self) -> None:
        self.TableList.SetSelectedPaths({str(File.Path) for File in self.Project.SourceFiles})
        self.Logs.Write("INFO", "已全选源文件")

    def _ClearSelection(self) -> None:
        self.TableList.SetSelectedPaths(set())
        self.Logs.Write("INFO", "已清空文件勾选")

    def _ClearSearch(self) -> None:
        self.TableList.ClearSearch()
        self.Logs.Write("INFO", "已清空搜索")

    def _FavoriteCurrent(self) -> None:
        IsFavorite = self.CurrentPath not in self.TableList.GetFavorites()
        self.TableList.FavoriteCurrent(IsFavorite)
        self.Logs.Write("INFO", "已更新当前文件收藏", Favorite=IsFavorite, FilePath=self.CurrentPath)

    def _ClearFavorites(self) -> None:
        self.TableList.ClearFavorites()
        self.Logs.Write("INFO", "已取消全部收藏")

    def _OpenFilePath(self, PathText: str) -> None:
        FilePath = Path(PathText)
        self.Logs.Write("INFO", "打开文件位置", FilePath=PathText)
        if not FilePath.exists():
            self.StatusBar.SetStatus(self.Language.Get("FileMissing"), "Error", datetime.now().strftime("%H:%M:%S"))
            QDesktopServices.openUrl(QUrl.fromLocalFile(str(FilePath.parent)))
            return
        if os.name == "nt":
            subprocess.Popen(["explorer", "/select,", str(FilePath)])
        else:
            QDesktopServices.openUrl(QUrl.fromLocalFile(str(FilePath.parent)))

    def _OpenSourceDirectory(self) -> None:
        Directory = GetSourceDirectory(self.ProjectRoot, self.Settings)
        Directory.mkdir(parents=True, exist_ok=True)
        self.Logs.Write("INFO", "打开配置表目录", Directory=str(Directory))
        QDesktopServices.openUrl(QUrl.fromLocalFile(str(Directory)))

    def _OpenExcel(self) -> None:
        if self.TableDetail.SourceFile is not None:
            FilePath = self.TableDetail.SourceFile.Path
            self.Logs.Write("INFO", "在 Excel 中打开当前文件", FilePath=str(FilePath))
            if not FilePath.exists():
                self.StatusBar.SetStatus(self.Language.Get("FileMissing"), "Error", datetime.now().strftime("%H:%M:%S"))
                QDesktopServices.openUrl(QUrl.fromLocalFile(str(FilePath.parent)))
                return
            QDesktopServices.openUrl(QUrl.fromLocalFile(str(FilePath)))

    def _OpenDocument(self, RelativePath: str) -> None:
        DocumentPath = self.ProjectRoot / RelativePath
        if DocumentPath.exists():
            QDesktopServices.openUrl(QUrl.fromLocalFile(str(DocumentPath)))
        else:
            QMessageBox.warning(
                self,
                self.Language.Get("DocsMenu"),
                self.Language.Get("DocumentMissing", RelativePath),
            )

    def _OpenSettings(self) -> None:
        self.CurrentPage = "Settings"
        self.SettingsPreviewSnapshot = (
            str(self.Settings["Interface"].get("ThemeMode", "Night")),
            str(self.Settings["Interface"].get("AccentColor", "#4D8DF7")),
        )
        self.SettingsPage.SetSettings(self.Settings)
        self.WorkspaceStack.setCurrentWidget(self.SettingsPage)
        self.ToolButtons[0].setChecked(False)

    def _ShowConfigPage(self) -> None:
        if self.CurrentPage == "Settings" and self.SettingsPreviewSnapshot is not None:
            ThemeMode, AccentColor = self.SettingsPreviewSnapshot
            ApplyTheme(QApplication.instance(), ThemeMode, self.ResourcesDirectory, AccentColor)
            self.MenuBar.SetThemeMode(ThemeMode)
            self.SettingsPreviewSnapshot = None
        self.CurrentPage = "Config"
        self.WorkspaceStack.setCurrentWidget(self.ConfigPage)
        self.ToolButtons[0].setChecked(True)

    def _PreviewSettingsTheme(self, Mode: str) -> None:
        AccentColor = str(self.SettingsPage.Settings["Interface"].get("AccentColor", "#4D8DF7"))
        ApplyTheme(QApplication.instance(), Mode, self.ResourcesDirectory, AccentColor)
        self.MenuBar.SetThemeMode(Mode)

    def _PreviewSettingsAccent(self, AccentColor: str) -> None:
        Mode = str(self.SettingsPage.Settings["Interface"].get("ThemeMode", "Night"))
        ApplyTheme(QApplication.instance(), Mode, self.ResourcesDirectory, AccentColor)

    def _SaveSettingsPage(self, Settings: dict) -> None:
        LanguageChanged = Settings["Interface"].get("Language", self.Language.CurrentCode) != self.Language.CurrentCode
        LogVisible = self.LogPanel.isVisible() if hasattr(self, "LogPanel") else False
        PreviousPath = self.CurrentPath
        PreviousSheet = self.CurrentSheet or getattr(self.TableDetail, "CurrentSheetName", "")
        self.Settings = Settings
        SaveSettings(self.ProjectRoot, self.Settings)
        self.SettingsPreviewSnapshot = None
        ApplyTheme(QApplication.instance(), self.Settings["Interface"]["ThemeMode"], self.ResourcesDirectory, self.Settings["Interface"].get("AccentColor", "#4D8DF7"))
        self.MenuBar.SetThemeMode(self.Settings["Interface"].get("ThemeMode", "Night"))
        self.MenuBar.SetLanguageCode(self.Settings["Interface"].get("Language", self.Language.CurrentCode))
        if LanguageChanged:
            self.Language.Load(self.Settings["Interface"]["Language"])
            QApplication.instance().setProperty("TableToolsLanguage", self.Language.CurrentCode)
            self._BuildUi()
            self.CurrentPath = PreviousPath
            self.CurrentSheet = PreviousSheet
            self.TableDetail.CurrentSheetName = PreviousSheet
            self._SetLogVisible(LogVisible)
        else:
            self.SettingsPage.SetSettings(self.Settings)
        self._ShowConfigPage()
        self.RefreshProject()

    def _ChangeTheme(self, Mode: str) -> None:
        self.Settings["Interface"]["ThemeMode"] = Mode
        ApplyTheme(QApplication.instance(), Mode, self.ResourcesDirectory, self.Settings["Interface"].get("AccentColor", "#4D8DF7"))
        self.MenuBar.SetThemeMode(Mode)
        if self.CurrentPage == "Settings" and self.SettingsPreviewSnapshot is not None:
            self.SettingsPage.Settings["Interface"]["ThemeMode"] = Mode
            for Name, Button in self.SettingsPage.ThemeButtons.items():
                Button.setChecked(Name == Mode)
            self.SettingsPreviewSnapshot = (
                Mode,
                str(self.Settings["Interface"].get("AccentColor", "#4D8DF7")),
            )
        self._SaveState()

    def _ChangeLanguage(self, LanguageCode: str) -> None:
        self.Settings["Interface"]["Language"] = LanguageCode
        self._SaveState()
        self.Language.Load(LanguageCode)
        QApplication.instance().setProperty("TableToolsLanguage", self.Language.CurrentCode)
        Page = self.CurrentPage
        LogVisible = self.LogPanel.isVisible() if hasattr(self, "LogPanel") else False
        PreviousPath = self.CurrentPath
        PreviousSheet = self.CurrentSheet or getattr(self.TableDetail, "CurrentSheetName", "")
        self._BuildUi()
        self.CurrentPath = PreviousPath
        self.CurrentSheet = PreviousSheet
        self.TableDetail.CurrentSheetName = PreviousSheet
        self._SetLogVisible(LogVisible)
        self.RefreshProject()
        if Page == "Settings":
            self._OpenSettings()

    def _ToggleSidebar(self) -> None:
        IsVisible = not self.ToolRail.isVisible()
        self.ToolRail.setVisible(IsVisible)
        self.Settings["Interface"]["SidebarExpanded"] = IsVisible
        self.MenuBar.SetSidebarVisible(IsVisible)
        self._SaveState()

    def _SetLogVisible(self, Visible: bool) -> None:
        self.LogPanel.setVisible(Visible)
        Action = self.MenuBar.Actions.get("ToggleLog")
        if Action is not None:
            Action.setChecked(Visible)

    def _ToggleMaximized(self) -> None:
        self.showNormal() if self.isMaximized() else self.showMaximized()
        self._UpdateMaximizeButton()

    def _UpdateMaximizeButton(self) -> None:
        if not hasattr(self, "MaximizeButton"):
            return
        TooltipKey = "Restore" if self.isMaximized() else "Maximize"
        self.MaximizeButton.setToolTip(self.Language.Get(TooltipKey))
        self.MaximizeButton.setAccessibleName(self.Language.Get(TooltipKey))

    def _SaveState(self) -> None:
        if not hasattr(self, "TableList"):
            return
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
        self.Logs.Write("INFO", "应用关闭")
        self._SaveState()
        Event.accept()
