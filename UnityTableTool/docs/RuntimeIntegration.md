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

### 使用 manifest 本地路径

在 `MyUnityProject/Packages/manifest.json` 的 `dependencies` 中加入：

```json
"com.company.unity-table-runtime": "file:../../UnityTableTool/UnityRuntimePackage/com.company.unity-table-runtime"
```

路径相对于 `manifest.json` 所在目录，Windows 路径建议使用 `/`。工具本身的“Unity 包目录”设置应指向包含 `com.company.unity-table-runtime` 的目录，例如仓库中的 `UnityRuntimePackage`，不要只指向包内的 `Runtime` 目录。

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

var label = Tables.Get("Items", "10001", row => row.Read<RuntimeLocalizationValue>("label"));
var map = Tables.Get("Items", "10001", row => row.Read<RuntimeMapValue>("item_map"));
var text = label.GetText("zh-CN") ?? label.Key;
```

工具导出的 localization JSON 形如 `{ "key": "greeting", "translations": { "zh-CN": "你好" } }`；缺少语言时 `GetText` 返回空值并保留 key。`TableRowView.Read<T>` 也能直接读取结构化 JSON，再由生成适配器转换为强类型嵌套对象。

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
