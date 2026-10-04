# 排错

| 错误 | 处理方式 |
| --- | --- |
| `FIELD_TYPE_UNKNOWN` | 检查第 3 行类型名称和单元格值。 |
| `FIELD_ARRAY_SEPARATOR_INVALID` | 检查 `#`、`|`、`_` 是否符合字段维度。 |
| `PRIMARY_KEY_DUPLICATE` | 修改重复的第一字段值。 |
| `RUNTIME_FORMAT_UNKNOWN` | 确认文件是有效 JSON 或以 `UTB1` 开头的 bytes。 |
| `RUNTIME_SCHEMA_MISMATCH` | 重新导出 C#、JSON、bytes 和 manifest。 |

GUI 导出的错误会显示文件、Sheet、Cell、字段名、行 key 和修复建议。
