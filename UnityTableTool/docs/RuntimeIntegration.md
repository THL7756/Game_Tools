# Runtime Package 接入

将 `UnityRuntimePackage/com.company.unity-table-runtime` 加入 Unity 项目的 `Packages` 目录，或在 `Packages/manifest.json` 使用本地路径引用。

Runtime Package 提供 `Company.UnityTableRuntime.TableManager`。读取时传入 JSON 或 UTB1 bytes，格式检测顺序为：

1. 检测 `UTB1` magic。
2. 检测合法 JSON 结构。
3. 都不匹配时报告错误。

不再依赖 manifest。客户端从 `Data_c` 加载，服务器从 `Data_s` 加载；JSON 和 bytes 不记录字段端别，端别由资源所在目录和运行端决定。

```csharp
var manager = new Company.UnityTableRuntime.TableManager();
manager.Load("Skill", bytesOrJson);
var skill = manager.Get("Skill", "1001");

var config = manager.GetSingleton("GlobalConfig");
```

正式构建推荐使用 bytes；开发和调试可以使用 JSON。后续可以通过 `TableRuntimeSettings.LoadBytes` 接入 AssetBundle、Addressables 或其他加载来源。
