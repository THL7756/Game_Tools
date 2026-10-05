# Diff 工具需求文档

> 状态：Excel Diff 第一版已完成。本文同时记录当前实现边界和后续扩展方向。

## 1. 工具定位

Diff 工具用于比较工程文件、配置数据和 Unity 资源，输出人可以阅读、程序可以继续处理的变更结果。工具需要同时支持命令行和可复用的核心库，方便本地使用、自动化检查和其他工具集成。

输入和输出以文件或目录为边界，不依赖外部提交系统。比较结果应能保存、复查和二次处理。

## 2. 当前已完成能力

当前自研版本已经完成 Excel Diff 的第一版闭环：

- `TableDiff.Core`：工作簿、工作表、主键和单元格级 Diff
- `TableDiff.Gui`：WPF 文件选择、Diff 列表、变更勾选和双向替换
- `TableDiff.Cli`：命令行比较、文本/JSON 报告和批量替换
- `TableDiff.Tests`：相同文件、行新增/删除、单元格修改和双向替换测试
- 替换保护：目标内容校验、时间戳备份、临时文件写入和冲突停止

当前自研版本首批聚焦 `.xlsx`。参考工具目录中的 `.xls`、`.xlsm`、`csv`、`tsv` 能力暂时保持独立，不作为自研 Core 的构建依赖。

## 3. P0 交付范围（已完成）

### 2.1 比较对象

- 单文件对比
- 两个目录的文件清单和内容对比
- 自动识别格式，也允许命令行显式指定格式
- 支持大文件的稳定处理，不能因为单个异常文件导致整个任务退出

### 2.2 首批格式

| 格式 | 比较方式 | 首批要求 |
| --- | --- | --- |
| 纯文本 | 按行比较，支持上下文和变更块 | 必须 |
| JSON | 按对象路径和数组项比较 | 必须 |
| YAML | 按节点路径比较，保留原始定位信息 | 必须 |
| CSV/TSV | 支持表头、主键列和行级变更 | 必须 |
| XLSX | 支持工作表、主键列、单元格和公式结果比较 | 必须 |
| Unity YAML | 支持对象、属性和引用变化展示 | 必须 |

### 2.3 核心能力

- 新增、删除、修改三类变更分类
- 结构化数据按路径或主键定位，不依赖行号
- 支持忽略空白、换行、数字格式和字段顺序等非实质差异
- 支持字段级过滤和路径过滤
- 变更结果包含文件、对象或行键、字段路径、旧值、新值
- 对二进制文件至少提供大小、哈希和文件状态变化
- 解析失败时返回明确错误位置、错误原因和处理建议

### 2.4 输出形式

- 控制台摘要：文件数、变更数、错误数和退出码
- JSON 报告：供自动化流程和其他工具消费
- HTML 报告：供人工查看，支持按文件、类型和严重级别筛选
- 纯文本报告：方便日志系统收集

## 4. 命令行接口

建议命令格式：

```text
tablediff compare <left> <right> [options]
```

首批参数：

```text
--format auto|text|json|yaml|csv|tsv|xlsx|unity-yaml|directory
--key <column-or-path>
--ignore <path-pattern>
--report console|json|html|text
--output <file>
--fail-on-change
--fail-on-error
```

示例：

```text
tablediff compare before.xlsx after.xlsx --format xlsx --key id --report html --output report.html
tablediff compare before.json after.json --format json --report json --output report.json
tablediff compare old-assets new-assets --format directory --report console --fail-on-change
```

## 5. 统一结果模型

每条变更至少包含以下字段：

```json
{
  "file": "Data/Item.xlsx",
  "format": "xlsx",
  "kind": "modified",
  "location": "Items[id=1001].price",
  "before": 100,
  "after": 120,
  "severity": "info",
  "message": "value changed"
}
```

顶层报告需要包含：

- 工具版本和比较时间
- 左右输入路径和识别格式
- 文件总数、变更文件数、错误数
- 新增、删除、修改统计
- 详细变更列表
- 解析错误和被忽略项目

## 6. 现有工具的关系

- `UniversalDiff`：统一入口和核心比较接口
- `Excel Smart Diff`：XLSX、CSV、TSV 适配器
- `PatchDiff`：Unity Patch 和资源变更视图适配器
- `VSG Visual Diff`：Unity Visual Scripting 资源适配器
- `WinMerge`：通用文件比较的外部工具兼容入口

这些工具应共享统一结果模型，避免每个工具使用不同的变更格式。

## 7. P1 后续增强

- Windows 图形界面，支持拖拽文件和目录
- 左右面板、变更树、字段级详情和搜索
- 报告模板和结果导出配置
- 可插拔格式适配器
- 大型 XLSX 和目录比较的进度显示
- 规则配置文件，集中管理忽略路径、字段和格式选项
- 与自动化构建流程的标准退出码和报告目录约定

## 8. P2 后续增强

- 二进制资源的结构化摘要比较
- 图片、音频和模型资源的元数据比较
- 变更影响分析和关联对象追踪
- 并行比较和缓存
- 历史报告索引与趋势统计

## 9. 已完成验收项

1. 相同输入比较结果为空，退出码为成功。
2. 文本、JSON、YAML、CSV、TSV、XLSX 和 Unity YAML 均能完成基本比较。
3. 结构化数据调整行顺序时，不应被误报为整表删除和新增。
4. XLSX 支持按主键定位行，并能显示字段级变化。
5. 解析失败时，报告包含文件路径、位置和错误原因。
6. JSON 报告可以被独立程序重新读取，字段结构稳定。
7. `--fail-on-change` 和 `--fail-on-error` 的退出码行为稳定。
8. 同一份输入在命令行和图形界面中得到一致的核心结果。

## 10. 后续实现顺序

1. 补充手工单元格编辑、清空、矩形粘贴和行操作。
2. 评估参考工具中的 `.xls`、`.xlsm`、`csv`、`tsv` 格式接入。
3. 增加 HTML 报告、拖拽操作、搜索和过滤。
4. 增加目录比较、进度显示和规则配置。
5. 性能优化、缓存和扩展格式。

## 11. Excel 首批实现边界

参考现有 Excel Smart Diff 的使用方式，首批 GUI 需要提供左右文件、主键列、变更列表和目标侧替换操作，并补充两个方向：

- 左侧文件替换到右侧文件
- 右侧文件替换到左侧文件

每次替换都必须先检查目标内容是否仍等于比较时的旧值。检查失败时只报告冲突，不覆盖目标文件。成功替换前生成带时间戳的备份，并通过临时文件完成写入。

当前版本只实现 `.xlsx`。读取和写回使用项目内的 Open XML 处理层，保证 Core、CLI 和 GUI 可以独立构建。`.xls`、`.xlsm`、格式保留、公式计算和手工编辑器能力列为后续扩展，不能阻塞已完成的 Diff 闭环。

## 12. 项目入口

- 解决方案：`TableDiffTool.sln`
- GUI：`src/TableDiff.Gui`
- CLI：`src/TableDiff.Cli`
- 核心库：`src/TableDiff.Core`
- 测试：`src/TableDiff.Tests`
- 参考工具：`excelSmartDiff_参考`
