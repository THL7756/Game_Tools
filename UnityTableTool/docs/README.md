# 文档索引

仓库根目录的 [README.md](../README.md) 面向第一次使用工具的开发者；本目录记录规则、格式、运行时接入和项目边界。

## 推荐阅读顺序

1. [支持范围](支持范围.md)：确认 Unity、UE、Godot 当前分别能做什么。
2. [表结构](表格规则.md)：了解普通表、单例表、Sheet 和分表。
3. [字段和校验规则](表格内容规则.md)：了解字段类型、端标记、数组和引用。
4. [导出格式](ExportFormats.md)：了解 JSON、C# 和 UTB1 的实际输出。
5. [Unity Runtime 接入](RuntimeIntegration.md)：把包加入 Unity 并读取表。
6. [排错](Troubleshooting.md)：处理扫描、校验和 Unity 接入问题。

## 代码入口

- `src/TableTool.Core/Reading/TableFileReader.cs`：输入文件读取。
- `src/TableTool.Core/Parsing/TableSchemaParser.cs`：表头和字段解析。
- `src/TableTool.Core/Validation/TableValidator.cs`：字段、主键和数据校验。
- `src/TableTool.Core/Validation/TableReferenceValidator.cs`：`<表名>_id` 引用校验。
- `src/TableTool.Gui/Services/BuildService.cs`：GUI 打表主流程。
- `UnityRuntimePackage/com.company.unity-table-runtime/Runtime/TableManager.cs`：Unity Runtime 加载和查询入口。

## 项目状态

当前 GUI 使用 JSON 作为正式数据输出，客户端/服务器 C# 可分别生成。序列化库和 Runtime 已支持 UTB1 `.bytes`，但 GUI 还没有 `.bytes` 输出开关。UE/Godot 目前只能自行消费 JSON，没有官方运行时包。

项目计划和验收记录见 [ProjectPlan.md](ProjectPlan.md)，未纳入当前版本的扩展方向见 [ExtensionPoints.md](ExtensionPoints.md)。

最近一次验证为 9/10 测试通过；唯一失败是样例工作簿重复主键回归测试与当前样例数据不一致，详见 [ProjectPlan.md](ProjectPlan.md) 的“最近验证”。
