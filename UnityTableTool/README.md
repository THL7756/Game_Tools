# UnityTableTool

UnityTableTool 是一个 Windows 本地配置表工具：扫描 Excel/CSV/TSV，检查表结构和数据，合并同名分表与引用表，并按客户端/服务器分别导出 JSON 和 C#。项目同时提供一个无 UnityEngine 依赖的 Unity Runtime Package，用于在 Unity 中加载客户端表。

当前发布脚本版本为 `2.4.0`。工具的正式 GUI 流程目前输出 JSON 和 C#；UTB1 `.bytes` 导出器已在序列化库中提供，但 GUI 尚未提供 `.bytes` 勾选项。

## 快速开始

### 1. 启动工具

有发布包时运行 `release/UnityTableTool-2.4.0/UnityTableTool.exe`。开发调试可双击 `run-ui.cmd`，它会还原、编译并启动 WPF 界面。发布使用 `publish.cmd`，发布包是 Windows x64 自包含程序，不需要另外安装 .NET Desktop Runtime。

### 2. 配置项目目录

首次启动默认使用本仓库目录：

```text
Data       配置表输入目录（递归扫描）
Data_c     客户端 JSON 输出目录
Data_s     服务器 JSON 输出目录
Code_c     客户端 C# 输出目录
Code_s     服务器 C# 输出目录
UnityRuntimePackage  Unity Runtime Package 源目录
```

在“设置 / 项目路径”中可以改成其他项目。四个输出目录必须互不相同，且不能放在配置表目录里面。

### 3. 放入 Unity 包

把下面的目录复制到 Unity 项目的 `Packages` 目录：

```text
UnityRuntimePackage/com.company.unity-table-runtime
```

复制后的结构应为：

```text
MyUnityProject/Packages/com.company.unity-table-runtime/package.json
```

也可以不复制，在 Unity 项目的 `Packages/manifest.json` 使用本地路径：

```json
{
  "dependencies": {
    "com.company.unity-table-runtime": "file:../../UnityTableTool/UnityRuntimePackage/com.company.unity-table-runtime"
  }
}
```

`file:` 路径按 `manifest.json` 的实际位置调整，Windows 路径建议使用 `/`。包的最低 Unity 版本声明为 `2021.3`，程序集名称为 `Company.UnityTableRuntime`。

### 4. 导出客户端文件

建议将 GUI 设置为：

```text
客户端数据目录  -> MyUnityProject/Assets/Resources/Tables
客户端代码目录  -> MyUnityProject/Assets/Scripts/GeneratedTables
```

在配置工具中选择表，确认客户端数据和客户端代码输出范围，然后点击“打表”。通过校验后会生成 `<表名>.json` 和 `<表名>Data.cs`。客户端 C# 文件包含数据类；普通表还会包含 `<表名>TableAdapter`，用于把 Runtime 行转换成生成类型。

## 表怎么定义

完整规则见 [docs/表格规则.md](docs/表格规则.md) 和 [docs/表格内容规则.md](docs/表格内容规则.md)，示例表见 [Data/README.md](Data/README.md)。最小规则如下：

- 普通表使用 `table: Skill` 标记逻辑表名，定义区依次描述说明、类型、字段名、端标记和默认值；数据从定义区之后开始。
- 单例表在表头增加 `type:single`，例如 `GlobalConfig`；导出后使用一个 `data` 对象，不要求主键。
- 输入支持 `.xlsx`、`.xls`、`.csv`、`.tsv`，Excel 的每个 Sheet 都会单独解析。
- 字段类型支持 `int`、`long`、`float`、`double`、`bool`、`string`、`enum(...)`、`Vector2/3/4`、`Color`、`Quaternion`、一到三维数组，以及 `localization`、递归 `beans(...)` 和 `map(key)(string)(beans(...))`。
- `localization` 从逻辑表 `localization` 的 `id` 列查键；beans 子字段使用 `字段名#类型`；map 条目使用 `条目;条目`、`key|字符串值#bean字段...`，重复 key 或缺失引用会阻断受影响表及其依赖。
- 字段名以 `<逻辑表名>_id` 结尾时会检查目标表和目标主键值，例如 `item_id` 会引用 `item` 表。
- `c`、`s`、`cs` 或空值决定字段进入客户端、服务器或两端；第 1 行单元格以 `##` 开头或字段名以 `##` 开头表示注释列，整行首个单元格以 `##` 开头表示注释行；`#test_`/`#ceshi` 字段和行只校验、不导出。
- 同名逻辑表可以分布在多个文件或 Sheet 中；合并前必须逐列比较字段名称、顺序、类型、默认值和端标记，冲突会阻断导出并显示双方坐标；选择其中一份时，工具会合并通过 schema 检查的分表并递归加入引用表。
- 单例表按 `id`、`type`、`data`、`cs` 语义列识别字段；即使字段名为 `values`，只要没有注释标记，也会进入单例 `data` 对象。

## 主要检测代码在哪里

表检测流程对应以下文件：

| 文件 | 作用 |
| --- | --- |
| `src/TableTool.Core/Reading/TableFileReader.cs` | 读取 Excel、CSV、TSV，保留文件相对路径和真实 Sheet 名。 |
| `src/TableTool.Core/Parsing/TableSchemaParser.cs` | 解析 `table:`、单例标记、字段类型、字段名、端标记和默认值。 |
| `src/TableTool.Core/Validation/TableValidator.cs` | 检查字段类型、默认值、数组、结构值、主键、单例行等。 |
| `src/TableTool.Core/Validation/TableReferenceValidator.cs` | 检查 `<表名>_id` 的目标表、主键类型和主键值。 |
| `src/TableTool.Core/Parsing/TableDocumentMerger.cs` | 逐列比较名称、顺序、类型、默认值和端标记，合并通过 schema 检查的同名表和分表。 |
| `src/TableTool.Gui/Services/BuildService.cs` | GUI 中执行选择展开、检测、客户端/服务器过滤和导出。 |

## 支持范围

| 目标 | 当前状态 | 说明 |
| --- | --- | --- |
| Unity | 支持 | 官方 Runtime Package、JSON 读取、生成 C# 数据类和客户端适配器。 |
| Unreal Engine | 部分支持 | 可把导出的 JSON 作为普通 JSON 使用；当前没有 UE 插件、C++ 代码生成或 `UDataTable` 适配器。 |
| Godot | 部分支持 | 可自行读取导出的 JSON；当前没有 GDScript/C# Runtime 包或 Godot 资源导入器。 |
| 其他引擎/工具 | 数据层支持 | JSON 是通用交换格式；引擎专用加载器需要自行实现。 |

因此，“支持 UE/Godot”目前只表示可以消费通用 JSON，不表示已有对应引擎的完整运行时集成。Unity 是当前唯一提供官方运行时包和示例的引擎。

## Unity 引用示例

下面示例假设 `Skill.json` 位于 `Assets/Resources/Tables/Skill.json`，`SkillData.cs` 和 `SkillTableAdapter` 位于 Unity 项目脚本目录。`Resources.Load` 的路径不带扩展名。`Name`、`Price` 等成员名取决于表字段，示例中的字段应按实际生成的 `<表名>Data.cs` 调整。

### 加载、按主键读取和读取单例

```csharp
using Company.UnityTableRuntime;
using UnityEngine;

public sealed class TableBootstrap : MonoBehaviour
{
    public static TableManager Tables { get; private set; } = null!;

    private void Awake()
    {
        Tables = new TableManager();

        var skillAsset = Resources.Load<TextAsset>("Tables/Skill");
        if (skillAsset == null)
            throw new System.InvalidOperationException("找不到 Resources/Tables/Skill.json");
        Tables.Load("Skill", skillAsset.bytes);

        var skill = Tables.Get("Skill", "1001", SkillTableAdapter.FromRow);
        Debug.Log($"技能：{skill.Name}");

        var configAsset = Resources.Load<TextAsset>("Tables/GlobalConfig");
        if (configAsset != null)
        {
            Tables.Load("GlobalConfig", configAsset.bytes);
            var config = Tables.GetSingleton("GlobalConfig", GlobalConfigTableAdapter.FromRow);
            Debug.Log($"配置读取完成：{config}");
        }
    }
}
```

如果只需要原始值，也可以不使用生成适配器：`Tables.Get("Skill", "1001")` 返回字段名到字符串值的字典，`Tables.GetSingleton("GlobalConfig")` 返回单例字典。`TableRowView.Read<T>` 会依据 JSON 的 `fields`、默认值和 `arraySeparators` 完成类型转换。

### 循环查询

```csharp
using System.Linq;
using Company.UnityTableRuntime;

var allSkills = TableBootstrap.Tables
    .GetAll("Skill", SkillTableAdapter.FromRow)
    .OrderBy(skill => skill.Id)
    .ToArray();

foreach (var skill in allSkills)
    UnityEngine.Debug.Log($"{skill.Id}: {skill.Name}");
```

### 循环实例化 UI 列表

```csharp
using Company.UnityTableRuntime;
using UnityEngine;

public sealed class SkillListView : MonoBehaviour
{
    [SerializeField] private SkillItemView itemPrefab = null!;
    [SerializeField] private Transform content = null!;

    private void Start()
    {
        var rows = TableBootstrap.Tables.GetAll("Skill", SkillTableAdapter.FromRow);
        foreach (var skill in rows)
        {
            var item = Instantiate(itemPrefab, content);
            item.Bind(skill);
        }
    }
}

public sealed class SkillItemView : MonoBehaviour
{
    [SerializeField] private TMPro.TMP_Text nameText = null!;
    [SerializeField] private TMPro.TMP_Text priceText = null!;

    public void Bind(SkillData skill)
    {
        nameText.text = skill.Name;
        priceText.text = skill.Price.ToString();
    }
}
```

### 从 Addressables、AssetBundle 或自定义资源加载

Runtime 不强制使用 `Resources`。可以把资源加载回调交给 `TableRuntimeSettings`：

```csharp
var manager = new TableManager();
manager.LoadClient("Skill", new TableRuntimeSettings
{
    LoadBytes = tableName => LoadBytesFromAddressables(tableName)
});
```

正式构建建议使用字节数组来源；开发阶段直接读取 JSON 的 `TextAsset.bytes` 即可。当前 GUI 默认生成 JSON，因此 `.bytes` 需要通过 `ExportService` 的 `GenerateBytes` 选项或其他构建脚本生成。

## 文档索引

- [运行时支持矩阵](docs/支持范围.md)
- [表结构](docs/表格规则.md)
- [字段和校验规则](docs/表格内容规则.md)
- [导出格式](docs/ExportFormats.md)
- [Unity Runtime 接入](docs/RuntimeIntegration.md)
- [排错](docs/Troubleshooting.md)
- [项目计划和后续计划](docs/ProjectPlan.md)
- [扩展点](docs/ExtensionPoints.md)

## 构建和测试

```text
dotnet test UnityTableTool.sln -c Release
run-ui.cmd
publish.cmd
```

测试覆盖表解析、注释列/行、重复字段、同名表 schema、复杂类型解析与转义、主键和嵌套引用校验、结构化 JSON/UTB1 Runtime 读取以及导出目录保护。最近一次测试为 22/22 通过，回归样例为 `Data/表格规范.xlsx`，详见 [docs/ProjectPlan.md](docs/ProjectPlan.md)。发布脚本生成 `release/UnityTableTool-2.4.0` 和对应的 Windows x64 压缩包。

## 后续计划

- 提供 CLI/CI 打表入口，支持无界面自动校验和构建流水线。
- 在 GUI 中增加 `.bytes`/UTB1 导出选项，并补充 manifest 或资源清单生成。
- 增加 UE C++/DataTable 和 Godot GDScript/C# 的官方适配层，明确各引擎的资源加载约定。
- 增加 schema 版本、差异比较和变更报告，方便多人协作和发布前审查。
- 扩展远程表源、Addressables/AssetBundle 构建集成，以及跨平台 GUI 支持。

当前未承诺的能力见 [docs/ExtensionPoints.md](docs/ExtensionPoints.md)。
