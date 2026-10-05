# 导出格式

## 导出端

客户端和服务器可以独立选择，也可以同时选择：

- 客户端数据写入 `Data_c`。
- 服务器数据写入 `Data_s`。
- 同时导出时，C# 代码写入 `Code/Client` 和 `Code/Server`。
- 只导出一端时，代码直接写入 `Code`。

字段标记 `c`、`s`、`cs` 或空值只决定字段进入哪一端，不写入最终 JSON 或 bytes。

## 数据格式

JSON 和 bytes 在 GUI 中只能二选一。JSON 和 bytes 都直接平铺在对应数据目录中，不生成 manifest。

### JSON

普通表包含 `tableName`、`schemaHash`、`isSingleton`、`primaryKey`、`fields` 和 `rows`。

单例表包含一个 `data` 对象，`primaryKey` 为 `null`，运行时通过 `GetSingleton` 读取。

`formatVersion` 表示 JSON 协议版本，`schemaHash` 表示当前字段结构指纹。它们是协议元数据，不是业务字段。

### UTB1 bytes

bytes 以 `UTB1` magic 开头，包含格式版本、表名、schema hash、payload 长度和 CRC。正式运行推荐使用 bytes，开发调试可使用 JSON。

## C# 代码

生成的 `*Data.cs` 是字段声明类，字段名转换为 PascalCase。代码输出目录与数据目录分离，避免校验报告或生成代码污染源表目录。

## 输出流程

点击导出后，工具按以下顺序执行：

1. 扫描并解析源表。
2. 展开分表和关联表。
3. 合并同逻辑表并校验。
4. 写入 staging 目录。
5. 校验通过后替换目标输出目录。
6. 成功后取消 GUI 中所有勾选并记录最近文件。
