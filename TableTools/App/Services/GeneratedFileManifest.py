import hashlib
import json
import os
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


class GeneratedFileManifest:
    def __init__(self, ManifestPath: Path) -> None:
        self.ManifestPath = ManifestPath

    def Load(self) -> list[dict[str, Any]]:
        if not self.ManifestPath.exists():
            return []
        try:
            Data = json.loads(self.ManifestPath.read_text(encoding="utf-8"))
            Entries = Data.get("GeneratedFiles", [])
            return [Entry for Entry in Entries if isinstance(Entry, dict)]
        except (OSError, json.JSONDecodeError):
            return []

    def Save(self, Entries: list[dict[str, Any]]) -> None:
        self.ManifestPath.parent.mkdir(parents=True, exist_ok=True)
        TempPath = self.ManifestPath.with_suffix(".json.tmp")
        TempPath.write_text(
            json.dumps(
                {"SchemaVersion": 1, "GeneratedFiles": Entries},
                ensure_ascii=False,
                indent=2,
            )
            + "\n",
            encoding="utf-8",
        )
        os.replace(TempPath, self.ManifestPath)

    def ReplaceGeneratedFile(
        self,
        Target: str,
        LogicalTable: str,
        FileType: str,
        OutputPath: Path,
        Content: str,
        AllowedRoot: Path,
    ) -> None:
        OutputPath.parent.mkdir(parents=True, exist_ok=True)
        TempPath = OutputPath.with_name(OutputPath.name + ".tmp")
        TempPath.write_text(Content, encoding="utf-8", newline="\n")
        os.replace(TempPath, OutputPath)

        Entries = self.Load()
        Remaining: list[dict[str, Any]] = []
        NewHash = ComputeContentHash(Content)
        for Entry in Entries:
            Matches = (
                Entry.get("Target") == Target
                and Entry.get("LogicalTable") == LogicalTable
                and Entry.get("FileType") == FileType
            )
            if not Matches:
                Remaining.append(Entry)
                continue
            OldPath = Path(str(Entry.get("OutputPath", "")))
            if OldPath != OutputPath and self.CanDeleteGeneratedFile(OldPath, AllowedRoot, str(Entry.get("ContentHash", ""))):
                try:
                    OldPath.unlink()
                except OSError:
                    pass

        Remaining.append(
            {
                "Target": Target,
                "LogicalTable": LogicalTable,
                "FileType": FileType,
                "OutputPath": str(OutputPath),
                "ContentHash": NewHash,
                "GeneratedAt": datetime.now(timezone.utc).isoformat(),
            }
        )
        self.Save(Remaining)

    def CanDeleteGeneratedFile(self, OutputPath: Path, AllowedRoot: Path, ExpectedHash: str) -> bool:
        if not ExpectedHash or not OutputPath.exists():
            return False
        try:
            ResolvedPath = OutputPath.resolve()
            ResolvedRoot = AllowedRoot.resolve()
            ResolvedPath.relative_to(ResolvedRoot)
        except (OSError, ValueError):
            return False
        try:
            return ComputeContentHash(ResolvedPath.read_text(encoding="utf-8")) == ExpectedHash
        except OSError:
            return False


def ComputeContentHash(Content: str) -> str:
    return hashlib.sha256(Content.encode("utf-8")).hexdigest()
