# 导出格式

## GUI 当前输出

GUI 的“打表”流程会根据客户端/服务器输出范围分别写入 JSON，并按代码输出范围生成 C#：

```text
客户端数据目录 -> <表名>.json
服务器数据目录 -> <表名>.json
客户端代码目录 -> <表名>Data.cs
服务器代码目录 -> <表名>Data.cs
```

客户端和服务器可以分别或同时选择。`c`、`s`、`cs` 或空字段标记只决定字段进入哪一端，不会写入最终 JSON。输出目录使用 staging 和原子替换，只覆盖本次生成的文件，不会清空目录中的其他表。

GUI 当前在 `BuildService` 中固定使用 `generateBytes: false`，所以正式界面不会生成 `.bytes`。序列化库仍提供 `ExportService.ExportBytes` 和 `ExportOptions.GenerateBytes`，构建脚本可以据此生成 UTB1 文件。

## JSON 结构

普通表输出 `<表名>.json`：

```json
{
  "tableName": "Skill",
  "isSingleton": false,
  "primaryKey": "id",
  "fields": [
    { "name": "id", "type": "int", "defaultValue": null },
    { "name": "name", "type": "string", "defaultValue": null }
  ],
  "arraySeparators": { "inner": "#", "middle": "|", "outer": ";" },
  "rows": [
    { "id": "1001", "name": "火球" }
  ]
}
```

单例表使用同样的元数据，但把 `rows` 替换成一个 `data` 对象：

```json
{
  "tableName": "GlobalConfig",
  "isSingleton": true,
  "primaryKey": null,
  "fields": [
    { "name": "language", "type": "string", "defaultValue": "zh-CN" }
  ],
  "arraySeparators": { "inner": "#", "middle": "|", "outer": ";" },
  "data": { "language": "zh-CN" }
}
```

`fields` 保存字段类型和默认值，供 Unity Runtime 的 `TableRowView.Read<T>` 转换使用；`arraySeparators` 保存当前工具设置的一到三维数组分隔符。`#test_`/`#ceshi` 测试行/列和 `##` 注释行/列不导出。单例表中的 `values` 等普通字段会保留在 `data` 对象中；只有空字段名、字段名 `##` 或第 1 行 `##` 整列标记才会被排除。

复杂字段使用结构化 JSON，不把已确认的复杂协议降级为普通字符串：

```json
{
  "label": { "key": "greeting", "translations": { "zh-CN": "你好", "en-US": "Hello" } },
  "item_bundle": { "item_id": 2001, "count": 10 },
  "item_map": {
    "1": { "text": "文本", "bean": { "item_id": 2001, "count": 10 } }
  }
}
```

`localization` 的键由工具从逻辑表 `localization` 的 `id` 列查找并写入语言字典；缺键阻断当前逻辑表和依赖它的表，缺语言由 Runtime 返回空文本。`beans` 输出字段对象。`map` 只接受 `map(key)(string)(beans(...))`，输出对象键和值对象，重复 key 会阻断当前逻辑表。复杂字段值在 Runtime 中仍可通过 `TableRowView.Read<T>` 读取，生成的客户端 C# 会包含字段对应的嵌套类型。

## C# 文件

客户端 C# 包含 `<表名>Data` 数据类和 `<表名>TableAdapter`。服务器 C# 只生成数据类，避免服务器项目依赖 Unity Runtime。字段名会转换为首字母大写的成员名；`enum(...)` 会生成对应枚举。复杂字段会在数据类中生成公开嵌套类型，例如 `LocalizationValue`、`ItemNumBean` 和 `CubeSpecialEntry`；map 字段使用 `Dictionary<TKey, Entry>`。

含 `Vector2`、`Vector3`、`Vector4`、`Color` 或 `Quaternion` 的客户端代码会引用 `UnityEngine`。普通标量和数组字段不需要 UnityEngine。

## UTB1 bytes

`BinaryTableExporter` 生成带有以下内容的 `.bytes`：

- `UTB1` magic 和版本号 `1`；
- 表名和 schema hash；
- UTF-8 JSON payload；
- payload 长度和 CRC32。

Unity Runtime 的 `RuntimeFormat.ReadAuto` 可以自动读取 JSON 或 UTB1，也可以通过 `manifestFormat` 指定 `json` 或 `bytes`。当前 GUI 没有 bytes 输出按钮，使用时需要在构建脚本中调用 Serialization API。
