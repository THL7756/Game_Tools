# UnityTableTool

UnityTableTool 是一个 Windows 本地 Unity 配置表扫描、校验和 JSON 打表工具。界面按“配置工具、项目路径、视觉风格”三屏设计实现。

## 功能

- 启动后自动扫描配置表目录，支持 `.xlsx`、`.xls`、`.csv`、`.tsv`。
- 表列表支持搜索、收藏、最近、所有表筛选，并可打开源文件。
- 勾选后自动展开 `_id` 关联表，合并同一逻辑表的分表。
- `##` 注释行/列不参与数据和主键校验，`#test` 测试行只校验、不导出。
- 预览字段定义和数据，显示校验问题与打表日志。
- 校验失败不会替换输出目录；通过后分别向客户端、服务器目录写入 JSON。
- 设置页可维护项目根目录、配置表目录、客户端/服务器输出目录和可选脚本路径。
- 支持白天、晚上、跟随系统三种模式，以及强调色、背景色、前景色、字体和缩放设置。
- 滚动区域支持鼠标和键盘滚动。

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

`publish.cmd` 会执行测试并生成 `release/UnityTableTool-2.4.0`，同时创建 `release/UnityTableTool-2.4.0-win-x64.zip`。发布包为 Windows x64 自包含版本，不需要单独安装 .NET Desktop Runtime。
