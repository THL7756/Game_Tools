# UnityTableTool

UnityTableTool 是一个 Windows 本地 Unity 配置表扫描、校验和 JSON 打表工具。界面按“配置工具、项目路径、视觉风格”三屏设计实现。

工具的工作单位是“表文件”。表列表显示文件名；打开文件、收藏、最近记录和勾选都以文件为单位。打开文件后，右侧详情显示该文件读取到的真实 Sheet 标签，逻辑表名来自当前 Sheet 的 `table:` 定义。

## 功能

- 启动后自动扫描配置表目录，支持 `.xlsx`、`.xls`、`.csv`、`.tsv`。
- 表列表支持搜索、收藏、最近、所有表筛选，并可打开源文件；列表默认不勾选任何文件。
- 详情区使用真实 Sheet 名称切换预览，字段标题和数据单元格水平、垂直居中。
- 勾选后自动展开 `_id` 关联表，合并同一逻辑表的分表。
- `##` 注释行/列不参与数据和主键校验，`#test` 测试行只校验、不导出。
- 预览字段定义和数据，显示校验问题与打表日志。
- 校验失败不会替换输出目录；通过后分别向客户端、服务器目录写入 JSON。
- 设置页可维护项目根目录、配置表目录、客户端/服务器输出目录和可选脚本路径。
- 支持白天、晚上、跟随系统三种模式，以及强调色、背景色、前景色、字体和缩放设置。
- 滚动区域支持鼠标和键盘滚动，预览表格使用普通滚轮上下滚动、Shift+滚轮左右滚动，滚动条本身隐藏。
- 支持简体中文和 English，语言包放在发布目录的 `Languages` 文件夹中，可继续添加 JSON 语言包。
- 问题列表中错误使用红色、警告使用黄色；警告不阻断导出，错误和致命问题会阻断导出。

## 快速使用

1. 双击 `release/UnityTableTool-2.4.0/UnityTableTool.exe`。
2. 工具自动扫描项目 `Data` 目录，也可在“设置 / 项目路径”中更改目录。
3. 在“配置工具”中勾选配置表并选择客户端、服务器输出范围。
4. 点击“打表 (N)”，结果只写入 JSON 文件。

## 目录

- `src/TableTool.Core`：表读取、解析、合并、关联展开和校验。
- `src/TableTool.Serialization`：JSON 导出。
- `src/TableTool.Gui`：WPF 图形界面。
- `UnityRuntimePackage`：Unity JSON/bytes 运行时读取包。
- `Data`：示例源表。
- `release`：最终发布目录和压缩包。

## 构建和发布

```text
dotnet test UnityTableTool.sln -c Release
publish.cmd
```

`publish.cmd` 会清理并生成 `release/UnityTableTool-2.4.0`，同时创建 `release/UnityTableTool-2.4.0-win-x64.zip`。发布包为 Windows x64 自包含版本，不需要单独安装 .NET Desktop Runtime；可扩展语言包保留在发布目录的 `Languages` 文件夹中。

## 当前验收范围

详细实施计划和验收标准见 [docs/ProjectPlan.md](docs/ProjectPlan.md)。本轮代码修改完成后只做 Release 编译确认，测试和重新打包按用户要求暂不执行。
