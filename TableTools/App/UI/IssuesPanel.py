# 用途：展示选中表格的错误与警告，并提供导出目标和打表操作。
# 最近修改日期：2026-10-07
# 作者：Codex

from pathlib import Path

from PySide6.QtCore import Qt, Signal
from PySide6.QtGui import QIcon
from PySide6.QtWidgets import (
    QCheckBox,
    QHBoxLayout,
    QLabel,
    QPushButton,
    QScrollArea,
    QVBoxLayout,
    QWidget,
)

from App.Core.Models import ExportTargetState, SeverityLevel, ValidationIssue
from App.Services.IconService import IconService
from App.Services.LanguageService import LanguageService


class IssuesPanel(QWidget):
    BuildRequested = Signal()
    TargetChanged = Signal(object)

    def __init__(self, Language: LanguageService, IconsDirectory: Path) -> None:
        super().__init__()
        self.Language = Language
        self.IconsDirectory = IconsDirectory
        self.Icons = IconService(IconsDirectory)
        self.SelectedCount = 0
        self.Issues: list[ValidationIssue] = []
        self.setObjectName("IssuesPanel")
        self.setFixedHeight(250)
        Root = QVBoxLayout(self)
        Root.setContentsMargins(12, 10, 12, 10)
        Root.setSpacing(8)

        Header = QHBoxLayout()
        self.TitleLabel = QLabel()
        self.TitleLabel.setObjectName("PanelTitle")
        Header.addWidget(self.TitleLabel)
        self.ScopeLabel = QLabel()
        self.ScopeLabel.setObjectName("Muted")
        Header.addWidget(self.ScopeLabel)
        Header.addStretch(1)
        Root.addLayout(Header)

        self.IssuesScroll = QScrollArea()
        self.IssuesScroll.setWidgetResizable(True)
        self.IssuesScroll.setHorizontalScrollBarPolicy(Qt.ScrollBarPolicy.ScrollBarAlwaysOff)
        self.IssuesContent = QWidget()
        self.IssuesLayout = QVBoxLayout(self.IssuesContent)
        self.IssuesLayout.setContentsMargins(0, 0, 0, 0)
        self.IssuesLayout.setSpacing(6)
        self.IssuesLayout.addStretch(1)
        self.IssuesScroll.setWidget(self.IssuesContent)
        Root.addWidget(self.IssuesScroll, 1)

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
        for Check in (self.ClientDataCheck, self.ClientCodeCheck, self.ServerDataCheck):
            Check.stateChanged.connect(self._TargetChanged)
        ExportLayout.addWidget(self.ClientDataCheck)
        ExportLayout.addWidget(self.ClientCodeCheck)
        ExportLayout.addWidget(self.ServerDataCheck)
        ExportLayout.addWidget(self.ServerCodeCheck)
        ExportLayout.addStretch(1)
        self.BuildButton = QPushButton()
        self.BuildButton.setObjectName("Primary")
        self.BuildButton.setIcon(self.Icons.Get("Build"))
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
        self.ClientDataCheck.setToolTip(self.Language.Get("ClientDataTooltip"))
        self.ClientCodeCheck.setToolTip(self.Language.Get("ClientCodeTooltip"))
        self.ServerDataCheck.setToolTip(self.Language.Get("ServerDataTooltip"))
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

    def _TargetChanged(self) -> None:
        TargetState = self.GetTargetState()
        self._UpdateBuildEnabled(TargetState)
        self.TargetChanged.emit(TargetState)

    def SetIssues(self, Issues: list[ValidationIssue], SelectedCount: int) -> None:
        self.Issues = list(Issues)
        self.SelectedCount = SelectedCount
        ErrorCount = sum(Issue.Severity == SeverityLevel.Error for Issue in Issues)
        WarningCount = sum(Issue.Severity == SeverityLevel.Warning for Issue in Issues)
        self.TitleLabel.setText(
            f"{self.Language.Get('TableIssues')}  "
            f"{self.Language.Get('ErrorCount', Count=ErrorCount)} / "
            f"{self.Language.Get('WarningCount', Count=WarningCount)}"
        )
        self.ScopeLabel.setText(self.Language.Get("IssueScope", Count=SelectedCount))
        self._UpdateBuildEnabled(self.GetTargetState())
        while self.IssuesLayout.count() > 1:
            Item = self.IssuesLayout.takeAt(0)
            if Item.widget() is not None:
                Item.widget().deleteLater()
        if not Issues:
            EmptyLabel = QLabel(self.Language.Get("NoIssues"))
            EmptyLabel.setObjectName("IssueEmpty")
            self.IssuesLayout.insertWidget(0, EmptyLabel)
            return
        for Issue in Issues:
            IsError = Issue.Severity == SeverityLevel.Error
            if not IsError:
                Prefix = self.Language.Get("WarningPrefix")
            elif "schema" in Issue.Message.lower():
                Prefix = self.Language.Get("SchemaErrorPrefix")
            elif Issue.Location.Row > 0:
                Prefix = self.Language.Get("DataErrorPrefix")
            else:
                Prefix = self.Language.Get("ErrorPrefix")
            Card = QWidget()
            Card.setObjectName("IssueError" if IsError else "IssueWarning")
            CardLayout = QHBoxLayout(Card)
            CardLayout.setContentsMargins(8, 6, 8, 6)
            Icon = QLabel()
            Icon.setPixmap(self.Icons.Pixmap("Error" if IsError else "Warning", 18))
            CardLayout.addWidget(Icon)
            Text = QLabel(Prefix + self.Language.FormatIssue(Issue))
            Text.setWordWrap(True)
            Text.setTextInteractionFlags(Qt.TextInteractionFlag.TextSelectableByMouse)
            CardLayout.addWidget(Text, 1)
            self.IssuesLayout.insertWidget(self.IssuesLayout.count() - 1, Card)

    def _UpdateBuildEnabled(self, TargetState: ExportTargetState) -> None:
        HasTarget = TargetState.ClientData or TargetState.ClientCode or TargetState.ServerData
        HasBlockingError = any(Issue.Severity == SeverityLevel.Error for Issue in self.Issues)
        IsEnabled = self.SelectedCount > 0 and HasTarget and not HasBlockingError
        self.BuildButton.setEnabled(IsEnabled)
        if IsEnabled:
            TooltipKey = "BuildTooltip"
        elif any(Issue.Severity == SeverityLevel.Error for Issue in self.Issues):
            TooltipKey = "BuildBlockedByIssues"
        else:
            TooltipKey = "BuildDisabledTooltip"
        self.BuildButton.setToolTip(self.Language.Get(TooltipKey))

    def CanBuild(self) -> bool:
        """Return the same decision used by the button, so command handlers cannot bypass it."""
        TargetState = self.GetTargetState()
        return (
            self.SelectedCount > 0
            and (TargetState.ClientData or TargetState.ClientCode or TargetState.ServerData)
            and not any(Issue.Severity == SeverityLevel.Error for Issue in self.Issues)
        )

    def UpdateBuildState(self) -> None:
        """Refresh the command state after an external refresh or export finishes."""
        self._UpdateBuildEnabled(self.GetTargetState())
