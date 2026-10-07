import json
import os
from pathlib import Path
from typing import Any


DefaultSettings: dict[str, Any] = {
    "SchemaVersion": 1,
    "SourceDirectory": "Data",
    "Output": {
        "ClientTableDirectory": "客户端表格数据",
        "ClientCodeDirectory": "客户端代码",
        "ServerTableDirectory": "服务器表格数据",
        "ServerCodeDirectory": "服务器代码",
    },
    "Interface": {
        "Language": "zh-CN",
        "ThemeMode": "Night",
        "SidebarExpanded": True,
        "WindowWidth": 1440,
        "WindowHeight": 960,
        "WindowMaximized": False,
    },
    "TableList": {
        "FilterMode": "All",
        "SearchText": "",
        "FavoriteFiles": [],
        "RecentFiles": [],
        "SelectedFiles": [],
    },
    "ExportTargets": {
        "ClientData": True,
        "ClientCode": True,
        "ServerData": True,
        "ServerCode": False,
    },
    "ArraySyntax": {
        "Level1Delimiter": "#",
        "Level2Delimiter": "|",
        "Level3Delimiter": ";",
        "EscapeCharacter": "\\",
    },
    "LastBuild": {
        "Status": "Idle",
        "Message": "",
        "Timestamp": "",
    },
}


def LoadSettings(ProjectRoot: Path) -> dict[str, Any]:
    SettingsPath = ProjectRoot / "settings.json"
    Loaded: dict[str, Any] = {}
    if SettingsPath.exists():
        try:
            Loaded = json.loads(SettingsPath.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            Loaded = {}
    return MergeSettings(DefaultSettings, Loaded)


def MergeSettings(Base: Any, Override: Any) -> Any:
    if isinstance(Base, dict) and isinstance(Override, dict):
        Result = dict(Base)
        for Key, Value in Override.items():
            Result[Key] = MergeSettings(Base.get(Key), Value) if Key in Base else Value
        return Result
    return Override


def SaveSettings(ProjectRoot: Path, Settings: dict[str, Any]) -> None:
    SettingsPath = ProjectRoot / "settings.json"
    TempPath = SettingsPath.with_suffix(".json.tmp")
    TempPath.write_text(
        json.dumps(Settings, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    os.replace(TempPath, SettingsPath)


def ResolveProjectPath(ProjectRoot: Path, Value: str) -> Path:
    PathValue = Path(Value)
    return PathValue if PathValue.is_absolute() else ProjectRoot / PathValue


def GetSourceDirectory(ProjectRoot: Path, Settings: dict[str, Any]) -> Path:
    return ResolveProjectPath(ProjectRoot, Settings["SourceDirectory"])


def GetOutputDirectories(ProjectRoot: Path, Settings: dict[str, Any]) -> dict[str, Path]:
    Output = Settings["Output"]
    return {
        "ClientData": ResolveProjectPath(ProjectRoot, Output["ClientTableDirectory"]),
        "ClientCode": ResolveProjectPath(ProjectRoot, Output["ClientCodeDirectory"]),
        "ServerData": ResolveProjectPath(ProjectRoot, Output["ServerTableDirectory"]),
        "ServerCode": ResolveProjectPath(ProjectRoot, Output["ServerCodeDirectory"]),
    }
