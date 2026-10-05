# 排错

| 现象或错误 | 处理方式 |
| --- | --- |
| `FIELD_TYPE_UNKNOWN` | 检查字段类型是否为 `int`、`long`、`float`、`double`、`bool`、`string`、`Vector2`、`Vector3`、`Vector4`、`Color` 或 `Quaternion`。 |
| `FIELD_ARRAY_SEPARATOR_INVALID` | 检查一维 `#`、二维 `|` 和 `#`、三维 `;`、`|` 和 `#` 是否按维度使用。 |
| `PRIMARY_KEY_DUPLICATE` | 修改普通表第一字段的重复 key。 |
| `TABLE_REFERENCE_MISSING` | 检查 `<逻辑表名>_id` 字段名称，或补充关联表。 |
| `SINGLETON_ROW_COUNT_INVALID` | 单例源表的字段应聚合成一个对象；检查 `id`、`type`、`data` 语义列和字段内容。 |
| 表列表没有结果 | 点击扫描，检查设置中的表根目录和搜索关键字；启动时会自动扫描项目 `Data` 目录。 |
| 最近列表没有记录 | 只有成功导出后才会记录，最近列表最多显示 10 张。 |
| 收藏列表为空 | 在表列表每项右侧点击“收藏”，再切换筛选为“收藏”。 |
| 勾选分表后导出更多文件 | 这是预期行为，同一个 `table:` 逻辑表的分表会自动合并；关联表也会自动加入。 |
| JSON/bytes 没有端别字段 | 这是预期行为，端别由导出选择和 `Data_c`/`Data_s` 目录决定。 |
| `RUNTIME_FORMAT_UNKNOWN` | 确认文件是有效 JSON 或以 `UTB1` 开头的 bytes。 |
| `RUNTIME_SCHEMA_MISMATCH` | 重新导出当前端的数据和代码，确认客户端读取 `Data_c`、服务器读取 `Data_s`。 |
| 字体大小看起来没有变化 | 在“设置”页调整字体大小；新版本已移除固定字号样式。 |
| 发布脚本无法运行 | 确认安装 .NET 8 SDK 或 Desktop Runtime，再运行 `publish.cmd`。 |

校验只在导出流程执行，错误会显示在 GUI 的“检测结果”页，不会写入 `Code`、`Data_c` 或 `Data_s`。
