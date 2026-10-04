# 示例表

`Skill.xlsx` 展示了完整规则：

- `int()` 使用 `#` 拆分。
- `int()()` 使用外层 `|`、内层 `#` 拆分。
- `int()()()` 使用外层 `;`、中层 `|`、内层 `#` 拆分。
- `c`、`s`、`cs` 和空标记。
- `##` 注释列和注释行。
- `#test` 测试列和测试行。

`GlobalConfig.xlsx` 展示单例表：第一行带 `type:single`，语义行包含 `id`、`type`、`data`，`desc` 可选，正式数据最终生成一个单例对象。

示例使用 Excel `.xlsx`，工具仍同时支持 `.xlsx`、`.xls`、`.csv`、`.tsv` 输入。

GUI 选择 `samples` 作为表根目录，再分别选择 `samples/Exported/Data` 和 `samples/Exported/Code`。数据目录包含 `Json`、`Bytes`、`Manifest`，代码目录直接包含生成的 `*.cs`。
