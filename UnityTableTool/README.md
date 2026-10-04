# UnityTableTool

UnityTableTool 是一个 Windows 本地 Unity 配置表检测和导出工具。源码、Runtime Package、示例和使用文档在同一目录内。

## 快速使用

1. 打开 `UnityTableTool.exe`；源码开发时运行 `TableTool.Gui` 项目。
2. 在“表根目录”选择包含表格的根文件夹，工具会递归读取 `.xlsx`、`.xls`、`.csv`、`.tsv`。
3. 在“输出数据目录”选择 JSON、bytes、manifest 的输出文件夹。
4. 在“输出代码目录”选择生成的 C# 文件夹。
5. 点击“扫描”和“校验”，先处理所有错误。
6. 选择 JSON、bytes 或 C#，点击“导出”。
7. 暂不测试 Unity 时，可以直接检查两个输出目录中的文件。

Runtime Package 后续接入 Unity 时再复制到项目的 `Packages` 目录。

表结构、类型、注释、测试标记和输出格式见 `docs` 目录。示例见 `samples` 目录。

## 源码目录

- `src/TableTool.Core`：表结构解析、类型解析、校验和文件读取。
- `src/TableTool.Serialization`：C#、JSON、UTB1 bytes 和 manifest 导出。
- `src/TableTool.Gui`：Windows WPF GUI。
- `UnityRuntimePackage`：Unity 运行时 JSON/bytes reader。
- `samples`：可直接扫描的 `.xlsx` 示例表和示例输出。
- `docs`：表结构、字段规则、导出格式、接入和排错说明。
- `build`：构建和发布说明；`release`：发布包目录。

输出目录约定：数据输出目录只包含 `Json`、`Bytes`、`Manifest`；代码输出目录只包含生成的 `*.cs`。

## 当前工具链说明

源码按 .NET 8 工程组织。若开发机没有 .NET SDK，仍可查看和接入源码，但不能运行 `dotnet build` 或 `dotnet test`；安装 .NET 8 SDK 后再执行构建和测试命令。
