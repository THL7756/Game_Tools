# 扩展接口

首版不实现远程服务器打表、CI/CLI、macOS GUI 或 Linux GUI。

- Core 的导出服务可被未来 CLI/CI 调用。
- GUI 的平台启动器可接入其他桌面平台。
- Runtime 的加载回调可接入 AssetBundle、Addressables 或远程资源。

这些接口不会改变六行表头、三层拆分协议、JSON 结构或 UTB1 magic。
