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
    ApplyTheme(Application, Settings["Interface"]["ThemeMode"], ProjectRoot / "App" / "Resources")
    Window = MainWindow(ProjectRoot)
    Window.show()
    return Application.exec()
