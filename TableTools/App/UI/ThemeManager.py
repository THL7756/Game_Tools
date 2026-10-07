import os
from pathlib import Path

from PySide6.QtGui import QColor, QFont, QFontDatabase, QPalette
from PySide6.QtWidgets import QApplication


def ApplyTheme(Application: QApplication, ThemeMode: str, ResourcesDirectory: Path) -> None:
    LoadSystemFonts()
    EffectiveMode = ResolveThemeMode(ThemeMode)
    StylePath = ResourcesDirectory / "Styles" / f"{EffectiveMode}.qss"
    Application.setStyleSheet(StylePath.read_text(encoding="utf-8"))
    Palette = QPalette()
    if EffectiveMode == "Dark":
        Palette.setColor(QPalette.ColorRole.Window, QColor("#17191D"))
        Palette.setColor(QPalette.ColorRole.WindowText, QColor("#E2E6ED"))
        Palette.setColor(QPalette.ColorRole.Base, QColor("#20242A"))
        Palette.setColor(QPalette.ColorRole.Text, QColor("#E2E6ED"))
        Palette.setColor(QPalette.ColorRole.Highlight, QColor("#4D8DF7"))
    else:
        Palette.setColor(QPalette.ColorRole.Window, QColor("#F4F6F8"))
        Palette.setColor(QPalette.ColorRole.WindowText, QColor("#20242A"))
        Palette.setColor(QPalette.ColorRole.Base, QColor("#FFFFFF"))
        Palette.setColor(QPalette.ColorRole.Text, QColor("#20242A"))
        Palette.setColor(QPalette.ColorRole.Highlight, QColor("#3278E8"))
    Application.setPalette(Palette)
    ApplyFonts(Application)


def ResolveThemeMode(ThemeMode: str) -> str:
    if ThemeMode == "System":
        return "Dark"
    return "Light" if ThemeMode == "Day" else "Dark"


def ApplyFonts(Application: QApplication) -> None:
    Families = set(QFontDatabase.families())
    UiFamily = "Microsoft YaHei UI" if "Microsoft YaHei UI" in Families else "Noto Sans SC"
    Font = QFont(UiFamily)
    Font.setPointSize(10)
    Application.setFont(Font)


def LoadSystemFonts() -> None:
    WindowsFonts = Path(os.environ.get("WINDIR", "C:/Windows")) / "Fonts"
    for FontName in ("msyh.ttc", "NotoSansSC-VF.ttf", "CascadiaMono.ttf"):
        FontPath = WindowsFonts / FontName
        if FontPath.exists():
            QFontDatabase.addApplicationFont(str(FontPath))
