# 通用 Unity 配置表工具设计方案

## 1. 目标和范围

UnityTableTool 是 Windows 本地配置表检测和导出工具，提供 Core、Windows GUI、Unity Runtime Package、示例源表和中文文档。

首版 GUI 负责扫描、文件名表列表、搜索、最近打表、勾选、端别选择、规则说明、校验、关联表展开、导出和日志展示，不修改 Excel 原文件。

Diff、远程服务器打表、CI/CLI、P4、服务器同步、macOS GUI 和 Linux GUI 不属于本方案。

## 2. 目录

```text
Game_Tools/
└── UnityTableTool/
    ├── src/
    ├── UnityRuntimePackage/
    ├── Data/          # 示例源表
    ├── Data_c/        # 客户端数据输出
    ├── Data_s/        # 服务器数据输出
    ├── Code/          # C# 输出
    ├── docs/
    └── UnityTableTool.sln
```

`Data` 只放源表，不放导出结果。`Code` 与示例源表目录分离。

## 3. 输入和输出路径

GUI 提供一个递归表根目录，读取 `.xlsx`、`.xls`、`.csv`、`.tsv`。

打表时选择：

- 只打客户端：输出到 `Data_c`。
- 只打服务器：输出到 `Data_s`。

每个输出目录中的 `.json` 和 `.bytes` 直接平铺，不再创建 `Json`、`Bytes`、`Manifest` 子目录，不生成 manifest。C# 单独输出到 `Code`。

JSON 和 bytes 数据不记录字段属于客户端还是服务器；端别由本次导出模式和输出目录决定。字段仍可在源表头中使用 `c`、`s`、`cs` 进行范围控制：

- `c`：只进入客户端结果。
- `s`：只进入服务器结果。
- `cs` 或空：进入当前所选端结果。

## 4. 表列表和选择

- 表列表以文件名展示和标识表。
- 搜索按文件名过滤。
- 最近打表按文件名记录最近使用项。
- 可以勾选一张或多张表。
- 勾选表的关联表即使未勾选，也会自动加入本次导出。
- 扫描结果仍按 `table:` 名称合并同名分表，再进行 schema 校验；同名但 schema 不一致时禁止导出。

## 5. Excel 表结构

每个 Sheet 或表文件使用固定六行定义区：

```text
第 1 行：table: Skill
第 2 行：字段说明
第 3 行：字段类型
第 4 行：字段名
第 5 行：客户端服务器区分
第 6 行：默认值
第 7 行开始：数据行
```

第一行可以包含 `type:single` 元数据。单例表通过 `id`、`type`、`data` 语义列识别，`desc` 可选且不导出。源表可以有多行常量字段，每行定义一个字段，所有字段最终聚合成一个正式对象并使用 `GetSingleton` 读取；“一条”描述的是输出对象，不是源表行数。

字段名为空或以 `##` 开头的列不进入正式输出；`#test` 和 `#ceshi` 参与校验但不导出。

## 6. 类型和拆分协议

标量类型：

```text
int long float double bool string
Vector2 Vector3 Vector4 Color Quaternion
```

数组最多三维：

| 类型 | 拆分层级 | 示例 |
| --- | --- | --- |
| `int()` | `#` | `1#2#3` |
| `int()()` | 外层 `|`、内层 `#` | `1#2|3#4` |
| `int()()()` | 外层 `;`、中层 `|`、内层 `#` | `1|2#3;4|5#6` |

低维类型出现高维分隔符、数组出现空元素、元素无法转换或维度不一致时必须报错。首版不支持分隔符转义。

## 7. Core

Core 不依赖 UnityEngine，提供表读取、schema 解析、值解析、校验、同名分表合并和导出服务。导出服务接收一组逻辑表，按端别选择生成 JSON、UTB1 bytes 或 C#。

## 8. 导出格式

- C#：生成 Unity 可编译的数据类，保存到 `Code`。
- JSON：包含表名、单例标记、schema hash、字段和正式行；字段不包含端别标记。
- bytes：使用 `UTB1` 格式，包含版本、表名、schema hash、JSON payload 长度和 CRC；payload 与对应端 JSON 一致。
- 不生成 manifest。

相同输入必须生成相同 bytes 和 hash。任何表校验失败都不能提交输出。

## 9. Runtime Package

Runtime Package 支持 JSON 和 UTB1 bytes 自动识别：

1. 检测 `UTB1` magic。
2. 检测合法 JSON 结构。
3. 都不匹配时报告错误。

普通表支持 `Get`、`Has`、`Keys`；单例表支持 `GetSingleton`。客户端从 `Data_c` 加载，服务器从 `Data_s` 加载，不依赖 manifest 或数据内的端别字段。

## 10. GUI

GUI 页面包括：

- 表根目录选择。
- 文件名表列表、文件名搜索和最近打表。
- 单表/多表勾选和关联表自动展开。
- 只打客户端/只打服务器选择。
- 表结构和字段检测。
- 字段规则说明。
- JSON、bytes、C# 选择。
- 校验、导出和错误、警告、报告查看。

## 11. 文档

必须提供：

- `README.md`：快速开始和目录。
- `TableFormat.md`：六行表头、单例表和目录扫描。
- `FieldRules.md`：字段范围、注释、测试标记、端别选择和拆分规则。
- `ExportFormats.md`：C#、JSON、bytes、端别目录和关联表。
- `RuntimeIntegration.md`：Runtime Package 接入。
- `Troubleshooting.md`：错误和修复。
- `ExtensionPoints.md`：Diff、CLI/CI、跨平台和自定义加载接口。

## 12. 验收流程

```text
选择表根目录 Data
扫描并按文件名搜索/选择表
必要时查看最近打表
选择只打客户端或只打服务器
校验并导出
在 Data_c 或 Data_s 获取平铺的 JSON/bytes
在 Code 获取生成的 C#
```

同名分表在校验前合并为一个逻辑表；关联表自动随选中表导出；输出不包含 manifest，也不记录字段端别。
