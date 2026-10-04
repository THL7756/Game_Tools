# 通用 Unity 配置表工具设计方案

## 1. 目标和首版边界

`UnityTableTool` 是一个 Windows 本地配置表检测和导出工具，交付内容同时包含：

- `UnityTableTool.exe` Windows GUI 发布程序。
- `TableTool.Core`、`TableTool.Gui`、`TableTool.Serialization` 源码工程。
- `com.company.unity-table-runtime` Unity Runtime Package 源码和包文件。
- 示例 Excel、JSON、bytes 和 C# 输出。
- 中文 Markdown 使用、规则、接入和排错文档。

使用者只需要把 Runtime Package 导入 Unity 项目，并用 `UnityTableTool.exe` 检测和导出符合规范的 Excel 表。

首版 GUI 只负责扫描、规则说明、校验、导出和日志展示，不创建项目、不创建表、不创建字段、不创建枚举，也不修改 Excel 原文件。

首版不交付远程服务器打表、CI/CLI 实现、P4 发布、服务器同步、macOS GUI 或 Linux GUI。Core、GUI 和 Runtime 保留可扩展接口，但这些功能不进入首版验收范围。

## 2. 交付目录

~~~text
UnityTableTool/
├── src/
│   ├── TableTool.Core/
│   ├── TableTool.Gui/
│   └── TableTool.Serialization/
├── UnityRuntimePackage/
│   └── com.company.unity-table-runtime/  # Runtime 源码和 Unity 包
├── samples/
│   ├── Skill.xlsx
│   ├── Item.xlsx
│   ├── Exported/
│   │   ├── Code/
│   │   ├── Json/
│   │   ├── Bytes/
│   │   └── Manifest/
│   └── README.md
├── docs/
│   ├── README.md
│   ├── TableFormat.md
│   ├── FieldRules.md
│   ├── ExportFormats.md
│   ├── RuntimeIntegration.md
│   ├── Troubleshooting.md
│   └── ExtensionPoints.md
├── UnityTableTool.exe
└── README.md
~~~

源码目录和发布目录可以由构建流程生成，但发布目录不能依赖开发机上的 .NET Runtime 或 Unity 编辑器才能启动。

## 3. Excel 表结构

每个 Sheet 或表文件使用固定六行定义区：

~~~text
第 1 行：table: Skill
第 2 行：字段说明
第 3 行：字段类型
第 4 行：字段名
第 5 行：客户端服务器区分
第 6 行：默认值
第 7 行开始：数据行
~~~

示例：

~~~text
table: Skill |          |          |          |
技能说明     | 技能 ID   | 技能名称  | 伤害      | 标签
字段类型     | int       | string    | int       | int()
字段名       | id        | name      | damage    | effects
客户端服务器 |           | c         | cs        | s
默认值       | 0         |           | 0         | 0
数据         | 1001      | 火球      | 120       | 1#2#3
~~~

第 4 行没有字段名的列不进入最终数据。字段说明可以为空，但字段名、字段类型和主键规则仍必须满足校验要求。

字段导出范围：

- 空：客户端和服务器都导出。
- `c`：客户端导出。
- `s`：服务器导出。
- `cs`：客户端和服务器都导出。

空和 `cs` 在内部统一表示为双端字段。

## 4. 注释和测试数据

行标记放在数据行第一列；列标记放在该列第 4 行字段名位置：

~~~text
## 说明       -> 忽略整行或整列，不参与解析和导出
#test        -> 测试行或测试列，参与校验但不进入最终数据
#ceshi       -> 测试行或测试列，参与校验但不进入最终数据
~~~

规则按前缀判断：`##` 优先于 `#test` 和 `#ceshi`。空字段名列直接忽略。测试数据必须通过类型、默认值和主键格式校验，但不会写入 C#、JSON、bytes 或 manifest 的正式数据区。

## 5. 类型和拆分协议

支持无括号标量类型：

~~~text
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
~~~

数组类型使用括号数量表示维度，最多三维。分隔符固定且不可配置：

| 类型 | 拆分顺序 | 示例 |
| --- | --- | --- |
| `int()` | 一级 `#` | `1#2#3` |
| `int()()` | 一级 `#`，二级 `|` | `1|2#3|4` |
| `int()()()` | 一级 `#`，二级 `|`，三级 `_` | `1|2_3#4|5_6` |

类型语法对基础类型通用，例如 `string()`、`float()()` 和 `bool()()()`。解析必须严格按以下层级执行：

~~~text
第一层：#
第二层：|
第三层：_
~~~

低维类型出现高维分隔符时必须报错；数组层级缺失、空元素、元素无法转换或维度不一致时必须报错。首版不支持分隔符转义，字段值不能直接包含 `#`、`|` 或 `_`。

## 6. Core 架构

Core 不依赖 UnityEngine，使用平台无关的中间模型：

~~~text
ProjectDefinition
TableSchema
FieldSchema
TableDocument
TableRow
TableValue
ValidationIssue
ExportArtifact
ExportManifest
~~~

数据流：

~~~text
ExcelCell
  -> RawCellValue
  -> TypedTableValue
  -> TableRow
  -> TableDocument
  -> Code/Json/Binary Artifact
~~~

推荐组件边界：

~~~text
ExcelScanner       扫描文件、Sheet 和表名
SchemaParser       解析六行定义区
ValueParser        解析标量和三层数组
TableValidator     执行结构、类型、主键和标记校验
CodeExporter       生成 C#
JsonExporter       生成 JSON
BinaryExporter     生成 UTB1 bytes
ManifestExporter   生成 manifest
ReportWriter       生成 JSON/HTML/JUnit 报告
~~~

GUI 只调用 Core，不复制导表逻辑。未来 CLI/CI 应调用同一组 Core 接口。

## 7. 导出格式

每张正式表可以独立选择 C#、JSON 和 bytes 输出；manifest 默认生成。

### C#

生成数据类和查询访问类，例如：

~~~csharp
public sealed class SkillData
{
    public int Id;
    public string Name;
    public int Damage;
    public int[] Effects;
}
~~~

### JSON

JSON 使用稳定字段名和稳定数组结构，包含表名、schema hash、行数据和必要的类型元信息。JSON 主要用于调试、人工检查和开发环境加载。

### bytes

bytes 使用自有 `UTB1` 格式，不兼容历史项目 bytes。文件至少包含：

~~~text
magic
formatVersion
tableName
schemaHash
keyType
rowCount
blockCount
indexOffset
indexLength
fileLength
payloadCrc
~~~

相同输入必须生成相同 bytes 和 hash。导出先写 staging 目录，所有表通过校验后再替换正式输出目录。

## 8. Runtime Package

Runtime Package 提供：

~~~text
TableManager
TableStore<TKey,TValue>
PagedTableStore<TKey,TValue>
TableManifest
TableReloadService
TableRuntimeSettings
ITableDataReader
BinaryTableDataReader
JsonTableDataReader
~~~

调用层不区分 JSON 和 bytes：

~~~csharp
var skill = TableManager.Get<SkillData>(1001);
var exists = TableManager.Has<SkillData>(1001);
~~~

格式检测顺序：

1. 优先读取 manifest 中声明的格式。
2. 检测 `UTB1` magic，匹配时使用 bytes reader。
3. 检测合法 JSON 结构，匹配时使用 JSON reader。
4. 都不匹配时报告文件格式错误。

Runtime Package 支持 StreamingAssets、直接文件路径和自定义加载回调，并预留 AssetBundle、Addressables、macOS/Linux 文件加载接口。首版只实现 Windows GUI 和 Unity 常用本地加载路径。

## 9. GUI 范围

GUI 页面：

- 项目和表目录选择。
- Runtime Package 检测。
- Excel 表扫描。
- 表结构和字段检测。
- 字段规则说明。
- 导出格式选择。
- 全量导出和选中表导出。
- 错误、警告和报告查看。

字段规则页面必须展示表头、字段范围、类型语法、三层分隔符、注释、测试数据和输出格式说明。该页面是只读规则说明，不提供创建或编辑表结构功能。

错误至少包含：

~~~text
错误代码
文件路径
Sheet 名
Cell 地址
字段名
行 key
错误原因
修复建议
~~~

## 10. 错误代码

~~~text
TABLE_NAME_INVALID
SCHEMA_ROW_MISSING
FIELD_TYPE_UNKNOWN
FIELD_ARRAY_DIMENSION_INVALID
FIELD_ARRAY_SEPARATOR_INVALID
FIELD_ARRAY_VALUE_INVALID
FIELD_DEFAULT_INVALID
PRIMARY_KEY_MISSING
PRIMARY_KEY_DUPLICATE
FIELD_TARGET_INVALID
SPLIT_SCHEMA_MISMATCH
TEST_ROW_INVALID
SERIALIZE_FAILED
OUTPUT_COMMIT_FAILED
RUNTIME_FORMAT_UNKNOWN
RUNTIME_SCHEMA_MISMATCH
~~~

## 11. 文档要求

必须提供中文 Markdown：

- `README.md`：安装、目录、快速开始。
- `TableFormat.md`：六行表头和表文件组织方式。
- `FieldRules.md`：字段范围、注释、测试标记和类型规则。
- `ExportFormats.md`：C#、JSON、bytes、manifest 说明。
- `RuntimeIntegration.md`：Unity Package 导入和运行时读取。
- `Troubleshooting.md`：错误代码和修复方式。
- `ExtensionPoints.md`：CLI/CI、macOS/Linux 和自定义加载接口。

## 12. 测试和验收

Core 测试覆盖：

- 六行表头解析。
- 空字段名忽略。
- `c`、`s`、`cs` 和空标记。
- `##` 注释行、注释列。
- `#test`、`#ceshi` 测试行、测试列。
- 标量类型转换。
- 一至三维数组分隔和维度校验。
- 重复主键和默认值校验。
- JSON 输出确定性。
- bytes 输出确定性。

Runtime 测试覆盖：

- UTB1 magic 和 schema 校验。
- JSON 读取。
- bytes 读取。
- 自动格式检测。
- `Get`、`Has` 和 `Keys`。
- 损坏文件和格式不匹配时的错误。

验收时，新 Unity 项目只需：

~~~text
导入 Runtime Package
准备 Excel 表
打开 UnityTableTool.exe
检测表结构
选择导出格式
执行导出
在 Unity 中读取 JSON 或 bytes
~~~

不要求远程服务器、CI、GUI 创建表或修改 Runtime Package 源码。

## 13. 后续扩展接口

Core 暴露稳定的导出服务接口，未来 CLI/CI 可以直接复用。GUI 抽象平台启动器，未来可接入 macOS/Linux GUI。Runtime 抽象文件加载器，未来可接入 AssetBundle、Addressables 和远程资源，但这些功能不属于首版实现和验收范围。
