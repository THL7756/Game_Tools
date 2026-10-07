# UnityTableTool 文档和发布清单

## 当前版本已完成

### 输入和表列表

- 支持 `.xlsx`、`.xls`、`.csv`、`.tsv`。
- 启动自动寻找项目 `Data` 目录并扫描。
- 搜索框可以按文件名和逻辑表名过滤。
- 列表提供“收藏”“最近 10 张”“所有表”筛选。
- 每个文件可以直接打开源文件。
- 选择分表会自动选择同逻辑表的其他分表；关联表按 `<逻辑表名>_id` 递归加入。

### 导出和校验

- 客户端、服务器可以分别或同时导出。
- 数据输出目录分别为 `Data_c`、`Data_s`。
- 同时导出时，代码目录分别为 `Code/Client`、`Code/Server`。
- GUI 打表输出 JSON 和 C# 数据文件，客户端与服务器可独立或同时选择。
- 导出前执行扫描、关联表检查、schema 合并和字段校验。
- 校验结果只显示在 GUI 中，不写入代码目录。
- 导出成功后自动取消表勾选并更新最近列表。
- 输出目录使用 staging 替换，并对 Windows 临时锁定做重试。

### 单例和运行时

- 单例表的多个常量字段聚合为一个对象。
- 客户端/服务器重复声明的同名单例字段会合并端别。
- JSON 单例使用 `data` 对象；Runtime 同时兼容旧的 `rows` 数组。
- `schemaHash` 用于识别结构变化，`formatVersion` 用于识别格式版本，当前保留。
- GUI 生成 JSON 和 C#；Runtime Package 仍可兼容 JSON 与 UTB1 bytes。
- 工具首次启动会在程序目录创建 `ToolData/settings.json`，用于保存界面、路径、输出范围和快捷键配置；目录不可写时回退到用户本地数据目录。

### GUI

- 设置页集中管理表根目录、客户端目录、服务器目录、代码目录。
- 主题支持浅色、深色、跟随 Windows 系统。
- 字体大小可通过设置页滑块调整。
- 主列表和结果面板之间可以拖拽调整大小。
- 内置字段规则和字段类型示例页。

## 目录索引

- `TableFormat.md`：表头、单例表、分表和数组格式。
- `FieldRules.md`：客户端/服务器字段标记、注释和测试列。
- `ExportFormats.md`：客户端/服务器 JSON 输出结构。
- `RuntimeIntegration.md`：Unity Package 接入和读取 API。
- `Troubleshooting.md`：常见错误和处理方法。
- `ExtensionPoints.md`：当前不在发布范围内的扩展方向。

## 发布验证

发布命令：

```text
publish.cmd
```

发布脚本会运行全部自动化测试，并生成：

```text
release/UnityTableTool/UnityTableTool.exe
```

当前验证结果：`35/35` 测试通过，GUI Release 构建通过。发布包为 Windows x64 自包含版本。

构建缓存位于 `%TEMP%\UnityTableTool-build`，`.build`、`.nuget`、`.dotnet-home` 等目录不是发布包必需内容。网络不可用时，脚本会在存在既有 Release 构建的情况下直接打包该构建，并明确输出提示。
# UnityTableTool 文档索引

- [项目实施与验收计划](ProjectPlan.md)
- [字段规则](FieldRules.md)
- [表结构](TableFormat.md)
- [排错](Troubleshooting.md)
- [运行时集成](RuntimeIntegration.md)
- [导出格式](ExportFormats.md)
- [扩展点](ExtensionPoints.md)
