# 构建

开发和编译需要安装 .NET 8 SDK。当前工作环境只有 .NET 8 Runtime，因此 dotnet build 和 dotnet test 暂时无法执行。

安装 SDK 后，在仓库根目录执行：

~~~text
dotnet restore UnityTableTool.sln
dotnet test src/TableTool.Tests/TableTool.Tests.csproj
dotnet build UnityTableTool.sln -c Release
~~~

WPF GUI 需要 Windows Desktop SDK。发布时使用 self-contained win-x64：

~~~text
dotnet publish src/TableTool.Gui/TableTool.Gui.csproj -c Release -r win-x64 --self-contained true -o release
~~~

`--self-contained true` 会把 .NET 运行时一起放进发布目录。最终用户只需解压发布目录并双击 `UnityTableTool.exe`，不需要安装 .NET SDK、MSBuild、C# 编译器或单独的 .NET Runtime。
