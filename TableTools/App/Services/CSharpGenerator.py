# 用途：根据逻辑表生成客户端 C# 数据模型和 Unity JsonUtility 读取包装。
# 最近修改日期：2026-10-07
# 作者：Codex

import re
from datetime import date

from App.Core.Models import FieldDefinition, LogicalTable


NonIdentifierPattern = re.compile(r"[^A-Za-z0-9]+")


def GenerateClientCSharp(Table: LogicalTable) -> str:
    ClassName = ToPascal(Table.Name) + "Config"
    Lines = [
        f"// 用途：{Table.Name} 客户端表格数据与 Unity JsonUtility 读取包装",
        f"// 最近修改日期：{date.today().isoformat()}",
        "",
        "using System;",
        "using UnityEngine;",
        "using UnityEngine.Serialization;",
        "",
        "namespace TableTools.Generated",
        "{",
    ]

    EnumBlocks: list[str] = []
    FieldLines: list[str] = []
    for Field in Table.Fields:
        SerializedType = GetSerializedCSharpType(Field)
        PropertyName = GetUniquePascalName(Field.Name, {Item.Name for Item in Table.Fields if Item != Field})
        SerializedFieldName = PropertyName + "Value" if Field.ParsedType.BaseName == "enum" and Field.ParsedType.Dimensions == 0 else PropertyName
        FieldLines.append(f"        [FormerlySerializedAs(\"{EscapeCSharp(Field.Name)}\")] public {SerializedType} {SerializedFieldName};")

        if Field.ParsedType.BaseName == "enum" and Field.ParsedType.Dimensions == 0:
            EnumName = PropertyName + "Options"
            EnumValues = [ToPascal(Value) or f"Value{Index}" for Index, Value in enumerate(Field.ParsedType.EnumValues)]
            EnumLines = [f"        public enum {EnumName}", "        {"]
            EnumLines.extend(f"            {Value}," for Value in EnumValues)
            EnumLines.append("        }")
            EnumBlocks.extend(EnumLines)
            EnumBlocks.append("")
            FieldLines.append(f"        public {EnumName} {PropertyName}")
            FieldLines.append("        {")
            FieldLines.append("            get")
            FieldLines.append("            {")
            for SourceValue, EnumValue in zip(Field.ParsedType.EnumValues, EnumValues):
                FieldLines.append(f"                if ({SerializedFieldName} == \"{EscapeCSharp(SourceValue)}\") return {EnumName}.{EnumValue};")
            FieldLines.append("                return default;")
            FieldLines.append("            }")
            FieldLines.append("        }")

    if Table.IsSingle:
        Lines.append(f"    [Serializable]")
        Lines.append(f"    public sealed class {ClassName}")
        Lines.append("    {")
        Lines.extend(EnumBlocks)
        Lines.extend(FieldLines)
        Lines.append("")
        Lines.append(f"        public static {ClassName} LoadFromJson(string Json)")
        Lines.append("        {")
        Lines.append(f"            return JsonUtility.FromJson<{ClassName}>(Json);")
        Lines.append("        }")
        Lines.append("    }")
    else:
        RowClassName = ToPascal(Table.Name) + "ConfigRow"
        Lines.append("    [Serializable]")
        Lines.append(f"    public sealed class {RowClassName}")
        Lines.append("    {")
        Lines.extend(EnumBlocks)
        Lines.extend(FieldLines)
        Lines.append("    }")
        Lines.append("")
        Lines.append("    [Serializable]")
        Lines.append(f"    public sealed class {ClassName}")
        Lines.append("    {")
        Lines.append(f"        [FormerlySerializedAs(\"Rows\")] public {RowClassName}[] Rows = Array.Empty<{RowClassName}>();")
        Lines.append("")
        Lines.append(f"        public static {ClassName} LoadFromJson(string Json)")
        Lines.append("        {")
        Lines.append(f"            return JsonUtility.FromJson<{ClassName}>(Json);")
        Lines.append("        }")
        Lines.append("    }")

    Lines.append("}")
    return "\n".join(Lines) + "\n"


def GetSerializedCSharpType(Field: FieldDefinition) -> str:
    Base = {
        "int": "int",
        "long": "long",
        "float": "float",
        "double": "double",
        "bool": "bool",
        "string": "string",
        "Vector2": "Vector2",
        "Vector3": "Vector3",
        "Vector4": "Vector4",
        "Color": "Color",
        "Quaternion": "Quaternion",
        "enum": "string",
    }[Field.ParsedType.BaseName]
    return Base + "[]" * Field.ParsedType.Dimensions


def ToPascal(Value: str) -> str:
    Parts = [Part for Part in NonIdentifierPattern.split(Value) if Part]
    Result = "".join(Part[0].upper() + Part[1:] for Part in Parts)
    if not Result:
        return "Value"
    if Result[0].isdigit():
        return "Value" + Result
    return Result


def GetUniquePascalName(Value: str, ExistingNames: set[str]) -> str:
    Base = ToPascal(Value)
    ExistingPascal = {ToPascal(Name) for Name in ExistingNames}
    Candidate = Base
    Suffix = 2
    while Candidate in ExistingPascal:
        Candidate = Base + str(Suffix)
        Suffix += 1
    return Candidate


def EscapeCSharp(Value: str) -> str:
    return Value.replace("\\", "\\\\").replace('"', '\\"')
