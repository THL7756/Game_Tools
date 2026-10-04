# 示例表

`Skill.csv` 展示了完整规则：

- `int()` 使用 `#` 拆分。
- `int()()` 使用 `#` 后再使用 `|` 拆分。
- `int()()()` 使用 `#`、`|`、`_` 三层拆分。
- `c`、`s`、`cs` 和空标记。
- `##` 注释列和注释行。
- `#test` 测试列和测试行。

CSV 与 XLSX 使用相同的六行表头。实际项目可以把这些示例复制到 Excel 后另存为 `.xlsx`。

导出目录由 GUI 选择，默认输出到 `samples/Exported`，包含 `Code`、`Json`、`Bytes` 和 `Manifest` 子目录。
