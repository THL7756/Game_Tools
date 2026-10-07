import json
from pathlib import Path
from typing import Any


class LanguageService:
    def __init__(self, LanguagesDirectory: Path, LanguageCode: str = "zh-CN") -> None:
        self.LanguagesDirectory = LanguagesDirectory
        self.CurrentCode = LanguageCode
        self.Strings: dict[str, str] = {}
        self.Load(LanguageCode)

    def Load(self, LanguageCode: str) -> None:
        Fallback = self.ReadLanguage("zh-CN")
        Selected = self.ReadLanguage(LanguageCode)
        self.Strings = {**Fallback, **Selected}
        self.CurrentCode = LanguageCode

    def ReadLanguage(self, LanguageCode: str) -> dict[str, str]:
        LanguagePath = self.LanguagesDirectory / f"{LanguageCode}.json"
        if not LanguagePath.exists():
            return {}
        try:
            Loaded: dict[str, Any] = json.loads(LanguagePath.read_text(encoding="utf-8"))
            return {str(Key): str(Value) for Key, Value in Loaded.items()}
        except (OSError, json.JSONDecodeError):
            return {}

    def Get(self, Key: str, **Arguments: Any) -> str:
        Template = self.Strings.get(Key, Key)
        try:
            return Template.format(**Arguments)
        except (KeyError, ValueError):
            return Template
