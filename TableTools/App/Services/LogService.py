# 用途：持久化运行日志、限制日志数量并向日志面板推送新增条目。
# 最近修改日期：2026-10-07
# 作者：Codex

import json
from datetime import datetime
from pathlib import Path
from typing import Any

from PySide6.QtCore import QObject, Signal


class LogService(QObject):
    EntryAdded = Signal(object)

    def __init__(self, LogPath: Path, Parent: QObject | None = None, MaxEntries: int = 2000) -> None:
        super().__init__(Parent)
        self.LogPath = LogPath
        self.MaxEntries = MaxEntries

    def Write(self, Level: str, Message: str, **Details: Any) -> None:
        Entry = {
            "Timestamp": datetime.now().isoformat(timespec="seconds"),
            "Level": Level.upper(),
            "Message": Message,
            "Details": Details,
        }
        try:
            self.LogPath.parent.mkdir(parents=True, exist_ok=True)
            with self.LogPath.open("a", encoding="utf-8") as Handle:
                Handle.write(json.dumps(Entry, ensure_ascii=False, default=str) + "\n")
            self._Trim()
        except OSError:
            pass
        self.EntryAdded.emit(Entry)

    def Read(self, Limit: int = 500) -> list[dict[str, Any]]:
        if not self.LogPath.exists():
            return []
        Entries: list[dict[str, Any]] = []
        try:
            Lines = self.LogPath.read_text(encoding="utf-8").splitlines()
        except OSError:
            return []
        for Line in Lines[-Limit:]:
            try:
                Value = json.loads(Line)
            except json.JSONDecodeError:
                continue
            if isinstance(Value, dict):
                Entries.append(Value)
        return Entries

    def Clear(self) -> None:
        try:
            if self.LogPath.exists():
                self.LogPath.write_text("", encoding="utf-8")
        except OSError:
            pass

    def _Trim(self) -> None:
        try:
            Lines = self.LogPath.read_text(encoding="utf-8").splitlines()
            if len(Lines) > self.MaxEntries:
                self.LogPath.write_text("\n".join(Lines[-self.MaxEntries:]) + "\n", encoding="utf-8")
        except OSError:
            pass
