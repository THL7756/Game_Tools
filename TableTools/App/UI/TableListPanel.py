from pathlib import Path

from PySide6.QtCore import Qt, Signal
from PySide6.QtGui import QIcon
from PySide6.QtWidgets import (
    QAbstractItemView,
    QButtonGroup,
    QCheckBox,
    QHBoxLayout,
    QLabel,
    QLineEdit,
    QListWidget,
    QListWidgetItem,
    QPushButton,
    QSizePolicy,
    QToolButton,
    QVBoxLayout,
    QWidget,
)

from App.Core.Models import SourceFileModel
from App.Services.LanguageService import LanguageService


class FileRowWidget(QWidget):
    CheckChanged = Signal(str, bool)
    OpenPathRequested = Signal(str)
    FavoriteChanged = Signal(str, bool)

    def __init__(
        self,
        SourceFile: SourceFileModel,
        Language: LanguageService,
        IsSelected: bool,
        IsFavorite: bool,
        IconsDirectory: Path,
    ) -> None:
        super().__init__()
        self.FilePath = str(SourceFile.Path)
        self.Language = Language
        self.IconsDirectory = IconsDirectory
        Layout = QHBoxLayout(self)
        Layout.setContentsMargins(8, 5, 7, 5)
        Layout.setSpacing(7)
        self.Check = QCheckBox(SourceFile.Path.name)
        self.Check.setChecked(IsSelected)
        self.Check.toggled.connect(lambda Checked: self.CheckChanged.emit(self.FilePath, Checked))
        Layout.addWidget(self.Check, 1)
        self.OpenButton = QToolButton()
        self.OpenButton.setIcon(QIcon(str(IconsDirectory / "ExternalLink.svg")))
        self.OpenButton.setFixedSize(28, 28)
        self.OpenButton.setToolTip(Language.Get("OpenFilePath"))
        self.OpenButton.clicked.connect(lambda: self.OpenPathRequested.emit(self.FilePath))
        Layout.addWidget(self.OpenButton)
        self.FavoriteButton = QToolButton()
        self.FavoriteButton.setIcon(QIcon(str(IconsDirectory / "Star.svg")))
        self.FavoriteButton.setFixedSize(28, 28)
        self.FavoriteButton.setCheckable(True)
        self.FavoriteButton.setChecked(IsFavorite)
        self.FavoriteButton.setToolTip(Language.Get("Favorites", Count=0).split(" ")[0])
        self.FavoriteButton.toggled.connect(lambda Checked: self.FavoriteChanged.emit(self.FilePath, Checked))
        Layout.addWidget(self.FavoriteButton)
        self.setSizePolicy(QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Fixed)


class TableListPanel(QWidget):
    SelectionChanged = Signal()
    CurrentFileChanged = Signal(object)
    OpenPathRequested = Signal(str)
    FavoriteChanged = Signal(str, bool)

    def __init__(
        self,
        Language: LanguageService,
        Favorites: set[str],
        Recents: list[str],
        IconsDirectory: Path,
    ) -> None:
        super().__init__()
        self.Language = Language
        self.Favorites = set(Favorites)
        self.Recents = list(Recents)
        self.IconsDirectory = IconsDirectory
        self.SourceFiles: list[SourceFileModel] = []
        self.SelectedPaths: set[str] = set()
        self.FilterMode = "All"
        self.setObjectName("TableListPanel")
        self.setMinimumWidth(250)
        self.setMaximumWidth(320)

        Root = QVBoxLayout(self)
        Root.setContentsMargins(10, 10, 10, 10)
        Root.setSpacing(8)

        self.SearchEdit = QLineEdit()
        self.SearchEdit.setPlaceholderText(Language.Get("SearchPlaceholder"))
        self.SearchEdit.setClearButtonEnabled(True)
        self.SearchEdit.textChanged.connect(self.RebuildList)
        Root.addWidget(self.SearchEdit)

        FilterRow = QHBoxLayout()
        FilterRow.setSpacing(5)
        self.FilterGroup = QButtonGroup(self)
        self.FilterGroup.setExclusive(True)
        self.FavoritesButton = QPushButton()
        self.RecentButton = QPushButton()
        self.AllButton = QPushButton()
        for Index, Button in enumerate((self.FavoritesButton, self.RecentButton, self.AllButton)):
            Button.setCheckable(True)
            Button.setFixedHeight(30)
            self.FilterGroup.addButton(Button, Index)
            FilterRow.addWidget(Button)
        self.AllButton.setChecked(True)
        self.FilterGroup.idClicked.connect(self._ChangeFilter)
        Root.addLayout(FilterRow)

        SelectionRow = QHBoxLayout()
        self.SelectAllCheck = QCheckBox()
        self.SelectAllCheck.stateChanged.connect(self._SelectAllChanged)
        SelectionRow.addWidget(self.SelectAllCheck)
        SelectionRow.addStretch(1)
        self.CountLabel = QLabel()
        self.CountLabel.setObjectName("Muted")
        SelectionRow.addWidget(self.CountLabel)
        Root.addLayout(SelectionRow)

        self.ListWidget = QListWidget()
        self.ListWidget.setAlternatingRowColors(True)
        self.ListWidget.setSelectionMode(QAbstractItemView.SelectionMode.SingleSelection)
        self.ListWidget.currentItemChanged.connect(self._CurrentItemChanged)
        Root.addWidget(self.ListWidget, 1)
        self.UpdateTexts()

    def UpdateTexts(self) -> None:
        self.SearchEdit.setPlaceholderText(self.Language.Get("SearchPlaceholder"))
        self.FavoritesButton.setText(self.Language.Get("Favorites", Count=len(self.Favorites)))
        self.RecentButton.setText(self.Language.Get("Recent", Count=len(self.Recents)))
        self.AllButton.setText(self.Language.Get("AllTables", Count=len(self.SourceFiles)))
        self.SelectAllCheck.setText(self.Language.Get("SelectAll"))
        self._UpdateCount()

    def SetFiles(
        self,
        SourceFiles: list[SourceFileModel],
        SelectedPaths: set[str],
        Favorites: set[str],
        Recents: list[str],
    ) -> None:
        self.SourceFiles = list(SourceFiles)
        self.SelectedPaths = set(SelectedPaths)
        self.Favorites = set(Favorites)
        self.Recents = list(Recents)
        self.UpdateTexts()
        self.RebuildList()

    def SetFilterMode(self, FilterMode: str) -> None:
        self.FilterMode = FilterMode
        Button = {
            "Favorites": self.FavoritesButton,
            "Recent": self.RecentButton,
            "All": self.AllButton,
        }.get(FilterMode, self.AllButton)
        Button.setChecked(True)
        self.RebuildList()

    def RebuildList(self) -> None:
        self.ListWidget.blockSignals(True)
        self.ListWidget.clear()
        SearchText = self.SearchEdit.text().strip().lower()
        RecentsSet = set(self.Recents[:10])
        for SourceFile in self.SourceFiles:
            PathText = str(SourceFile.Path)
            if self.FilterMode == "Favorites" and PathText not in self.Favorites:
                continue
            if self.FilterMode == "Recent" and PathText not in RecentsSet:
                continue
            if SearchText and SearchText not in SourceFile.Path.name.lower() and SearchText not in PathText.lower():
                continue
            Item = QListWidgetItem()
            Item.setData(Qt.ItemDataRole.UserRole, PathText)
            Row = FileRowWidget(
                SourceFile,
                self.Language,
                PathText in self.SelectedPaths,
                PathText in self.Favorites,
                self.IconsDirectory,
            )
            Row.CheckChanged.connect(self._SetPathSelected)
            Row.OpenPathRequested.connect(self.OpenPathRequested.emit)
            Row.FavoriteChanged.connect(self._FavoriteChanged)
            self.ListWidget.addItem(Item)
            self.ListWidget.setItemWidget(Item, Row)
        self.ListWidget.blockSignals(False)
        if self.ListWidget.count() > 0:
            self.ListWidget.setCurrentRow(0)
        self._UpdateCount()

    def GetSelectedPaths(self) -> set[str]:
        return set(self.SelectedPaths)

    def SetSelectedPaths(self, SelectedPaths: set[str]) -> None:
        self.SelectedPaths = set(SelectedPaths)
        self.RebuildList()

    def GetFavorites(self) -> set[str]:
        return set(self.Favorites)

    def GetRecents(self) -> list[str]:
        return list(self.Recents)

    def GetFilterMode(self) -> str:
        return self.FilterMode

    def GetSearchText(self) -> str:
        return self.SearchEdit.text()

    def SetSearchText(self, Text: str) -> None:
        self.SearchEdit.setText(Text)

    def MarkRecent(self, FilePath: str) -> None:
        self.Recents = [FilePath] + [Path for Path in self.Recents if Path != FilePath]
        del self.Recents[10:]

    def _ChangeFilter(self, FilterId: int) -> None:
        self.FilterMode = {0: "Favorites", 1: "Recent", 2: "All"}.get(FilterId, "All")
        self.RebuildList()

    def _SelectAllChanged(self, State: int) -> None:
        ShouldSelect = int(State) == int(Qt.CheckState.Checked.value)
        for SourceFile in self.SourceFiles:
            PathText = str(SourceFile.Path)
            if ShouldSelect:
                self.SelectedPaths.add(PathText)
            else:
                self.SelectedPaths.discard(PathText)
        self.RebuildList()
        self.SelectionChanged.emit()

    def _SetPathSelected(self, PathText: str, IsSelected: bool) -> None:
        if IsSelected:
            self.SelectedPaths.add(PathText)
        else:
            self.SelectedPaths.discard(PathText)
        self._UpdateCount()
        self.SelectionChanged.emit()

    def _FavoriteChanged(self, PathText: str, IsFavorite: bool) -> None:
        if IsFavorite:
            self.Favorites.add(PathText)
        else:
            self.Favorites.discard(PathText)
        self.UpdateTexts()
        self.FavoriteChanged.emit(PathText, IsFavorite)

    def _CurrentItemChanged(self, Current: QListWidgetItem | None, Previous: QListWidgetItem | None) -> None:
        if Current is None:
            return
        PathText = Current.data(Qt.ItemDataRole.UserRole)
        SourceFile = next((File for File in self.SourceFiles if str(File.Path) == PathText), None)
        if SourceFile is not None:
            self.CurrentFileChanged.emit(SourceFile)

    def _UpdateCount(self) -> None:
        self.CountLabel.setText(
            self.Language.Get("SelectedCount", Selected=len(self.SelectedPaths), Total=len(self.SourceFiles))
        )
        self.SelectAllCheck.blockSignals(True)
        self.SelectAllCheck.setChecked(bool(self.SourceFiles) and len(self.SelectedPaths) == len(self.SourceFiles))
        self.SelectAllCheck.blockSignals(False)
