# 用途：加载中英文语言资源，并为界面文本提供统一格式化入口。
# 最近修改日期：2026-10-07
# 作者：Codex

import json
import re
from pathlib import Path
from typing import Any

from App.Core.Models import ValidationIssue


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

    def Get(self, Key: str, *PositionalArguments: Any, **Arguments: Any) -> str:
        Template = self.Strings.get(Key, Key)
        try:
            return Template.format(*PositionalArguments, **Arguments)
        except (KeyError, ValueError):
            return Template

    def FormatIssue(self, Issue: ValidationIssue) -> str:
        """Render parser issues with the active language while preserving source coordinates."""
        Location = f"{Issue.Location.FilePath.name} · {Issue.Location.SheetName}"
        if Issue.Location.Row > 0:
            RowLabel = "第{}行".format(Issue.Location.Row) if self.CurrentCode == "zh-CN" else f"Row {Issue.Location.Row}"
            Location += f" · {RowLabel}"
        if Issue.FieldName:
            FieldLabel = f"{Issue.FieldName} 字段" if self.CurrentCode == "zh-CN" else f"field {Issue.FieldName}"
            Location += f" · {FieldLabel}"
        Message = self._TranslateIssueMessage(Issue.Message)
        Prefix = "问题" if self.CurrentCode == "zh-CN" else "Issue"
        return f"{Location} · {Prefix}: {Message}"

    def LocalizeMessage(self, Message: str) -> str:
        """Translate runtime/export messages without exposing parser language keys."""
        return self._TranslateIssueMessage(str(Message))

    def _TranslateIssueMessage(self, Message: str) -> str:
        if self.CurrentCode == "zh-CN":
            return Message
        Replacements = {
            "请先勾选源文件": "Select at least one source file",
            "当前目标没有需要导出的内容": "The selected targets contain no exportable fields",
            "服务器代码生成尚未实现，本次未生成服务器代码文件": "Server code generation is not implemented; no server code file was written",
            "导出失败：": "Export failed: ",
            "已处理 ": "Processed ",
            " 个逻辑表，生成 ": " logical tables, wrote ",
            " 个文件": " files",
            "int 格式错误": "Invalid int format",
            "long 格式错误": "Invalid long format",
            "float 格式错误": "Invalid float format",
            "double 格式错误": "Invalid double format",
            "int 超出范围": "int is out of range",
            "long 超出范围": "long is out of range",
            "bool 仅接受 true、false、1、0": "bool accepts only true, false, 1 or 0",
            "Color 需要 3 或 4 个分量": "Color requires 3 or 4 components",
            "Vector2 需要 2 个分量": "Vector2 requires 2 components",
            "Vector3 需要 3 个分量": "Vector3 requires 3 components",
            "Vector4 需要 4 个分量": "Vector4 requires 4 components",
            "Quaternion 需要 4 个分量": "Quaternion requires 4 components",
            "结构值分量格式错误": "A structure component has an invalid format",
            "结构值分量不能为 NaN 或 Infinity": "A structure component cannot be NaN or Infinity",
            "字段类型为空": "Field type is empty",
            "未知字段类型 str，请使用 string": "Unknown field type str; use string",
            "未知字段类型 ": "Unknown field type ",
            "enum 配置中存在空值": "The enum contains an empty value",
            "enum 配置中存在重复值": "The enum contains a duplicate value",
            "enum 值 ": "Enum value ",
            " 不在配置中": " is not in the enum configuration",
            "数组不能为空": "Array cannot be empty",
            "数组元素不能为空": "Array elements cannot be empty",
            "数组最多支持三维": "Arrays support at most three dimensions",
            "普通表需要六行定义区": "A regular table needs six definition rows",
            "普通表没有有效字段": "The regular table has no valid fields",
            "单例表没有有效字段": "The singleton table has no valid fields",
            "主键为空": "Primary key is empty",
            "单例表字段重复": "Singleton field is duplicated",
            "引用的": "Referenced ",
            " id 未找到：": " id was not found: ",
            "客户端服务器标记无效：": "Invalid client/server scope: ",
            "Sheet 为空或缺少 table 定义": "The sheet is empty or has no table definition",
            "缺少 table: 逻辑表名": "The table logical name is missing",
            "文件无法读取：": "Unable to read file: ",
            "表格文件无法读取：": "Unable to read workbook: ",
            "普通表需要六行定义区": "A regular table needs six definition rows",
            "普通表没有有效字段": "The regular table has no valid fields",
            "单例表缺少语义列：": "The singleton table is missing semantic columns: ",
            "单例表缺少语义定义区": "The singleton table is missing its definition rows",
            "单例表没有有效字段": "The singleton table has no valid fields",
            "普通表": "Regular table",
            "单例表": "Singleton table",
            "同名逻辑表": "Logical table",
            "schema 不一致，禁止合并；": " has a schema mismatch and cannot be merged; ",
            "已有定义：": "existing: ",
            "当前定义：": "current: ",
            "字段名、类型和客户端/服务器范围必须完全一致。": "field names, types, and client/server scope must match.",
            "应用启动": "Application started",
            "应用关闭": "Application closed",
            "开始扫描表格": "Table scan started",
            "表格扫描完成": "Table scan completed",
            "已加载解析缓存": "Parsed table cache loaded",
            "解析缓存无法读取，将重新扫描": "Parsed table cache could not be read; rescanning",
            "检测到源目录变化，自动刷新": "Source directory changed; refreshing",
            "源表文件变化": "Source table file changed",
            "源目录变化": "Source directory changed",
            "导出目标已更新": "Export targets updated",
            "开始打表": "Build started",
            "打表阻断": "Build blocked",
            "打表完成": "Build completed",
            "打表失败": "Build failed",
            "已全选源文件": "All source files selected",
            "已清空文件勾选": "Source file selection cleared",
            "当前文件已删除，已切换到可用文件": "The current file was deleted; switched to an available file",
            "缺少图标资源：": "Missing icon resource: ",
        }
        Result = Message
        Result = re.sub(
            r"同名逻辑表 (.+?) 的 schema 不一致，禁止合并；",
            r"Logical table \1 has a schema mismatch and cannot be merged; ",
            Result,
        )
        for Source, Target in Replacements.items():
            Result = Result.replace(Source, Target)
        Result = re.sub(r"单例表缺少语义列：", "Singleton table is missing semantic columns: ", Result)
        if self.CurrentCode != "zh-CN":
            Result = re.sub(r"第(\d+)行", r"Row \1", Result)
            Result = Result.replace("字段", "field").replace("问题：", "Issue: ")
        return Result
