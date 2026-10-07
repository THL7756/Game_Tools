# 用途：显示当前工程路径、运行状态、日志入口、时间和版本信息。
# 最近修改日期：2026-10-07
# 作者：Codex

from PySide6.QtCore import Signal
from PySide6.QtGui import QIcon
from PySide6.QtWidgets import QHBoxLayout, QLabel, QSizeGrip, QToolButton, QWidget

from App.Services.IconService import IconService


class StatusPanel(QWidget):
    LogRequested = Signal()

    def __init__(self, PathText: str, VersionText: str, Icons: IconService | None = None, Parent=None) -> None:
        super().__init__(Parent)
        self.Icons = Icons
        self.setObjectName("StatusBar")
        self.setFixedHeight(24)
        Layout = QHBoxLayout(self)
        Layout.setContentsMargins(16, 0, 8, 0)
        Layout.setSpacing(8)
        self.PathLabel = QLabel(PathText)
        self.PathLabel.setObjectName("Muted")
        Layout.addWidget(self.PathLabel)
        Layout.addStretch(1)
        self.LogButton = QToolButton()
        self.LogButton.setObjectName("StatusLogButton")
        self.LogButton.setIcon(QIcon())
        self.LogButton.setText("LOG")
        self.LogButton.setToolTip("Open log")
        self.LogButton.clicked.connect(self.LogRequested.emit)
        Layout.addWidget(self.LogButton)
        self.StatusIcon = QLabel()
        self.StatusIcon.setFixedSize(16, 16)
        Layout.addWidget(self.StatusIcon)
        self.StatusLabel = QLabel("IDLE")
        self.StatusLabel.setObjectName("StatusIdle")
        self.StatusLabel.setMinimumWidth(52)
        Layout.addWidget(self.StatusLabel)
        self.TimeLabel = QLabel("--:--:--")
        self.TimeLabel.setObjectName("Subtle")
        Layout.addWidget(self.TimeLabel)
        self.VersionLabel = QLabel(VersionText)
        self.VersionLabel.setObjectName("Muted")
        Layout.addWidget(self.VersionLabel)
        Layout.addWidget(QSizeGrip(self))

    def SetStatus(self, Text: str, State: str = "Idle", TimeText: str = "--:--:--") -> None:
        self.StatusLabel.setText(Text)
        self.StatusLabel.setObjectName(f"Status{State}")
        self.StatusLabel.style().unpolish(self.StatusLabel)
        self.StatusLabel.style().polish(self.StatusLabel)
        self.TimeLabel.setText(TimeText)
        if self.Icons is not None:
            IconName = {"Idle": "Info", "Busy": "Loading", "Success": "Success", "Error": "Error"}.get(State, "Info")
            self.StatusIcon.setPixmap(self.Icons.Pixmap(IconName, 14))
