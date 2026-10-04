# 排错

| 错误 | 处理方式 |
| --- | --- |
| `FIELD_TYPE_UNKNOWN` | 检查第 3 行类型名称和单元格值。 |
| `FIELD_ARRAY_SEPARATOR_INVALID` | 检查 `#`、`|`、`;` 是否符合字段维度。 |
| `PRIMARY_KEY_DUPLICATE` | 修改重复的第一字段值。 |
| `RUNTIME_FORMAT_UNKNOWN` | 确认文件是有效 JSON 或以 `UTB1` 开头的 bytes。 |
| `RUNTIME_SCHEMA_MISMATCH` | 重新导出当前端的 `Data_c` 或 `Data_s`，以及需要的 `Code`。 |
| `SINGLETON_ROW_COUNT_INVALID` | 单例表源表可以有多行常量字段，但这些字段必须聚合成一个正式对象；该错误表示解析结果没有得到恰好一个对象，不是限制源表只能录入一行。 |
| 表列表没有结果 | 检查表根目录和搜索关键字；列表和搜索使用文件名。 |
| 没有找到最近打表 | 最近记录按文件名保存，确认文件名没有变化。 |
| 关联表未勾选但仍被导出 | 这是预期行为；关联表会随选中表自动加入本次打表。 |
| JSON/bytes 中找不到端别字段 | 这是预期行为；端别只由只打客户端/只打服务器模式及 `Data_c`/`Data_s` 目录决定。 |

GUI 导出的错误会显示文件、Sheet、Cell、字段名、行 key 和修复建议。校验存在错误时不会提交输出。
