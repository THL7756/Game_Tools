# 扩展接口

当前版本明确保留以下扩展方向：

- `Core` 和 `Serialization` 可被未来 CLI/CI 调用；当前 GUI 是主要入口。
- `ExportService` 已有 JSON、C# 和 UTB1 bytes 输出能力，GUI 暂时只打开 JSON/C#。
- Unity Runtime 的 `TableRuntimeSettings.LoadBytes` 可接入 AssetBundle、Addressables、远程资源或项目自定义缓存。
- Diff、schema 版本和变更报告可作为独立模块实现，不改变现有表头和 JSON 基础结构。
- GUI 平台启动器未来可接入 macOS/Linux，但当前发布目标是 Windows x64。

UE 和 Godot 的专用运行时适配器尚未纳入当前版本；它们目前使用通用 JSON，需要项目自行完成字段转换和资源加载。

后续扩展应保持以下兼容约定：六行普通表定义区、最多三维数组、UTB1 magic `UTB1`、普通表 `rows` 数组和单例表 `data` 对象。
