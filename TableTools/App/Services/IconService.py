# 用途：按语义名称加载本地图标，并在资源缺失时提供可记录的备用图标。
# 最近修改日期：2026-10-07
# 作者：Codex

from pathlib import Path
from typing import Callable

from PySide6.QtGui import QIcon, QPixmap


class IconService:
    """Keep semantic icon names out of UI layout code."""

    Aliases = {
        "Sidebar": "SidebarToggle.svg",
        "External": "ExternalLink.svg",
        "ExternalFile": "ExternalLinkOpen.svg",
        "Excel": "ExternalLinkExcel.svg",
        "OpenExcel": "ExternalLinkExcel.svg",
        "Favorite": "Star.svg",
        "Star": "Star.svg",
        "StarFilled": "StarFilled.svg",
        "File": "File.svg",
        "Folder": "ToolbarIcon12.svg",
        "Search": "Search.svg",
        "Filter": "Filter.svg",
        "Clear": "Clear.svg",
        # 这些资源来自 Figma 侧栏导出，保持五个工具入口的笔画和尺寸一致。
        # ToolbarIcon06 is the Figma navigation glyph; Table.svg is used for file/detail content.
        "TableTool": "ToolbarIcon06.svg",
        "Table": "Table.svg",
        "Alert": "TriangleAlert.svg",
        "AlertAlt": "TriangleAlertAlt.svg",
        "RefreshList": "ToolbarIcon11.svg",
        "RefreshContent": "ToolbarIcon11.svg",
        "Refresh": "ToolbarIcon11.svg",
        "OpenSourceDirectory": "ToolbarIcon12.svg",
        "OpenInExcel": "ExternalLinkExcel.svg",
        "OpenFilePath": "ExternalLink.svg",
        "Settings": "Settings.svg",
        "ClearSelection": "Clear.svg",
        "ClearSearch": "Clear.svg",
        "FavoriteCurrent": "Star.svg",
        "ClearFavorites": "Trash.svg",
        "Trash": "Trash.svg",
        "Save": "Save.svg",
        "Cancel": "Cancel.svg",
        "Docs": "Docs.svg",
        "ToggleSidebar": "SidebarToggle.svg",
        "ToggleLog": "Log.svg",
        "Readme": "Docs.svg",
        "TableRules": "Docs.svg",
        "TableContentRules": "Docs.svg",
        "SelectAll": "SelectAll.svg",
        "Build": "Build.svg",
        "Error": "Error.svg",
        "Warning": "Warning.svg",
        "Info": "Info.svg",
        "Success": "Success.svg",
        "Failure": "Error.svg",
        "Loading": "Refresh.svg",
        "Sync": "Refresh.svg",
        "FileMissing": "Warning.svg",
        "Log": "Log.svg",
        "CopyLogTooltip": "Copy.svg",
        "ClearLogTooltip": "Trash.svg",
        "OpenLogTooltip": "ExternalLink.svg",
        "Day": "Sun.svg",
        "Night": "Moon.svg",
        "System": "Monitor.svg",
        "Sun": "Sun.svg",
        "Moon": "Moon.svg",
        "Monitor": "Monitor.svg",
        "FolderKanban": "ToolbarIcon07.svg",
        "Images": "ToolbarIcon08.svg",
        "AudioLines": "ToolbarIcon09.svg",
        "NotebookText": "ToolbarIcon10.svg",
        "CollapseLogTooltip": "ChevronDown.svg",
        "Language": "Language.svg",
        "SimplifiedChinese": "LanguageCheck.svg",
        "English": "LanguageCheck.svg",
        "Exit": "WindowClose.svg",
        "Minimize": "WindowMinimize.svg",
        "Maximize": "WindowMaximize.svg",
        "Close": "WindowClose.svg",
    }

    def __init__(self, IconsDirectory: Path, LogCallback: Callable[[str], None] | None = None) -> None:
        self.IconsDirectory = IconsDirectory
        self.LogCallback = LogCallback

    def PathFor(self, Name: str) -> Path:
        FileName = self.Aliases.get(Name, Name)
        if not FileName.lower().endswith(".svg"):
            FileName += ".svg"
        PathValue = self.IconsDirectory / FileName
        if PathValue.exists():
            return PathValue
        Fallback = self.IconsDirectory / "AppIcon.svg"
        if self.LogCallback is not None:
            self.LogCallback(f"缺少图标资源：{FileName}")
        return Fallback

    def Get(self, Name: str) -> QIcon:
        return QIcon(str(self.PathFor(Name)))

    def Pixmap(self, Name: str, Size: int = 20) -> QPixmap:
        return self.Get(Name).pixmap(Size, Size)
