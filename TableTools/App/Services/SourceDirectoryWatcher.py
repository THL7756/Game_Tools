# 用途：递归监听源表目录，忽略临时文件和输出目录，并以防抖信号触发刷新。
# 最近修改日期：2026-10-07
# 作者：Codex

from pathlib import Path

from PySide6.QtCore import QFileSystemWatcher, QObject, QTimer, Signal


class SourceDirectoryWatcher(QObject):
    Changed = Signal()
    PathChanged = Signal(str)

    def __init__(self, Parent: QObject | None = None) -> None:
        super().__init__(Parent)
        self.Watcher = QFileSystemWatcher(self)
        self.Timer = QTimer(self)
        self.Timer.setSingleShot(True)
        self.Timer.setInterval(600)
        self.Timer.timeout.connect(self.Changed.emit)
        self.Watcher.directoryChanged.connect(self._ScheduleChanged)
        self.Watcher.fileChanged.connect(self._ScheduleChanged)
        self.RootDirectory: Path | None = None
        self.IgnoredDirectories: tuple[Path, ...] = ()

    def SetRootDirectory(self, RootDirectory: Path, IgnoredDirectories: tuple[Path, ...] = ()) -> None:
        self.RootDirectory = RootDirectory
        self.IgnoredDirectories = tuple(Path(Item).resolve() for Item in IgnoredDirectories)
        ExistingPaths = self.Watcher.directories() + self.Watcher.files()
        if ExistingPaths:
            self.Watcher.removePaths(ExistingPaths)
        Paths = self._CollectPaths(RootDirectory)
        if Paths:
            self.Watcher.addPaths(Paths)

    def _CollectPaths(self, RootDirectory: Path) -> list[str]:
        if not RootDirectory.exists():
            # Keep watching the parent so recreating a deleted source directory
            # can register the recursive watchers again automatically.
            Parent = RootDirectory.parent
            return [str(Parent)] if Parent.exists() and not self._IsIgnored(Parent) else []
        RootResolved = RootDirectory.resolve()
        if self._IsIgnored(RootResolved):
            return []
        Paths = [str(RootDirectory)]
        for PathItem in RootDirectory.rglob("*"):
            if self._IsIgnored(PathItem):
                continue
            if PathItem.is_dir():
                Paths.append(str(PathItem))
                continue
            if (
                PathItem.is_file()
                and PathItem.suffix.lower() in {".xlsx", ".xls"}
                and not PathItem.name.startswith("~$")
            ):
                Paths.append(str(PathItem))
        return Paths

    def _ScheduleChanged(self, ChangedPath: str) -> None:
        if Path(ChangedPath).name.startswith("~$"):
            return
        if self._IsIgnored(Path(ChangedPath)):
            return
        self.PathChanged.emit(str(ChangedPath))
        if self.RootDirectory is not None:
            self.SetRootDirectory(self.RootDirectory, self.IgnoredDirectories)
        self.Timer.start()

    def _IsIgnored(self, Candidate: Path) -> bool:
        try:
            Resolved = Candidate.resolve()
            return any(Resolved == Root or Root in Resolved.parents for Root in self.IgnoredDirectories)
        except OSError:
            return False
