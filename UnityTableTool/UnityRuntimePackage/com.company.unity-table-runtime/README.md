# Unity Table Runtime

这个包包含两部分：

- `Runtime/`：游戏运行时读取 JSON/UTB1、主键查询、单例查询和字段类型转换。
- `Editor/`：Unity Editor 内的源表扫描、完整规则校验、表列表和客户端/服务器打表窗口。

支持 Unity 2021.3 及以上，包内源码使用 C# 9 语法。复制安装时，关闭 Unity，将 `com.company.unity-table-runtime` 文件夹放到项目的 `Packages` 中；更新旧包时请先备份并移走旧文件夹，再复制新版，避免旧的 `obj/bin` 生成文件残留。安装目录应为 `Packages/com.company.unity-table-runtime/package.json`，包内不得携带 `Runtime/obj` 或 `Runtime/bin`。

安装包后，在 Unity 中创建 `UnityTableTool/Table Tool Settings` 资产，配置源表与输出目录，再通过 `Tools/UnityTableTool/Table Tool` 打开窗口。窗口只在“刷新表列表”或“开始打表”时扫描源表，不会在每次界面重绘时自动重复加载。

Excel 读取依赖（含 `.xls` 所需的代码页程序集）随包放在 `Editor/Plugins`，不会进入 Runtime 程序集；JSON 读取所需依赖放在 `Runtime/Plugins`，用于 Runtime 的 JSON/UTB1 读取。桌面 GUI 项目也直接编译 `Editor/Shared` 中的同一套解析、校验和导出源码，保证两端规则一致。

多语言字段在业务 JSON 中只保存键，例如 `"label": "greeting"`；翻译集中在独立的 `localization.json`。运行时先调用 `tables.Load("localization", bytes)` 加载一次语言表，再用 `tables.GetLocalizationText(key, "zh-CN")` 查询，内部直接使用主键和字段字典，不逐行遍历。多语言值类型只含 `Key`；缺语言返回 `null`，缺表或缺键抛出 `KeyNotFoundException`。

此版本不再读取旧的多语言对象 JSON，更新时需同步更新包、重新打表生成 JSON/UTB1 和 C#。完整用法见仓库的 `docs/RuntimeIntegration.md`。
