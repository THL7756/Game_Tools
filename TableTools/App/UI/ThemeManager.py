# 用途：应用深色、浅色和跟随系统主题，并统一界面字体与强调色。
# 最近修改日期：2026-10-07
# 作者：Codex

import os
from pathlib import Path

from PySide6.QtGui import QColor, QFont, QFontDatabase, QPalette
from PySide6.QtWidgets import QApplication


def ApplyTheme(
    Application: QApplication,
    ThemeMode: str,
    ResourcesDirectory: Path,
    AccentColor: str = "#4D8DF7",
) -> None:
    LoadSystemFonts()
    EffectiveMode = ResolveThemeMode(ThemeMode)
    StylePath = ResourcesDirectory / "Styles" / f"{EffectiveMode}.qss"
    StyleText = StylePath.read_text(encoding="utf-8")
    Accent = QColor(AccentColor)
    if not Accent.isValid():
        Accent = QColor("#4D8DF7")
    AccentBase = Accent.name().upper()
    AccentHover = Accent.lighter(110).name().upper()
    AccentPressed = Accent.darker(110).name().upper()
    AccentText = (
        Accent.lighter(125).name().upper()
        if EffectiveMode == "Dark"
        else Accent.darker(125).name().upper()
    )
    AccentSoft = f"rgba({Accent.red()}, {Accent.green()}, {Accent.blue()}, {42 if EffectiveMode == 'Dark' else 34})"
    StyleText = (
        StyleText
        .replace("#4D8DF7", AccentBase)
        .replace("#3278E8", AccentBase)
        .replace("#619BF8", AccentHover)
        .replace("#4387F0", AccentHover)
        .replace("#3D7DE8", AccentPressed)
        .replace("#72A7FF", AccentText)
        .replace("#2464C1", AccentText)
        .replace("#22334E", AccentSoft)
        .replace("#DCE8F8", AccentSoft)
    )
    Application.setStyleSheet(StyleText)
    Palette = QPalette()
    if EffectiveMode == "Dark":
        Palette.setColor(QPalette.ColorRole.Window, QColor("#17191D"))
        Palette.setColor(QPalette.ColorRole.WindowText, QColor("#E2E6ED"))
        Palette.setColor(QPalette.ColorRole.Base, QColor("#20242A"))
        Palette.setColor(QPalette.ColorRole.Text, QColor("#E2E6ED"))
        Palette.setColor(QPalette.ColorRole.Highlight, Accent)
    else:
        Palette.setColor(QPalette.ColorRole.Window, QColor("#F4F6F8"))
        Palette.setColor(QPalette.ColorRole.WindowText, QColor("#20242A"))
        Palette.setColor(QPalette.ColorRole.Base, QColor("#FFFFFF"))
        Palette.setColor(QPalette.ColorRole.Text, QColor("#20242A"))
        Palette.setColor(QPalette.ColorRole.Highlight, Accent)
    Application.setPalette(Palette)
    ApplyFonts(Application)


def ResolveThemeMode(ThemeMode: str) -> str:
    if ThemeMode == "System":
        Application = QApplication.instance()
        if Application is not None:
            try:
                Scheme = Application.styleHints().colorScheme()
                if str(Scheme).lower().endswith("light"):
                    return "Light"
            except AttributeError:
                pass
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
