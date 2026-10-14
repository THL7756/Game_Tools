# 项目计划

## 当前目标

UnityTableTool 当前定位是 Windows 本地配置表检查和导出工具，重点保证“文件级选择、Sheet 级详情、统一校验、客户端/服务器分端输出”流程稳定，并为 Unity 提供可直接接入的 Runtime Package。

## 当前已实现

- 递归读取 `.xlsx`、`.xls`、`.csv`、`.tsv`，Excel 保留真实 Sheet 名。
- 解析普通表、`type:single` 单例表、同名分表和 `<表名>_id` 引用。
- 识别字段名 `##`、第 1 行 `##` 整列标记、`##` 注释行和 `#test_`/`#ceshi` 测试内容；注释与测试内容不会进入正式导出。
- 预览字段使用字段定义身份和源列索引，重复字段保留全部列；正式字段重名会报告每个冲突位置。
- 单例表支持带说明列的偏移字段行，`values` 等普通字段会进入单例 `data` 对象。
- 校验标量、数组、结构值、enum、默认值、主键、单例数据和引用主键。
- 校验 `localization`、递归 `beans(...)`、`map(key)(string)(beans(...))` 的默认值、嵌套引用、转义字符和重复 map key。
- GUI 支持搜索、收藏、最近表、Sheet 预览、问题定位、客户端/服务器输出范围和主题设置。
- 生成客户端/服务器 JSON 与 C#；客户端 C# 包含 Runtime 行适配器。
- 序列化库支持 UTB1 `.bytes`，Unity Runtime 支持 JSON/UTB1 自动读取、主键查询、单例查询、批量转换。
- 输出使用 staging 和原子替换，只覆盖本次生成文件，保留输出目录中的其他文件。
- 测试项目覆盖表校验、引用、JSON/Runtime 和导出目录保护。

## 后续计划

1. 增加 CLI/CI 入口，让构建机可以只执行扫描、校验和导出。
2. 在 GUI 中增加 `.bytes`/UTB1 开关，并生成可供 Unity 资源系统使用的 manifest。
3. 增加 UE C++/DataTable 和 Godot GDScript/C# 的官方适配层，明确 JSON 到引擎类型的映射。
4. 增加 schema 版本、差异比较、变更报告和破坏性变更提示。
5. 提供 Addressables/AssetBundle 构建钩子、远程表源和资源缓存示例。
6. 评估 macOS/Linux GUI 与跨平台发布方案。

## 验收重点

- 检测错误必须阻断导出，并保留来源文件、Sheet、行、列和字段位置。
- 警告不阻断导出，信息只用于日志。
- 同名表字段结构冲突必须显式报错，不能静默合并。
- 同名表正式字段和测试字段必须逐列匹配名称、顺序、类型、默认值和端标记；冲突会阻止合并和导出。
- 正式字段重复、缺失引用和未知类型必须保留预览源列，同时阻止导出。
- 客户端和服务器字段必须按端标记分别输出，单例必须输出 `data` 对象。
- Unity Runtime 必须能读取 GUI 生成的 JSON，并通过生成的适配器得到强类型对象。
- 复杂类型 JSON、C# 嵌套类型和 Runtime `TableRowView.Read<T>` 必须使用同一份类型 AST 与值解析结果。

构建命令、目录说明和使用示例见根目录 [README.md](../README.md)。

## 最近验证

截至 2026-10-14，`dotnet test UnityTableTool.sln -c Release --no-restore` 可以完成编译并运行 22 个测试，全部通过。回归工作簿为 `Data/表格规范.xlsx`，覆盖普通表注释列/行、测试列、单例 `values`、重复字段、同名表 schema 冲突，以及 localization、beans、map、转义、嵌套引用和 Runtime round-trip。
