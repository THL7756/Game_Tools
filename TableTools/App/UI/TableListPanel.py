# 用途：显示源表文件列表，并隔离预览、导出勾选、收藏、搜索和筛选交互。
# 最近修改日期：2026-10-07
# 作者：Codex

import os
from pathlib import Path

from PySide6.QtCore import Qt, Signal
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
from App.Services.IconService import IconService
from App.Services.LanguageService import LanguageService


class FileRowWidget(QWidget):
    CheckChanged = Signal(str, bool)
    OpenPathRequested = Signal(str)
    FavoriteChanged = Signal(str, bool)
    Activated = Signal(str)

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
        self.Icons = IconService(IconsDirectory)
        Layout = QHBoxLayout(self)
        Layout.setContentsMargins(8, 5, 7, 5)
        Layout.setSpacing(7)
        self.Check = QCheckBox()
        self.Check.setToolTip(str(SourceFile.Path))
        self.Check.setChecked(IsSelected)
        self.Check.toggled.connect(lambda Checked: self.CheckChanged.emit(self.FilePath, Checked))
        Layout.addWidget(self.Check)
        self.FileIcon = QLabel()
        self.FileIcon.setPixmap(self.Icons.Pixmap("File", 18))
        self.FileIcon.setAttribute(Qt.WidgetAttribute.WA_TransparentForMouseEvents, True)
        Layout.addWidget(self.FileIcon)
        self.NameLabel = QLabel(SourceFile.Path.stem)
        self.NameLabel.setObjectName("FileName")
        self.NameLabel.setToolTip(str(SourceFile.Path))
        self.NameLabel.setAttribute(Qt.WidgetAttribute.WA_TransparentForMouseEvents, True)
        Layout.addWidget(self.NameLabel, 1)
        self.OpenButton = QToolButton()
        self.OpenButton.setObjectName("FileOpenButton")
        self.OpenButton.setIcon(self.Icons.Get("External"))
        self.OpenButton.setFixedSize(28, 28)
        self.OpenButton.setToolTip(Language.Get("OpenFilePath"))
        self.OpenButton.setAccessibleName(Language.Get("OpenFilePath"))
        self.OpenButton.clicked.connect(lambda: self.OpenPathRequested.emit(self.FilePath))
        Layout.addWidget(self.OpenButton)
        self.FavoriteButton = QToolButton()
        self.FavoriteButton.setObjectName("FavoriteButton")
        self.FavoriteButton.setIcon(self.Icons.Get("StarFilled" if IsFavorite else "Star"))
        self.FavoriteButton.setFixedSize(28, 28)
        self.FavoriteButton.setCheckable(True)
        self.FavoriteButton.setChecked(IsFavorite)
        self.FavoriteButton.setToolTip(Language.Get("FavoriteTooltip"))
        self.FavoriteButton.setAccessibleName(Language.Get("FavoriteTooltip"))
        self.FavoriteButton.toggled.connect(self._SetFavoriteIcon)
        self.FavoriteButton.toggled.connect(lambda Checked: self.FavoriteChanged.emit(self.FilePath, Checked))
        Layout.addWidget(self.FavoriteButton)
        self.setSizePolicy(QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Fixed)

    def mousePressEvent(self, Event) -> None:
        if Event.button() == Qt.MouseButton.LeftButton:
            self.Activated.emit(self.FilePath)
        super().mousePressEvent(Event)

    def _SetFavoriteIcon(self, Checked: bool) -> None:
        self.FavoriteButton.setIcon(self.Icons.Get("StarFilled" if Checked else "Star"))


class TableListPanel(QWidget):
    SelectionChanged = Signal()
    CurrentFileChanged = Signal(object)
    OpenPathRequested = Signal(str)
    FavoriteChanged = Signal(str, bool)
    StateChanged = Signal()

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
        self.Icons = IconService(IconsDirectory)
        self.SourceFiles: list[SourceFileModel] = []
        self.SelectedPaths: set[str] = set()
        self.FilterMode = "All"
        self.CurrentPath = ""
        self.setObjectName("TableListPanel")
        self.setMinimumWidth(290)
        self.setMaximumWidth(360)

        Root = QVBoxLayout(self)
        Root.setContentsMargins(12, 12, 12, 12)
        Root.setSpacing(8)

        self.SearchEdit = QLineEdit()
        self.SearchEdit.addAction(self.Icons.Get("Search"), QLineEdit.ActionPosition.LeadingPosition)
        self.SearchClearAction = self.SearchEdit.addAction(self.Icons.Get("Clear"), QLineEdit.ActionPosition.TrailingPosition)
        self.SearchClearAction.setToolTip(Language.Get("ClearSearch"))
        self.SearchClearAction.triggered.connect(self.ClearSearch)
        self.SearchEdit.setPlaceholderText(Language.Get("SearchPlaceholder"))
        self.SearchEdit.setClearButtonEnabled(False)
        self.SearchEdit.textChanged.connect(self._SearchChanged)
        self.SearchEdit.textChanged.connect(lambda Text: self.SearchClearAction.setVisible(bool(Text)))
        self.SearchClearAction.setVisible(False)
        Root.addWidget(self.SearchEdit)

        FilterRow = QHBoxLayout()
        FilterRow.setSpacing(5)
        self.FilterGroup = QButtonGroup(self)
        self.FilterGroup.setExclusive(True)
        self.FavoritesButton = QPushButton()
        self.RecentButton = QPushButton()
        self.AllButton = QPushButton()
        self.FavoritesButton.setIcon(self.Icons.Get("Favorite"))
        self.RecentButton.setIcon(self.Icons.Get("File"))
        self.AllButton.setIcon(self.Icons.Get("Table"))
        for Index, Button in enumerate((self.FavoritesButton, self.RecentButton, self.AllButton)):
            Button.setCheckable(True)
            Button.setFixedHeight(30)
            self.FilterGroup.addButton(Button, Index)
            FilterRow.addWidget(Button)
        self.FilterGroup.idClicked.connect(self._ChangeFilter)
        Root.addLayout(FilterRow)

        SelectionRow = QHBoxLayout()
        self.SelectAllCheck = QCheckBox()
        self.SelectAllCheck.setTristate(True)
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
        self.EmptyLabel = QLabel()
        self.EmptyLabel.setObjectName("EmptyState")
        self.EmptyLabel.setAlignment(Qt.AlignmentFlag.AlignCenter)
        self.EmptyLabel.setVisible(False)
        Root.addWidget(self.EmptyLabel)
        self.UpdateTexts()

    def UpdateTexts(self) -> None:
        self.SearchEdit.setPlaceholderText(self.Language.Get("SearchPlaceholder"))
        AvailableKeys = {self._PathKey(str(File.Path)) for File in self.SourceFiles}
        FavoriteCount = sum(self._PathKey(PathText) in AvailableKeys for PathText in self.Favorites)
        RecentCount = sum(self._PathKey(PathText) in AvailableKeys for PathText in self.Recents)
        self.FavoritesButton.setText(self.Language.Get("Favorites", Count=FavoriteCount))
        self.RecentButton.setText(self.Language.Get("Recent", Count=RecentCount))
        self.AllButton.setText(self.Language.Get("AllTables", Count=len(self.SourceFiles)))
        self.SelectAllCheck.setText(self.Language.Get("SelectAll"))
        self.EmptyLabel.setText(self.Language.Get("NoTables"))
        self._UpdateCount()

    def SetFiles(
        self,
        SourceFiles: list[SourceFileModel],
        SelectedPaths: set[str],
        Favorites: set[str],
        Recents: list[str],
    ) -> None:
        self.SourceFiles = list(SourceFiles)
        Available = {self._PathKey(str(File.Path)): str(File.Path) for File in self.SourceFiles}
        self.SelectedPaths = {
            Available[self._PathKey(PathText)]
            for PathText in SelectedPaths
            if self._PathKey(PathText) in Available
        }
        self.Favorites = {
            Available.get(self._PathKey(PathText), str(PathText))
            for PathText in Favorites
        }
        self.Recents = [
            Available.get(self._PathKey(PathText), str(PathText))
            for PathText in Recents
        ]
        self.UpdateTexts()
        self.RebuildList()

    @staticmethod
    def _PathKey(PathText: str) -> str:
        try:
            return os.path.normcase(str(Path(PathText).expanduser().resolve(strict=False)))
        except OSError:
            return os.path.normcase(str(Path(PathText)))

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
        PreviousPath = self.CurrentPath
        self.ListWidget.blockSignals(True)
        self.ListWidget.clear()
        SearchText = self.SearchEdit.text().strip().lower()
        FavoriteKeys = {self._PathKey(PathText) for PathText in self.Favorites}
        RecentsKeys = {self._PathKey(PathText) for PathText in self.Recents[:10]}
        SelectedKeys = {self._PathKey(PathText) for PathText in self.SelectedPaths}
        for SourceFile in self.SourceFiles:
            PathText = str(SourceFile.Path)
            PathKey = self._PathKey(PathText)
            if self.FilterMode == "Favorites" and PathKey not in FavoriteKeys:
                continue
            if self.FilterMode == "Recent" and PathKey not in RecentsKeys:
                continue
            if SearchText and SearchText not in SourceFile.Path.name.lower() and SearchText not in PathText.lower():
                continue
            Item = QListWidgetItem()
            Item.setData(Qt.ItemDataRole.UserRole, PathText)
            Row = FileRowWidget(
                SourceFile,
                self.Language,
                PathKey in SelectedKeys,
                PathKey in FavoriteKeys,
                self.IconsDirectory,
            )
            Item.setSizeHint(Row.sizeHint())
            Row.CheckChanged.connect(self._SetPathSelected)
            Row.OpenPathRequested.connect(self.OpenPathRequested.emit)
            Row.FavoriteChanged.connect(self._FavoriteChanged)
            Row.Activated.connect(self._UserActivatePath)
            self.ListWidget.addItem(Item)
            self.ListWidget.setItemWidget(Item, Row)
        self.ListWidget.blockSignals(False)
        IsEmpty = self.ListWidget.count() == 0
        self.EmptyLabel.setVisible(IsEmpty)
        self.ListWidget.setVisible(not IsEmpty)
        TargetRow = self._FindRow(PreviousPath)
        PreviousExists = any(self._PathKey(str(File.Path)) == self._PathKey(PreviousPath) for File in self.SourceFiles)
        if TargetRow < 0 and PreviousPath and PreviousExists:
            # A filter may hide the current file. Keep its preview context until the filter is cleared.
            self.ListWidget.blockSignals(True)
            self.ListWidget.setCurrentRow(-1)
            self.ListWidget.blockSignals(False)
            self.CurrentPath = PreviousPath
        elif TargetRow < 0 and self.ListWidget.count() > 0:
            TargetRow = 0
        if TargetRow >= 0:
            self.ListWidget.setCurrentRow(TargetRow)
            if self.CurrentPath != self.ListWidget.currentItem().data(Qt.ItemDataRole.UserRole):
                self._CurrentItemChanged(self.ListWidget.currentItem(), None)
        elif self.CurrentPath and not PreviousExists:
            self.CurrentPath = ""
            self.CurrentFileChanged.emit(None)
        self._UpdateCount()

    def _FindRow(self, PathText: str) -> int:
        TargetKey = self._PathKey(PathText)
        for Row in range(self.ListWidget.count()):
            RowPath = self.ListWidget.item(Row).data(Qt.ItemDataRole.UserRole)
            if self._PathKey(str(RowPath)) == TargetKey:
                return Row
        return -1

    def _ActivatePath(self, PathText: str) -> None:
        Row = self._FindRow(PathText)
        if Row >= 0:
            self.ListWidget.setCurrentRow(Row)

    def _UserActivatePath(self, PathText: str) -> None:
        self.MarkRecent(PathText)
        self._ActivatePath(PathText)
        self.StateChanged.emit()

    def GetSelectedPaths(self) -> set[str]:
        return set(self.SelectedPaths)

    def SetSelectedPaths(self, SelectedPaths: set[str]) -> None:
        Available = {self._PathKey(str(File.Path)): str(File.Path) for File in self.SourceFiles}
        self.SelectedPaths = {
            Available[self._PathKey(PathText)]
            for PathText in SelectedPaths
            if self._PathKey(PathText) in Available
        }
        self.RebuildList()
        self.SelectionChanged.emit()
        self.StateChanged.emit()

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

    def ClearSearch(self) -> None:
        self.SearchEdit.clear()

    def FavoriteCurrent(self, IsFavorite: bool) -> None:
        if not self.CurrentPath:
            return
        PathText = self.CurrentPath
        self.Favorites.add(PathText) if IsFavorite else self.Favorites.discard(PathText)
        self.RebuildList()
        self.FavoriteChanged.emit(PathText, IsFavorite)
        self.StateChanged.emit()

    def ClearFavorites(self) -> None:
        Previous = list(self.Favorites)
        self.Favorites.clear()
        self.RebuildList()
        for PathText in Previous:
            self.FavoriteChanged.emit(PathText, False)
        self.StateChanged.emit()

    def MarkRecent(self, FilePath: str) -> None:
        FileKey = self._PathKey(FilePath)
        self.Recents = [FilePath] + [
            PathValue for PathValue in self.Recents if self._PathKey(PathValue) != FileKey
        ]
        del self.Recents[10:]
        self.UpdateTexts()

    def _ChangeFilter(self, FilterId: int) -> None:
        self.FilterMode = {0: "Favorites", 1: "Recent", 2: "All"}.get(FilterId, "All")
        self.RebuildList()
        self.StateChanged.emit()

    def _SearchChanged(self) -> None:
        self.RebuildList()
        self.StateChanged.emit()

    def _SelectAllChanged(self, State: int) -> None:
        ShouldSelect = int(State) == int(Qt.CheckState.Checked.value)
        if ShouldSelect:
            self.SelectedPaths = {str(SourceFile.Path) for SourceFile in self.SourceFiles}
        else:
            self.SelectedPaths.clear()
        self.RebuildList()
        self.SelectionChanged.emit()
        self.StateChanged.emit()

    def _SetPathSelected(self, PathText: str, IsSelected: bool) -> None:
        CanonicalPath = next(
            (str(File.Path) for File in self.SourceFiles if self._PathKey(str(File.Path)) == self._PathKey(PathText)),
            PathText,
        )
        if IsSelected:
            self.SelectedPaths.add(CanonicalPath)
        else:
            self.SelectedPaths = {
                Value for Value in self.SelectedPaths if self._PathKey(Value) != self._PathKey(PathText)
            }
        self._UpdateCount()
        self.SelectionChanged.emit()
        self.StateChanged.emit()

    def _FavoriteChanged(self, PathText: str, IsFavorite: bool) -> None:
        if IsFavorite:
            self.Favorites.add(PathText)
        else:
            self.Favorites.discard(PathText)
        self.UpdateTexts()
        # 收藏筛选下取消星标后，行应立即从当前列表消失；其它筛选也同步行状态。
        self.RebuildList()
        self.FavoriteChanged.emit(PathText, IsFavorite)
        self.StateChanged.emit()

    def _CurrentItemChanged(self, Current: QListWidgetItem | None, Previous: QListWidgetItem | None) -> None:
        if Current is None:
            if self.CurrentPath:
                self.CurrentPath = ""
                self.CurrentFileChanged.emit(None)
            return
        PathText = Current.data(Qt.ItemDataRole.UserRole)
        PathKey = self._PathKey(str(PathText))
        SourceFile = next((File for File in self.SourceFiles if self._PathKey(str(File.Path)) == PathKey), None)
        if SourceFile is not None:
            self.CurrentPath = PathText
            self.CurrentFileChanged.emit(SourceFile)

    def _UpdateCount(self) -> None:
        self.CountLabel.setText(
            self.Language.Get("SelectedCount", Selected=len(self.SelectedPaths), Total=len(self.SourceFiles))
        )
        self.SelectAllCheck.blockSignals(True)
        if not self.SelectedPaths:
            self.SelectAllCheck.setCheckState(Qt.CheckState.Unchecked)
        elif self.SourceFiles and len(self.SelectedPaths) == len(self.SourceFiles):
            self.SelectAllCheck.setCheckState(Qt.CheckState.Checked)
        else:
            self.SelectAllCheck.setCheckState(Qt.CheckState.PartiallyChecked)
        self.SelectAllCheck.blockSignals(False)
