# ExcelSmartDiff 参考工具说明

## 1. 定位

`excelSmartDiff_参考` 是搬入项目的独立 Excel Diff 成品包，用于保留现有使用能力和提供功能参考。它不参与 `TableDiffTool.sln` 的编译，也不作为自研 Core 的运行时依赖。

## 2. 目录结构

- `app/ExcelMerge.GUI.exe`：ExcelMerge 主程序
- `app/*.dll`：主程序运行依赖
- `p4v-diff/ExcelMergeP4VDiff.exe`：外部包装程序
- `install_p4v_excel_smart_diff.cmd`：安装入口
- `install_p4v_excel_smart_diff.ps1`：安装脚本
- `README.md`：参考工具原始说明

## 3. 已知能力

参考工具 README 记录的能力包括：

- `xlsx`、`xls`、`xlsm`、`csv`、`tsv` 比较
- 表格结构化 Diff
- 右侧工作表编辑
- 单元格编辑、清空和矩形粘贴
- 复制行、插入空行、复制行、删除行
- 保存前备份
- 临时文件缓存和日志清理

## 4. 与自研版本的关系

自研版本和参考工具的职责分开：

- 自研版本负责可维护的 Core、CLI、WPF GUI 和自动化测试。
- 参考工具保留成熟的 Excel 交互能力，作为兼容性和功能设计参考。
- 自研版本首批聚焦 `.xlsx`，不直接复制参考工具的旧版 DLL。
- 后续如果需要支持 `.xls`、`.xlsm`、`csv`、`tsv`，先评估适配器方案，再决定是否接入。

## 5. 当前使用建议

需要稳定查看或编辑多种 Excel 格式时，可直接运行：

```text
excelSmartDiff_参考\app\ExcelMerge.GUI.exe
```

需要接入自动化流程、双向替换、冲突检查或自研功能时，使用 `TableDiffTool` 自研版本。

## 6. 注意事项

参考工具自身 README 仍保留原始包装程序和安装说明。本文只描述它在当前项目中的定位，不把参考工具的外部集成配置当作自研工具的必需步骤。
