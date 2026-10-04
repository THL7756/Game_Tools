# bytes 示例

`Skill.bytes` 由 `UnityTableTool.exe` 或 `ExportService` 生成。当前工作环境没有 .NET SDK，无法在仓库内执行生成步骤，因此这里不放未经程序生成的二进制占位文件。

生成命令完成后，文件应以 ASCII `UTB1` 开头，并包含版本、表名、schema hash、JSON payload 长度和 CRC。
