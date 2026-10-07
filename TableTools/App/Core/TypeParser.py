import math
import re
from typing import Any

from App.Core.Models import ParsedType


class TypeParseError(ValueError):
    pass


ArrayTypePattern = re.compile(r"^(.*?)(\(\))$")
EnumTypePattern = re.compile(r"^enum\((.+)\)$", re.IGNORECASE)
IntegerPattern = re.compile(r"^[+-]?\d+$")
ScalarTypeNames = {"int", "long", "float", "double", "bool", "string"}
StructComponentCounts = {
    "Vector2": 2,
    "Vector3": 3,
    "Vector4": 4,
    "Color": 4,
    "Quaternion": 4,
}


def ParseFieldType(TypeText: str) -> ParsedType:
    RawText = (TypeText or "").strip()
    if not RawText:
        raise TypeParseError("字段类型为空")

    WorkingText = RawText
    Dimensions = 0
    while True:
        Match = ArrayTypePattern.match(WorkingText)
        if not Match:
            break
        WorkingText = Match.group(1).strip()
        Dimensions += 1
        if Dimensions > 3:
            raise TypeParseError("数组最多支持三维")

    EnumMatch = EnumTypePattern.match(WorkingText)
    if EnumMatch:
        Values = tuple(Value.strip() for Value in EnumMatch.group(1).split("|"))
        if any(not Value for Value in Values):
            raise TypeParseError("enum 配置中存在空值")
        if len(set(Values)) != len(Values):
            raise TypeParseError("enum 配置中存在重复值")
        return ParsedType(RawText, "enum", Dimensions, Values)

    BaseName = WorkingText
    if BaseName.lower() == "str":
        raise TypeParseError("未知字段类型 str，请使用 string")
    if BaseName in StructComponentCounts:
        return ParsedType(RawText, BaseName, Dimensions)
    if BaseName.lower() in ScalarTypeNames:
        return ParsedType(RawText, BaseName.lower(), Dimensions)
    raise TypeParseError(f"未知字段类型 {BaseName}")


def ParseValue(RawValue: Any, FieldType: ParsedType, EscapeCharacter: str = "\\") -> Any:
    Text = "" if RawValue is None else str(RawValue).strip()
    if FieldType.Dimensions == 0:
        return ParseScalar(Text, FieldType)
    return ParseArray(Text, FieldType, 1, EscapeCharacter)


def ParseScalar(Text: str, FieldType: ParsedType) -> Any:
    BaseName = FieldType.BaseName
    if BaseName == "string":
        return Text
    if BaseName == "bool":
        Normalized = Text.lower()
        if Normalized in {"true", "1"}:
            return True
        if Normalized in {"false", "0"}:
            return False
        raise TypeParseError("bool 仅接受 true、false、1、0")
    if BaseName in {"int", "long"}:
        if not IntegerPattern.fullmatch(Text):
            raise TypeParseError(f"{BaseName} 格式错误")
        Number = int(Text)
        if BaseName == "int" and not -(2**31) <= Number < 2**31:
            raise TypeParseError("int 超出范围")
        if BaseName == "long" and not -(2**63) <= Number < 2**63:
            raise TypeParseError("long 超出范围")
        return Number
    if BaseName in {"float", "double"}:
        try:
            Number = float(Text)
        except ValueError as Error:
            raise TypeParseError(f"{BaseName} 格式错误") from Error
        if not math.isfinite(Number):
            raise TypeParseError(f"{BaseName} 不能是 NaN 或 Infinity")
        return Number
    if BaseName in StructComponentCounts:
        return ParseStruct(Text, BaseName)
    if BaseName == "enum":
        if Text not in FieldType.EnumValues:
            raise TypeParseError(f"enum 值 {Text!r} 不在配置中")
        return Text
    raise TypeParseError(f"未知字段类型 {BaseName}")


def ParseStruct(Text: str, BaseName: str) -> dict[str, float]:
    Parts = [Part.strip() for Part in Text.split(",")]
    Expected = StructComponentCounts[BaseName]
    AllowedCounts = {3, 4} if BaseName == "Color" else {Expected}
    if len(Parts) not in AllowedCounts:
        ExpectedText = "3 或 4" if BaseName == "Color" else str(Expected)
        raise TypeParseError(f"{BaseName} 需要 {ExpectedText} 个分量")
    Values: list[float] = []
    for Part in Parts:
        try:
            Number = float(Part)
        except ValueError as Error:
            raise TypeParseError(f"{BaseName} 分量格式错误") from Error
        if not math.isfinite(Number):
            raise TypeParseError(f"{BaseName} 分量不能是 NaN 或 Infinity")
        Values.append(Number)

    if BaseName == "Color":
        if len(Values) == 3:
            Values.append(1.0)
        return {"r": Values[0], "g": Values[1], "b": Values[2], "a": Values[3]}
    if BaseName == "Quaternion":
        return {"x": Values[0], "y": Values[1], "z": Values[2], "w": Values[3]}
    return {"x": Values[0], "y": Values[1], "z": Values[2], "w": Values[3]} if BaseName == "Vector4" else {"x": Values[0], "y": Values[1], "z": Values[2]} if BaseName == "Vector3" else {"x": Values[0], "y": Values[1]}


def ParseArray(Text: str, FieldType: ParsedType, Level: int, EscapeCharacter: str) -> list[Any]:
    Separators = {1: ["#"], 2: ["|", "#"], 3: [";", "|", "#"]}[FieldType.Dimensions]
    Separator = Separators[Level - 1]
    Pieces = SplitEscaped(Text, Separator, EscapeCharacter)
    if not Pieces:
        raise TypeParseError("数组不能为空")
    if any(Piece == "" for Piece in Pieces):
        raise TypeParseError("数组元素不能为空")
    if Level == FieldType.Dimensions:
        return [ParseScalar(Piece, FieldType) for Piece in Pieces]
    return [ParseArray(Piece, FieldType, Level + 1, EscapeCharacter) for Piece in Pieces]


def SplitEscaped(Text: str, Separator: str, EscapeCharacter: str) -> list[str]:
    Result: list[str] = []
    Builder: list[str] = []
    Index = 0
    while Index < len(Text):
        Character = Text[Index]
        if Character == EscapeCharacter and Index + 1 < len(Text):
            Builder.append(Text[Index + 1])
            Index += 2
            continue
        if Character == Separator:
            Result.append("".join(Builder))
            Builder.clear()
        else:
            Builder.append(Character)
        Index += 1
    Result.append("".join(Builder))
    return Result
