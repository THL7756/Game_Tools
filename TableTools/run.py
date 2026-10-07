# 用途：提供双击启动入口，并自动切换到项目虚拟环境。
# 最近修改日期：2026-10-07
# 作者：Codex

import os
import subprocess
import sys
from pathlib import Path


sys.dont_write_bytecode = True


def _ProjectPython() -> Path:
    """返回项目虚拟环境解释器，供从文件管理器双击时自举。"""
    ProjectRoot = Path(__file__).resolve().parent
    if os.name == "nt":
        return ProjectRoot / ".venv" / "Scripts" / "python.exe"
    return ProjectRoot / ".venv" / "bin" / "python"


def _RunInProjectEnvironment() -> int | None:
    """系统 Python 误启动入口时，转交给项目虚拟环境并保留退出码。"""
    ProjectPython = _ProjectPython()
    if not ProjectPython.exists():
        return None
    CurrentPython = Path(sys.executable).resolve()
    if CurrentPython == ProjectPython.resolve():
        return None
    Environment = os.environ.copy()
    for Name in ("QT_QPA_PLATFORM", "QT_PLUGIN_PATH", "QT_QPA_PLATFORM_PLUGIN_PATH", "QML2_IMPORT_PATH"):
        Environment.pop(Name, None)
    return subprocess.call(
        [str(ProjectPython), str(Path(__file__).resolve()), *sys.argv[1:]],
        cwd=str(Path(__file__).resolve().parent),
        env=Environment,
    )


if __name__ == "__main__":
    ProjectExitCode = _RunInProjectEnvironment()
    if ProjectExitCode is not None:
        raise SystemExit(ProjectExitCode)

    try:
        from App.Main import RunApplication
    except ModuleNotFoundError as Error:
        if Error.name not in {"PySide6", "openpyxl", "xlrd"}:
            raise
        Message = (
            "TableTools 依赖尚未安装。请双击 StartTableTools.bat 或 启动TableTools.bat，"
            "启动器会自动创建 .venv 并安装依赖。"
        )
        if os.name == "nt":
            try:
                import ctypes

                ctypes.windll.user32.MessageBoxW(0, Message, "TableTools 启动失败", 0x10)
            except (AttributeError, OSError):
                pass
        raise SystemExit(Message)

    RaiseCode = RunApplication()
    raise SystemExit(RaiseCode)
