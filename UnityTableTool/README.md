# UnityTableTool

UnityTableTool 是一个 Windows 本地 Unity 配置表检测和导出工具。源码、Runtime Package、示例和中文文档都在本目录内。

## 快速使用

1. 打开 `UnityTableTool.exe`；源码开发时运行 `TableTool.Gui` 项目。
2. 在“表根目录”选择 `Data` 或其他包含表格的目录，工具递归读取 `.xlsx`、`.xls`、`.csv`、`.tsv`。
3. 扫描后，表列表按**文件名**展示；搜索框按文件名筛选，最近打表按文件名记录最近使用项。
4. 用复选框勾选一张或多张表；勾选表的关联表即使未勾选，也会在打表时自动一起导出。
5. 打表时选择“只打客户端”或“只打服务器”。客户端结果写入 `Data_c`，服务器结果写入 `Data_s`；两个目录中的 `.json` 和 `.bytes` 直接平铺存放，不建立 `Json`、`Bytes` 子目录，也不生成 manifest。
6. JSON 和 bytes 数据不记录字段属于客户端还是服务器；端别只由本次导出模式和 `Data_c`/`Data_s` 输出目录决定。
7. C# 代码单独输出到 `Code`，不放在示例源表目录 `Data` 中。
8. 先校验，再点击“导出”；校验存在错误时不会提交输出。

Runtime Package 后续接入 Unity 时复制到项目的 `Packages` 目录。

表结构、类型、注释、测试标记、输出格式和关联表规则见 `docs` 目录。示例源表见 `Data/README.md`。

## 目录

- `src/TableTool.Core`：表结构解析、类型解析、校验、文件读取和同名分表合并。
- `src/TableTool.Serialization`：C#、JSON、UTB1 bytes 导出。
- `src/TableTool.Gui`：Windows WPF 图形界面。
- `UnityRuntimePackage`：Unity 运行时 JSON/bytes reader。
- `Data`：示例源表目录。
- `Data_c`：客户端数据输出目录。
- `Data_s`：服务器数据输出目录。
- `Code`：生成的 C# 输出目录。
- `docs`：使用和规则文档。

同一文件名对应的逻辑表可以在多个 Sheet 或多个文件中分表维护；扫描后按 schema 合并，再统一校验和导出。

## 构建

源码按 .NET 8 工程组织，可执行：

```text
dotnet restore UnityTableTool.sln
dotnet test UnityTableTool.sln
dotnet build UnityTableTool.sln -c Release
```
