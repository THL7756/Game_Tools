# Unity Runtime 接入

本包位于 `UnityRuntimePackage/com.company.unity-table-runtime`，包名为 `com.company.unity-table-runtime`，最低 Unity 版本声明为 `2021.3`，程序集为 `Company.UnityTableRuntime`。Runtime 不依赖 `UnityEngine`，只有生成的 `Vector`/`Color`/`Quaternion` 数据类在需要时才引用 `UnityEngine`。

## 安装方式

### 复制到 Packages

把整个目录复制到 Unity 项目：

```text
UnityRuntimePackage/com.company.unity-table-runtime
    -> MyUnityProject/Packages/com.company.unity-table-runtime
```

确认 `MyUnityProject/Packages/com.company.unity-table-runtime/package.json` 存在，重新打开 Unity 后 Package Manager 会识别该包。

复制前请确认包目录不包含 .NET 构建目录：`Runtime/obj` 和 `Runtime/bin` 必须删除。包内源码使用 C# 9 可用语法，Unity 会递归编译包内的 `.cs` 文件；如果复制了旧生成文件，或使用了未转换的文件范围命名空间/集合表达式，就可能出现 `CS8773`。遇到该错误时，关闭 Unity，重新复制已清理的源包，再重新打开项目。

### 使用 manifest 本地路径

在 `MyUnityProject/Packages/manifest.json` 的 `dependencies` 中加入：

```json
"com.company.unity-table-runtime": "file:../../UnityTableTool/UnityRuntimePackage/com.company.unity-table-runtime"
```

路径相对于 `manifest.json` 所在目录，Windows 路径建议使用 `/`。工具本身的“Unity 包目录”设置应指向包含 `com.company.unity-table-runtime` 的目录，例如仓库中的 `UnityRuntimePackage`，不要只指向包内的 `Runtime` 目录。

## Unity Editor 轻量打表工具

安装包后可在 Unity 菜单 `Tools/UnityTableTool/Table Tool` 打开轻量工具。先在项目中创建设置资产：

```text
Assets -> Create -> UnityTableTool -> Table Tool Settings
```

设置资产保存源表目录、客户端/服务器数据目录、客户端/服务器代码目录、输出目标和数组分隔符。源表目录支持 `.xlsx`、`.xls`、`.csv` 和 `.tsv`，会递归保留 Excel 的真实 Sheet 名。

窗口中的“刷新表列表”才会扫描源表；“开始打表”会在必要时先刷新一次，然后执行与桌面 GUI 相同的字段类型、数组、主键、单例、同名表、localization、beans、map 和跨表引用校验。错误会阻止导出，并在 Unity Console 中保留源文件、Sheet、行列和字段信息。窗口重绘、切换焦点或重新打开窗口不会自动重复扫描。

Excel 读取依赖和 `.xls` 所需的代码页程序集随包放在 `Editor/Plugins`，只参与 Unity Editor 编译，不会进入游戏 Runtime 程序集；JSON 读取依赖放在 `Runtime/Plugins`，供 Runtime 的 JSON/UTB1 读取使用。

## 输出文件放置

建议把 UnityTableTool 的客户端数据目录设为 `Assets/Resources/Tables`，客户端代码目录设为 `Assets/Scripts/GeneratedTables`：

```text
Assets/Resources/Tables/Skill.json
Assets/Resources/Tables/GlobalConfig.json
Assets/Scripts/GeneratedTables/SkillData.cs
Assets/Scripts/GeneratedTables/GlobalConfigData.cs
```

服务器 JSON 和服务器 C# 放在 Unity 项目之外。GUI 当前默认生成 JSON；如果由序列化库调用 `ExportService` 并启用 `GenerateBytes`，也可以生成 `.bytes`，Runtime 会自动识别两种格式。

## 加载和查询

最简单的开发期接入是使用 `Resources.Load<TextAsset>`：

```csharp
using Company.UnityTableRuntime;
using UnityEngine;

public sealed class TableBootstrap : MonoBehaviour
{
    public static TableManager Tables { get; private set; } = null!;

    private void Awake()
    {
        Tables = new TableManager();
        var asset = Resources.Load<TextAsset>("Tables/Skill");
        if (asset == null)
            throw new System.InvalidOperationException("找不到 Skill.json");

        Tables.Load("Skill", asset.bytes);
        var skill = Tables.Get("Skill", "1001", SkillTableAdapter.FromRow);
        Debug.Log(skill.Name);
    }
}
```

客户端生成的 `SkillData.cs` 会包含 `SkillData` 和 `SkillTableAdapter`。适配器只负责把 `TableRowView` 转换为生成类型；表的字段定义、默认值和数组分隔符来自 JSON 的 `fields` 和 `arraySeparators`。

常用查询 API：

```csharp
var raw = Tables.Get("Skill", "1001");
var typed = Tables.Get("Skill", "1001", SkillTableAdapter.FromRow);
var all = Tables.GetAll("Skill", SkillTableAdapter.FromRow);
var exists = Tables.Has("Skill", "1001");
var config = Tables.GetSingleton("GlobalConfig", GlobalConfigTableAdapter.FromRow);
```

复杂字段也通过同一行读取入口转换。生成的表数据类会包含按字段命名的嵌套类型，例如 `LabelValue`、`ItemBundleBean` 和 `ItemMapEntry`；map 字段生成 `Dictionary<TKey, Entry>`。如果只使用 Runtime 原始 API，可以读取共享值对象：

```csharp
using Company.UnityTableRuntime.Shared;

var localizationAsset = Resources.Load<TextAsset>("Tables/localization");
if (localizationAsset == null)
    throw new System.InvalidOperationException("找不到 localization.json");
Tables.Load("localization", localizationAsset.bytes);

var label = Tables.Get("Items", "10001", row => row.Read<RuntimeLocalizationValue>("label"));
var map = Tables.Get("Items", "10001", row => row.Read<RuntimeMapValue>("item_map"));
var text = Tables.GetLocalizationText(label.Key, "zh_CN") ?? label.Key;
```

业务表的多语言 JSON 只保存键，例如 `"label": "greeting"`；语言文本只保存在 `localization.json`。将语言表以名称 `"localization"` 加载一次后，使用 `GetLocalizationText(key, language)` 查询；底层复用加载时建立的主键和字段字典，不逐行遍历。语言代码不区分大小写，key 与普通主键一样区分大小写。缺语言或空文本返回 `null`，语言列默认值仍生效；未加载表或 key 不存在时抛出 `KeyNotFoundException`。不自动回退语言，示例中的 `?? label.Key` 是调用方的显示策略。

`RuntimeLocalizationValue` 和生成的多语言值类型只包含 `Key`。切换语言时传入新的语言代码，无需重新读取业务表；重载、卸载或清空语言表会立即影响后续文本查询。旧的多语言对象 JSON 不再支持，升级时需同步更新 Runtime 包、重新导出的 JSON/UTB1 和生成 C#。

不存在的表、主键或单例会抛出 `KeyNotFoundException`；需要无异常判断时先使用 `Has`。`GetAll` 返回生成类型列表，显示 UI 时可以直接遍历并实例化预制体：

```csharp
foreach (var skill in Tables.GetAll("Skill", SkillTableAdapter.FromRow))
{
    var item = Instantiate(itemPrefab, content);
    item.Bind(skill);
}
```

## 自定义资源加载

正式项目可以把数据放进 Addressables、AssetBundle 或自定义缓存：

```csharp
var manager = new TableManager();
manager.LoadClient("Skill", new TableRuntimeSettings
{
    LoadBytes = tableName => LoadBytesFromAddressables(tableName)
});
```

`LoadBytes` 必须返回对应表的完整 JSON 或 UTB1 字节数组。`RuntimeFormat.ReadAuto` 会先识别 UTB1 magic，再识别 JSON；也可以通过 `manifestFormat` 显式指定 `"json"` 或 `"bytes"`。

## Runtime 的职责边界

Runtime 只负责读取已经打好的客户端数据、主键/单例查询和字段类型转换，不负责 Excel 解析、表结构检测、分表合并、引用检查或生成代码。这些规则由工具的 Core 和 Serialization 项目在打表阶段完成。
