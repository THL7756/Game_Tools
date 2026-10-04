# 导出格式

## C#

生成可被 Unity 编译的数据类，字段名会转换为 PascalCase。

## JSON

JSON 包含 `tableName`、`schemaHash`、`primaryKey`、`fields` 和 `rows`，用于开发环境读取、调试和人工检查。

## UTB1 bytes

bytes 以 `UTB1` magic 开头，包含版本、表名、schema hash、JSON payload 长度和 CRC。相同输入产生稳定的 bytes。

## Manifest

manifest 记录表名、schema hash、正式行数和导出格式。Runtime Package 优先使用 manifest 中的格式声明。
