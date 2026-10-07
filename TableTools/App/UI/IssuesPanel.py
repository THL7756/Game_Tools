from pathlib import Path

from PySide6.QtCore import Qt, Signal
from PySide6.QtGui import QColor, QIcon, QTextCharFormat
from PySide6.QtWidgets import (
    QCheckBox,
    QHBoxLayout,
    QLabel,
    QPushButton,
    QTextEdit,
    QVBoxLayout,
    QWidget,
)

from App.Core.Models import ExportTargetState, SeverityLevel, ValidationIssue
from App.Services.LanguageService import LanguageService


class IssuesPanel(QWidget):
    BuildRequested = Signal()

    def __init__(self, Language: LanguageService, IconsDirectory: Path) -> None:
        super().__init__()
        self.Language = Language
        self.IconsDirectory = IconsDirectory
        self.setObjectName("IssuesPanel")
        self.setFixedHeight(250)
        Root = QVBoxLayout(self)
        Root.setContentsMargins(12, 10, 12, 10)
        Root.setSpacing(8)

        Header = QHBoxLayout()
        self.TitleLabel = QLabel()
        self.TitleLabel.setObjectName("TableTitle")
        self.TitleLabel.setStyleSheet("font-size: 14px;")
        Header.addWidget(self.TitleLabel)
        self.ScopeLabel = QLabel()
        self.ScopeLabel.setObjectName("Muted")
        Header.addWidget(self.ScopeLabel)
        Header.addStretch(1)
        Root.addLayout(Header)

        self.IssuesView = QTextEdit()
        self.IssuesView.setReadOnly(True)
        self.IssuesView.setAcceptRichText(False)
        self.IssuesView.setVerticalScrollBarPolicy(Qt.ScrollBarPolicy.ScrollBarAsNeeded)
        self.IssuesView.setStyleSheet("background: transparent; border: 0; color: #E2E6ED;")
        Root.addWidget(self.IssuesView, 1)

        Export = QWidget()
        Export.setObjectName("ExportBar")
        ExportLayout = QHBoxLayout(Export)
        ExportLayout.setContentsMargins(12, 9, 12, 9)
        ExportLayout.setSpacing(16)
        self.OutputLabel = QLabel()
        ExportLayout.addWidget(self.OutputLabel)
        self.ClientDataCheck = QCheckBox()
        self.ClientCodeCheck = QCheckBox()
        self.ServerDataCheck = QCheckBox()
        self.ServerCodeCheck = QCheckBox()
        self.ServerCodeCheck.setEnabled(False)
        ExportLayout.addWidget(self.ClientDataCheck)
        ExportLayout.addWidget(self.ClientCodeCheck)
        ExportLayout.addWidget(self.ServerDataCheck)
        ExportLayout.addWidget(self.ServerCodeCheck)
        ExportLayout.addStretch(1)
        self.BuildButton = QPushButton()
        self.BuildButton.setObjectName("Primary")
        self.BuildButton.setIcon(QIcon(str(IconsDirectory / "Play.svg")))
        self.BuildButton.clicked.connect(self.BuildRequested.emit)
        ExportLayout.addWidget(self.BuildButton)
        Root.addWidget(Export)
        self.UpdateTexts()

    def UpdateTexts(self) -> None:
        self.OutputLabel.setText(self.Language.Get("OutputScope"))
        self.ClientDataCheck.setText(self.Language.Get("ClientData"))
        self.ClientCodeCheck.setText(self.Language.Get("ClientCode"))
        self.ServerDataCheck.setText(self.Language.Get("ServerData"))
        self.ServerCodeCheck.setText(self.Language.Get("ServerCode"))
        self.ServerCodeCheck.setToolTip(self.Language.Get("ServerCodeTodo"))
        self.BuildButton.setText(self.Language.Get("Build"))
        self.BuildButton.setToolTip(self.Language.Get("BuildTooltip"))

    def SetTargetState(self, TargetState: ExportTargetState) -> None:
        self.ClientDataCheck.setChecked(TargetState.ClientData)
        self.ClientCodeCheck.setChecked(TargetState.ClientCode)
        self.ServerDataCheck.setChecked(TargetState.ServerData)
        self.ServerCodeCheck.setChecked(False)

    def GetTargetState(self) -> ExportTargetState:
        return ExportTargetState(
            self.ClientDataCheck.isChecked(),
            self.ClientCodeCheck.isChecked(),
            self.ServerDataCheck.isChecked(),
            False,
        )

    def SetIssues(self, Issues: list[ValidationIssue], SelectedCount: int) -> None:
        ErrorCount = sum(Issue.Severity == SeverityLevel.Error for Issue in Issues)
        WarningCount = sum(Issue.Severity == SeverityLevel.Warning for Issue in Issues)
        self.TitleLabel.setText(
            f"{self.Language.Get('TableIssues')}  "
            f"{self.Language.Get('ErrorCount', Count=ErrorCount)} / "
            f"{self.Language.Get('WarningCount', Count=WarningCount)}"
        )
        self.ScopeLabel.setText(self.Language.Get("IssueScope", Count=SelectedCount))
        self.IssuesView.clear()
        Cursor = self.IssuesView.textCursor()
        if not Issues:
            Format = QTextCharFormat()
            Format.setForeground(QColor("#667181"))
            Cursor.insertText("0", Format)
            return

        for Index, Issue in enumerate(Issues):
            Format = QTextCharFormat()
            IsError = Issue.Severity == SeverityLevel.Error
            Format.setForeground(QColor("#FFB7BD" if IsError else "#F5D98B"))
            Prefix = "[ERROR] " if IsError else "[WARN]  "
            Cursor.insertText(Prefix + Issue.Format(), Format)
            if Index < len(Issues) - 1:
                Cursor.insertBlock()
