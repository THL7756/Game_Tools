# 通用 Unity 配置表工具设计方案

## 1. 目标和范围

UnityTableTool 是 Windows 本地配置表检测和导出工具。首版交付 Core、Windows GUI、Runtime Package 源码、可发布 GUI 程序、示例表和中文使用文档。

首版 GUI 只负责扫描、规则说明、校验、导出和日志展示，不创建项目、表、字段或枚举，也不修改 Excel 原文件。

Diff、远程服务器打表、CI/CLI、P4、服务器同步、macOS GUI 和 Linux GUI 不属于首版。Diff 后续作为同级独立工具接入，Core 保留可复用接口。

## 2. 目录

```text
Game_Tools/
├── UnityTableTool/
│   ├── src/
│   │   ├── TableTool.Core/
│   │   ├── TableTool.Gui/
│   │   ├── TableTool.Serialization/
│   │   └── TableTool.Tests/
│   ├── UnityRuntimePackage/
│   ├── samples/
│   ├── docs/
│   ├── build/
│   ├── release/
│   └── UnityTableTool.sln
└── UnityTableToolDiff/       # 后续独立工具
```

## 3. 输入和输出路径

GUI 只提供两个路径：

- 一个表根目录，递归扫描所有子目录中的 `.xlsx`、`.xls`、`.csv`、`.tsv`。
- 一个输出数据目录写入 JSON、bytes 和 manifest，一个输出代码目录写入 C#。

用户只需要配置表根目录、输出数据目录和输出代码目录。数据目录内部的 `Json`、`Bytes`、`Manifest` 子目录由工具固定管理，代码目录直接保存生成的 C# 文件。

同一表根目录中的所有合法表一次扫描、校验和提交。输入子目录只用于组织和来源定位，不改变表名规则。

## 4. Excel 表结构

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

第一行可以附加单例表元数据：

```text
table: GlobalConfig
type:single
```

单例表通过 `id`、`type`、`data` 语义列识别，`desc` 可选且不导出，列顺序可以调整。单例表最终生成一条正式数据对象，测试行不计入单例行数，运行时通过 `GetSingleton` 读取，不要求主键字段。

第 4 行没有字段名的列不进入最终数据。

字段范围：

- 空或 `cs`：客户端和服务器。
- `c`：客户端。
- `s`：服务器。

## 5. 注释和测试

行标记放在数据行第一列，列标记放在第 4 行字段名位置：

```text
## 说明       -> 忽略整行或整列
#test         -> 参与校验但不导出
#ceshi        -> 参与校验但不导出
```

`##` 优先于 `#test` 和 `#ceshi`。测试数据必须通过类型、默认值和格式校验，但不会写入正式 C#、JSON、bytes 或 manifest 数据区。

## 6. 类型和拆分协议

标量类型：

```text
int
long
float
double
bool
string
Vector2
Vector3
Vector4
Color
Quaternion
```

数组最多三维，分隔符固定：

| 类型 | 拆分层级 | 示例 |
| --- | --- | --- |
| `int()` | 内层 `#` | `1#2#3` |
| `int()()` | 外层 `|`，内层 `#` | `1#2|3#4` |
| `int()()()` | 外层 `;`，中层 `|`，内层 `#` | `1|2#3;4|5#6` |

该规则对所有基础类型通用，例如 `string()`、`float()()` 和 `bool()()()`。低维类型出现高维分隔符、数组出现空元素、元素无法转换或维度不一致时必须报错。首版不支持分隔符转义，值不能直接包含 `#`、`|` 或 `;`。

## 7. Core

Core 不依赖 UnityEngine，提供：

```text
ExcelScanner
SchemaParser
ValueParser
TableValidator
CodeExporter
JsonExporter
BinaryExporter
ManifestExporter
ReportWriter
```

Core 输入一个表根目录，输出一组 `TableDocument`，导出服务接收整组文档并分别提交到输出数据目录和输出代码目录。

## 8. 导出格式

每张正式表可以选择 C#、JSON 和 bytes，manifest 默认生成。

- C#：生成 Unity 可编译的数据类。
- JSON：包含表名、单例标记、schema hash、字段和正式行。
- bytes：使用自有 `UTB1` 格式，包含版本、表名、schema hash、payload 长度和 CRC。
- manifest：记录表名、是否单例、schema hash、正式行数和输出格式。

相同输入必须生成相同 bytes 和 hash。任何表校验失败都不能提交半套新输出。

## 9. Runtime Package

Runtime Package 支持 JSON 和 UTB1 bytes 自动识别：

1. 优先使用 manifest 声明。
2. 检测 `UTB1` magic。
3. 检测合法 JSON 结构。
4. 都不匹配时报告错误。

普通表支持 `Get`、`Has`、`Keys`；单例表支持 `GetSingleton`。Runtime Package 暂不纳入当前 Unity 验收，但源码和接口必须保留。

## 10. GUI

GUI 页面：

- 表根目录选择。
- 输出数据目录和输出代码目录选择。
- 递归扫描结果。
- 表结构和字段检测。
- 字段规则说明。
- JSON、bytes、C# 选择。
- 全部表一次导出。
- 错误、警告和报告查看。

GUI 不包含 Diff 页面。Diff 后续放在 `Game_Tools/UnityTableToolDiff`，独立发布和接入。

## 11. 文档

必须提供：

- `README.md`：快速开始和目录。
- `TableFormat.md`：六行表头、单例表和目录扫描。
- `FieldRules.md`：字段范围、注释、测试标记和拆分规则。
- `ExportFormats.md`：C#、JSON、bytes、manifest。
- `RuntimeIntegration.md`：Runtime Package 接入。
- `Troubleshooting.md`：错误和修复。
- `ExtensionPoints.md`：Diff、CLI/CI、跨平台和自定义加载接口。

## 12. 测试和验收

Core 必须覆盖：

- 六行表头。
- 单例表元数据和单行约束。
- 空字段名。
- `c`、`s`、`cs`。
- `##`、`#test`、`#ceshi`。
- 一至三维数组新拆分规则。
- 递归扫描四种文件扩展名。
- 重复主键、默认值和确定性导出。

Runtime 源码测试覆盖 JSON、bytes、自动识别、CRC 和单例读取；Unity Package 本版暂不做 Unity 工程验收。

验收流程：

```text
选择一个表根目录
选择一个输出数据目录和一个输出代码目录
扫描并校验
一次导出所有表
在数据目录获取 JSON、bytes、manifest，在代码目录获取 C#
```
