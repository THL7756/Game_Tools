# 项目计划

## 当前目标

UnityTableTool 当前定位是 Windows 本地配置表检查和导出工具，重点保证“文件级选择、Sheet 级详情、统一校验、客户端/服务器分端输出”流程稳定，并为 Unity 提供可直接接入的 Runtime Package。

## 当前已实现

- 递归读取 `.xlsx`、`.xls`、`.csv`、`.tsv`，Excel 保留真实 Sheet 名。
- 解析普通表、`type:single` 单例表、同名分表和 `<表名>_id` 引用。
- 校验标量、数组、结构值、enum、默认值、主键、单例数据和引用主键。
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
- 客户端和服务器字段必须按端标记分别输出，单例必须输出 `data` 对象。
- Unity Runtime 必须能读取 GUI 生成的 JSON，并通过生成的适配器得到强类型对象。

构建命令、目录说明和使用示例见根目录 [README.md](../README.md)。

## 最近验证

截至 2026-10-11，`dotnet test UnityTableTool.sln -c Release --no-restore` 可以完成编译并运行 10 个测试，其中 9 个通过、1 个失败。失败项是 `SampleWorkbookRegressionTests.RealSkillWorkbooksReportTheObservedDuplicateLocations`：样例工作簿实际返回 0 个 `1007` 重复主键，但测试仍期望 2 个。该样例回归需要在后续整理测试数据或校验规则时同步更新。
