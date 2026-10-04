# UnityTableTool

UnityTableTool 是一个 Windows 本地 Unity 配置表检测和导出工具。源码、Runtime Package、示例和使用文档在同一目录内。

## 快速使用

1. 将 `UnityRuntimePackage/com.company.unity-table-runtime` 复制或添加到 Unity 项目的 `Packages` 目录。
2. 打开 `UnityTableTool.exe`；源码开发时运行 `TableTool.Gui` 项目。
3. 在“表目录”填写 `.xlsx`、`.csv` 或 `.tsv` 文件路径。
4. 点击“扫描”和“校验”，先处理所有错误。
5. 选择 JSON、bytes 或 C#，点击“导出”。
6. 将导出的 bytes 或 JSON 放入 Unity 项目运行时加载路径。

表结构、类型、注释、测试标记和输出格式见 `docs` 目录。示例见 `samples` 目录。

## 源码目录

- `src/TableTool.Core`：表结构解析、类型解析、校验和文件读取。
- `src/TableTool.Serialization`：C#、JSON、UTB1 bytes 和 manifest 导出。
- `src/TableTool.Gui`：Windows WPF GUI。
- `UnityRuntimePackage`：Unity 运行时 JSON/bytes reader。

## 当前工具链说明

源码按 .NET 8 工程组织。若开发机没有 .NET SDK，仍可查看和接入源码，但不能运行 `dotnet build` 或 `dotnet test`；安装 .NET 8 SDK 后再执行构建和测试命令。
