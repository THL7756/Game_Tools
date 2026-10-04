# UnityTableTool 发布目录

发布目录应包含：

~~~text
UnityTableTool.exe
UnityRuntimePackage/com.company.unity-table-runtime/
samples/
docs/
~~~

当前目录只保留布局说明，UnityTableTool.exe 需要在有 .NET 8 SDK 的构建环境中执行 self-contained 发布命令生成。生成后的发布包不包含 CLI、远程服务或 CI 配置；最终用户无需安装 SDK、MSBuild、C# 编译器或 .NET Runtime。
