# 用途：初始化 Qt 应用、加载设置并启动 TableTools 主窗口。
# 最近修改日期：2026-10-07
# 作者：Codex

import sys
from pathlib import Path

from PySide6.QtCore import Qt
from PySide6.QtWidgets import QApplication

from App.Services.SettingsService import LoadSettings
from App.UI.MainWindow import MainWindow
from App.UI.ThemeManager import ApplyTheme


def RunApplication() -> int:
    QApplication.setHighDpiScaleFactorRoundingPolicy(
        Qt.HighDpiScaleFactorRoundingPolicy.PassThrough
    )
    Application = QApplication(sys.argv)
    ProjectRoot = Path(__file__).resolve().parents[1]
    Settings = LoadSettings(ProjectRoot)
    Application.setProperty("TableToolsLanguage", Settings["Interface"]["Language"])
    ApplyTheme(
        Application,
        Settings["Interface"]["ThemeMode"],
        ProjectRoot / "App" / "Resources",
        Settings["Interface"].get("AccentColor", "#4D8DF7"),
    )
    Window = MainWindow(ProjectRoot)
    Window.show()
    Window.raise_()
    Window.activateWindow()
    return Application.exec()
