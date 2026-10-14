# Unity Table Runtime

这个包包含两部分：

- `Runtime/`：游戏运行时读取 JSON/UTB1、主键查询、单例查询和字段类型转换。
- `Editor/`：Unity Editor 内的源表扫描、完整规则校验、表列表和客户端/服务器打表窗口。

安装包后，在 Unity 中创建 `UnityTableTool/Table Tool Settings` 资产，配置源表与输出目录，再通过 `Tools/UnityTableTool/Table Tool` 打开窗口。窗口只在“刷新表列表”或“开始打表”时扫描源表，不会在每次界面重绘时自动重复加载。

Excel 读取依赖（含 `.xls` 所需的代码页程序集）随包放在 `Editor/Plugins`，不会进入 Runtime 程序集；JSON 读取所需依赖放在 `Runtime/Plugins`，用于 Runtime 的 JSON/UTB1 读取。桌面 GUI 项目也直接编译 `Editor/Shared` 中的同一套解析、校验和导出源码，保证两端规则一致。
