# UnityTableTool

UnityTableTool 是一个 Windows 本地 Unity 配置表扫描、校验和导出工具。当前版本已经完成可直接使用的发布底包，发布文件位于 `release/UnityTableTool/UnityTableTool.exe`。

## 已完成的功能

- 扫描 `.xlsx`、`.xls`、`.csv`、`.tsv`，启动时自动定位项目 `Data` 目录并立即扫描。
- 表列表支持搜索、收藏、最近 10 张、所有表四种组合筛选；每项可以勾选、收藏或打开源文件。
- 勾选一张分表会自动带出同一 `table:` 逻辑表的其他分表；`<逻辑表名>_id` 关联表也会自动加入。
- 客户端和服务器可以同时导出，分别写入 `Data_c`、`Data_s`；代码输出到 `Code/Client`、`Code/Server`。
- JSON 与 bytes 二选一，C# 代码可独立开关；校验只在导出前执行，校验失败不会替换输出目录。
- 导出成功后自动取消所有表的勾选，并把本次文件记录为最近打表。
- 单例表会聚合为一个对象，JSON 使用 `data` 对象，Runtime 支持 `GetSingleton`。
- 设置页集中管理目录、浅色/深色/跟随系统主题和字体大小；主界面左右模块可拖拽调整宽度。
- 内置字段规则页和字段类型示例页，包含基础类型、Unity 类型和一到三维数组写法。
- Runtime Package 支持 JSON 和 UTB1 bytes 自动识别，不依赖 manifest。

## 快速使用

1. 双击 `release/UnityTableTool/UnityTableTool.exe`。
2. 工具会自动扫描项目 `Data` 目录；也可以在“设置”中改成其他表根目录。
3. 在列表中搜索并勾选一张或多张表。
4. 选择客户端、服务器或同时选择两端，选择 JSON 或 bytes，按需选择 C# 代码。
5. 点击“导出”。导出前会自动完成关联表展开、合并和校验。

## 目录

- `src/TableTool.Core`：表读取、解析、类型处理、关联表展开和校验。
- `src/TableTool.Serialization`：客户端/服务器分端导出、C#、JSON、UTB1 bytes。
- `src/TableTool.Gui`：Windows WPF 图形界面。
- `UnityRuntimePackage`：Unity 运行时读取包。
- `Data`：示例源表。
- `docs`：表结构、字段规则、导出格式和 Runtime 接入文档。

## 构建和发布

项目固定使用 .NET 8 SDK。已安装 .NET 8 SDK 或 .NET 10 SDK 的 Windows 环境均可运行：

```text
dotnet test UnityTableTool.sln -c Release
dotnet build src/TableTool.Gui/TableTool.Gui.csproj -c Release
publish.cmd
```

`publish.cmd` 会先测试，再生成 `release/UnityTableTool`。当前发布为 framework-dependent 版本，需要目标机器安装 .NET 8 Desktop Runtime 或 SDK。

构建缓存写入 Windows 临时目录 `%TEMP%\UnityTableTool-build`，不会在项目根目录创建 `.build`、`.nuget` 等缓存目录。若当前环境无法访问 NuGet，但源码已有成功的 Release 构建，脚本会自动打包该构建；恢复网络后重新运行即可执行完整还原、测试和发布。

## 文档索引

- [总功能和发布清单](docs/README.md)
- [表结构](docs/TableFormat.md)
- [字段规则](docs/FieldRules.md)
- [导出格式](docs/ExportFormats.md)
- [Runtime Package 接入](docs/RuntimeIntegration.md)
- [排错](docs/Troubleshooting.md)
- [扩展接口](docs/ExtensionPoints.md)
