# Runtime Package 接入

将 `UnityRuntimePackage/com.company.unity-table-runtime` 加入 Unity 项目的 `Packages` 目录，或在 `Packages/manifest.json` 使用本地路径引用。

Runtime Package 提供 `Company.UnityTableRuntime.TableManager`。读取时可以传入 JSON 或 UTB1 bytes，格式检测顺序为：manifest 声明、UTB1 magic、合法 JSON。

```csharp
var manager = new Company.UnityTableRuntime.TableManager();
manager.Load("Skill", bytesOrJson);
var skill = manager.Get("Skill", "1001");
```

正式构建推荐使用 bytes；开发和调试可以使用 JSON。后续可以通过 `TableRuntimeSettings.LoadBytes` 接入 AssetBundle、Addressables 或其他加载来源。
