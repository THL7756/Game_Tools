// 用途：使用已安装 SDK 的 C# 9 编译器验证生成源码，不增加第三方依赖。
// 编写日期：2026-10-15
// 作者：Codex（按用户需求修改）

using System.Diagnostics;
using System.Reflection;
using Company.UnityTableRuntime;
using Xunit;

namespace TableTool.Tests.Serialization;

internal static class GeneratedCodeCompiler
{
    public static Assembly Compile(params string[] sources)
    {
        var compiler = typeof(GeneratedCodeCompiler).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(item => item.Key == "CSharpCompilerPath").Value!;
        var root = Path.Combine(Path.GetTempPath(), "UnityTableGenerated-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var output = Path.Combine(root, "Generated.dll");
            var arguments = new List<string>
            {
                "/nologo", "/target:library", "/langversion:9.0", "/nostdlib+", $"/out:\"{output}\""
            };
            // 只引用框架和 Runtime，避免 Core 内链接的共享类型与 Runtime 重复。
            var frameworkDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Where(path => Path.GetDirectoryName(path) == frameworkDirectory)
                .Append(typeof(TableManager).Assembly.Location)
                .Distinct();
            arguments.AddRange(references.Select(path => $"/reference:\"{path}\""));
            for (var index = 0; index < sources.Length; index++)
            {
                var path = Path.Combine(root, $"Source{index}.cs");
                File.WriteAllText(path, sources[index]);
                arguments.Add($"\"{path}\"");
            }
            var response = Path.Combine(root, "compile.rsp");
            File.WriteAllLines(response, arguments);
            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.ArgumentList.Add(compiler);
            start.ArgumentList.Add("@" + response);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30_000))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                throw new TimeoutException("生成代码的 C# 9 编译超时。");
            }
            Assert.True(process.ExitCode == 0, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
            return Assembly.Load(File.ReadAllBytes(output));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    public static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "UnityTableTool.sln")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate UnityTableTool repository root.");
    }
}
