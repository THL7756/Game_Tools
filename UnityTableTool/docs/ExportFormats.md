# 导出格式

## 导出模式

每次打表可以选择：

- **只打客户端**：结果写入 `Data_c`。
- **只打服务器**：结果写入 `Data_s`。

`c` 字段只进入客户端结果，`s` 字段只进入服务器结果，`cs` 或空值字段进入当前所选端的结果。

## 目录布局

- `Data_c`：客户端 JSON 和 bytes。
- `Data_s`：服务器 JSON 和 bytes。
- `Code`：独立的 C# 输出目录。

每个数据目录内部直接平铺保存同名表的 `.json` 和 `.bytes` 文件，不再创建 `Json`、`Bytes`、`Manifest` 子目录，也不生成 manifest。生成的 `*Data.cs` 不放入示例源表目录 `Data`。

## C#

生成可被 Unity 编译的数据类，字段名转换为 PascalCase。代码文件保存在 `Code`。

## JSON

JSON 包含 `tableName`、`schemaHash`、`isSingleton`、`primaryKey`、`fields` 和 `rows`，用于开发环境读取、调试和人工检查。单例表的 `primaryKey` 为 `null`。

JSON 的 `fields` 不包含 `target`、客户端或服务器标记。字段是否属于当前数据由导出模式和所在目录决定。

## UTB1 bytes

bytes 以 `UTB1` magic 开头，包含版本、表名、schema hash、JSON payload 长度和 CRC。相同输入产生稳定的 bytes。

bytes 的 payload 与对应端的 JSON 数据一致，不记录字段的客户端或服务器标记。

## 选择和关联表

表列表、搜索和最近记录都以文件名标识表。勾选单张或多张表后，工具会按关联关系自动补齐需要一起打表的关联表，即使关联表没有被勾选也会参与本次导出。
