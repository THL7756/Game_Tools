# 发布目录

运行根目录的 `publish.cmd` 后，可执行文件会生成在 `release/UnityTableTool`。

发布前需要安装 .NET 8 SDK 和 .NET 8 Windows Desktop Runtime。仓库通过 `global.json` 固定 SDK 版本，脚本生成 framework-dependent 发布目录。

将 `Data` 目录复制到发布目录旁边后，启动 `UnityTableTool.exe` 即可扫描示例表；客户端和服务器结果分别写入 `Data_c`、`Data_s`，代码写入 `Code`。
