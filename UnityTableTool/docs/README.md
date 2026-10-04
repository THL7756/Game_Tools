# 文档索引

- [表结构](TableFormat.md)
- [字段规则](FieldRules.md)
- [导出格式](ExportFormats.md)
- [Runtime Package 接入](RuntimeIntegration.md)
- [排错](Troubleshooting.md)
- [扩展接口](ExtensionPoints.md)

目录用途：

- `src/TableTool.Core`：读取 `.xlsx`、`.xls`、`.csv`、`.tsv`，解析表结构、类型、注释和校验规则。
- `src/TableTool.Serialization`：输出 C#、JSON、UTB1 bytes 和 manifest。
- `src/TableTool.Gui`：Windows WPF 图形界面，负责选择路径、扫描、校验和导出。
- `src/TableTool.Tests`：Core、导出和 Runtime 读取逻辑的自动化测试。
- `UnityRuntimePackage`：Unity 项目运行时读取 JSON/bytes 的包，本版不要求先接入 Unity 验收。
- `samples`：示例输入表和示例输出；输入目录可以继续嵌套子目录。
- `build`：SDK 构建和 self-contained 发布命令；`release`：发布包布局说明。

GUI 中的三个路径含义：表根目录负责递归发现输入表；输出数据目录保存 JSON、bytes、manifest；输出代码目录保存生成的 C# 文件。
