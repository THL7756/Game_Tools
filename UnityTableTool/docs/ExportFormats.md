# 导出格式

## C#

生成可被 Unity 编译的数据类，字段名会转换为 PascalCase。

## JSON

JSON 包含 `tableName`、`schemaHash`、`isSingleton`、`primaryKey`、`fields` 和 `rows`，用于开发环境读取、调试和人工检查。单例表的 `primaryKey` 为 `null`。

## UTB1 bytes

bytes 以 `UTB1` magic 开头，包含版本、表名、schema hash、JSON payload 长度和 CRC。相同输入产生稳定的 bytes。

## Manifest

manifest 记录表名、schema hash、正式行数和导出格式。Runtime Package 优先使用 manifest 中的格式声明。

GUI 和 `ExportService` 使用两个输出位置：数据输出目录下按 `Json`、`Bytes`、`Manifest` 分类保存，代码输出目录直接保存生成的 `*.cs` 文件。
