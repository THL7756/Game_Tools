# 文档索引

- [表结构](TableFormat.md)
- [字段规则](FieldRules.md)
- [导出格式](ExportFormats.md)
- [Runtime Package 接入](RuntimeIntegration.md)
- [排错](Troubleshooting.md)
- [扩展接口](ExtensionPoints.md)

目录用途：

- `src/TableTool.Core`：读取 `.xlsx`、`.xls`、`.csv`、`.tsv`，解析表结构、类型、注释、校验规则并合并同名分表。
- `src/TableTool.Serialization`：输出 C#、JSON 和 UTB1 bytes。
- `src/TableTool.Gui`：Windows WPF 图形界面，负责扫描、文件名列表、搜索、最近打表、勾选、端别选择、校验和导出。
- `src/TableTool.Tests`：Core、导出和 Runtime 读取逻辑的自动化测试。
- `UnityRuntimePackage`：Unity 项目运行时读取 JSON/bytes 的包。
- `Data`：示例源表；`Data_c`、`Data_s`：客户端/服务器数据输出；`Code`：独立代码输出。

GUI 的表列表、搜索和最近记录均以文件名为主。可以只打客户端或只打服务器；勾选一张表时，关联表会自动加入本次导出。`Data_c` 和 `Data_s` 中的 JSON、bytes 平铺存放，不生成 manifest，数据内容也不记录字段端别。
