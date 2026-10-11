# 示例表

`Data` 是示例源表目录，不是导出输出目录。工具会递归扫描其中的 `.xlsx`、`.xls`、`.csv` 和 `.tsv` 文件。

## 示例文件

- `技能表.xlsx`：完整普通表规则，逻辑表名为 `Skill`，包含数组、端标记、注释和测试行示例。
- `道具表.xlsx`：普通表和 `GlobalConfig` 单例表示例；单例表使用 `type:single`，前部语义行包含 `id`、`type`、`data`，`desc` 可选。
- `道具表_1.xlsx`：与 `道具表.xlsx` 同名逻辑表的分表示例。勾选其中任意一份时，工具会自动合并分表。
- `可识别文件夹里面嵌套的表格/道具表_1.xlsx`：递归目录扫描示例。
- `ValidationExamples/`：字段类型、单例、数组、缺少定义、结构冲突和未知类型等校验样例。

## 规则入口

表头和单例定义见 [../docs/表格规则.md](../docs/表格规则.md)，字段类型、数组、端标记和引用见 [../docs/表格内容规则.md](../docs/表格内容规则.md)。工具不会把没有 `table:` 或定义区不完整的文件当作正式表，通常只记录信息日志。

## 运行结果

GUI 默认读取本目录，导出到仓库根目录的 `Data_c`、`Data_s`、`Code_c`、`Code_s`。客户端 JSON 可以直接复制到 Unity 的 `Assets/Resources/Tables`；客户端 C# 可以复制到 Unity 的脚本目录。GUI 当前导出 JSON 和 C#，`.bytes` 需要通过 Serialization API 单独生成。
