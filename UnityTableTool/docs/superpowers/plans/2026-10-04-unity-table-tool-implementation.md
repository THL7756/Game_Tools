# Unity Table Tool Implementation Plan

> For agentic workers: use superpowers:executing-plans to implement this plan task-by-task with verification checkpoints.

**Goal:** 建立包含 Core、Windows GUI、Unity Runtime Package、示例和中文文档的本地 Unity 配置表工具，支持固定六行表头、单例表、递归表目录、三层数组拆分、C#/JSON/UTB1 bytes 导出，以及 JSON/bytes 运行时读取。

**Architecture:** Core 使用不依赖 UnityEngine 的中间模型和服务接口；GUI 只负责路径、选项、规则说明、执行和报告展示；Runtime Package 通过统一 reader 接口自动读取 JSON 或 UTB1 bytes。Excel 读取器通过可替换的数据源接口隔离，测试同时覆盖内存表格。

**Tech Stack:** C#/.NET 8、WPF Windows GUI、Unity C# Runtime Package、xUnit、ExcelDataReader 或等价 Open XML reader、UTB1 自有二进制协议。

**Spec:** docs/superpowers/specs/2026-10-04-universal-unity-table-tool-design.md

## Global Constraints

- 首版只实现 Windows GUI，不实现远程服务器、CI/CLI、macOS GUI 或 Linux GUI。
- 表头固定为第 1 至第 6 行，数据从第 7 行开始。
- 数组分隔符固定为一维 #、二维外层 | 内层 #、三维外层 ; 中层 | 内层 #。
- ## 行/列忽略；#test 和 #ceshi 行/列只校验不导出。
- 空字段名列不导出。
- JSON 和 UTB1 bytes 都必须能被 Runtime Package 自动识别和读取。
- 生产代码必须先有失败测试；每个任务完成后运行对应验证。
- 输入只有一个递归表根目录，输出分为数据目录和代码目录。
- Diff、远程服务、CI 配置和历史 bytes 兼容层不属于本版。

---

### Task 1: 建立源码、测试和发布目录

**Files:**
- Create: src/TableTool.Core/TableTool.Core.csproj
- Create: src/TableTool.Gui/TableTool.Gui.csproj
- Create: src/TableTool.Serialization/TableTool.Serialization.csproj
- Create: src/TableTool.Tests/TableTool.Tests.csproj
- Create: UnityRuntimePackage/com.company.unity-table-runtime/package.json
- Create: UnityRuntimePackage/com.company.unity-table-runtime/Runtime/Runtime.asmdef
- Create: UnityTableTool.sln
- Create: .gitignore
- Create: samples/Exported/.gitkeep

**Interfaces:**
- Core targets net8.0.
- GUI targets net8.0-windows and enables WPF.
- Runtime Package targets Unity-compatible C# without external NuGet dependencies.
- Tests reference Core and Serialization.

- [ ] Step 1: Write project files and the solution structure.
- [ ] Step 2: Run dotnet --info and dotnet build UnityTableTool.sln.
- [ ] Step 3: If the SDK is unavailable, keep source files and report that compilation is pending SDK installation.

### Task 2: Implement table model, singleton metadata, type grammar, and strict value parser

**Files:**
- Create: src/TableTool.Core/Models/TableSchema.cs
- Create: src/TableTool.Core/Models/TableDocument.cs
- Create: src/TableTool.Core/Models/ValidationIssue.cs
- Create: src/TableTool.Core/Parsing/TypeDescriptor.cs
- Create: src/TableTool.Core/Parsing/ValueParser.cs
- Test: src/TableTool.Tests/Parsing/ValueParserTests.cs
- Test: src/TableTool.Tests/Parsing/TypeDescriptorTests.cs

**Interfaces:**
~~~csharp
public sealed record TypeDescriptor(string BaseType, int Dimensions);
public static TypeDescriptor ParseType(string text);
public static object ParseValue(string text, TypeDescriptor type);
~~~

- [ ] Step 1: Write failing tests for int, int(), int()(), int()()(), the #/|/; protocol, invalid high-dimensional separators, empty elements, and non-integer values.
- [ ] Step 2: Run dotnet test src/TableTool.Tests --filter FullyQualifiedName~ValueParser and confirm failure caused by missing parser types.
- [ ] Step 3: Implement grammar parsing and recursive separator parsing with #, |, ;.
- [ ] Step 4: Run focused tests and confirm all pass.
- [ ] Step 5: Add tests for string(), float()(), and bool()()() and repeat the red-green cycle.

### Task 3: Implement schema parsing, singleton tables, comments, test rows and validation

**Files:**
- Create: src/TableTool.Core/Parsing/TableSchemaParser.cs
- Create: src/TableTool.Core/Validation/TableValidator.cs
- Create: src/TableTool.Core/Validation/ErrorCodes.cs
- Test: src/TableTool.Tests/Parsing/TableSchemaParserTests.cs
- Test: src/TableTool.Tests/Validation/TableValidatorTests.cs

**Interfaces:**
~~~csharp
public sealed record RawTableGrid(string SourceName, IReadOnlyList<IReadOnlyList<string?>> Rows);
public TableDocument Parse(RawTableGrid grid);
public IReadOnlyList<ValidationIssue> Validate(TableDocument document);
~~~

- [ ] Step 1: Write failing tests for six-row parsing, type:single metadata, singleton row count, blank field names, c/s/cs, blank target, ## rows/columns, and #test/#ceshi rows/columns.
- [ ] Step 2: Run focused tests and verify expected failures.
- [ ] Step 3: Implement schema parsing and row/column classification.
- [ ] Step 4: Implement primary key, duplicate key, unknown type, target marker, default value, and test-row validation.
- [ ] Step 5: Run all Core validation tests.

### Task 4: Implement deterministic JSON, UTB1 bytes, manifest and reports

**Files:**
- Create: src/TableTool.Serialization/JsonTableExporter.cs
- Create: src/TableTool.Serialization/BinaryTableExporter.cs
- Create: src/TableTool.Serialization/ManifestExporter.cs
- Create: src/TableTool.Serialization/ExportService.cs
- Create: src/TableTool.Serialization/Reports/ReportWriter.cs
- Test: src/TableTool.Tests/Serialization/ExportTests.cs

**Interfaces:**
~~~csharp
public ExportResult Export(TableDocument document, ExportOptions options);
public byte[] ExportBytes(TableDocument document);
public string ExportJson(TableDocument document);
~~~

- [ ] Step 1: Write failing tests for stable JSON, UTB1 magic, schema hash, CRC, and repeated-export byte equality.
- [ ] Step 2: Run serialization tests and verify failure before implementation.
- [ ] Step 3: Implement canonical JSON and a documented UTB1 header/payload format.
- [ ] Step 4: Implement manifest and staging-directory commit behavior.
- [ ] Step 5: Run serialization tests and inspect generated sample artifacts.

### Task 5: Implement Runtime Package JSON/bytes readers and singleton access

**Files:**
- Create: UnityRuntimePackage/com.company.unity-table-runtime/Runtime/ITableDataReader.cs
- Create: UnityRuntimePackage/com.company.unity-table-runtime/Runtime/JsonTableDataReader.cs
- Create: UnityRuntimePackage/com.company.unity-table-runtime/Runtime/BinaryTableDataReader.cs
- Create: UnityRuntimePackage/com.company.unity-table-runtime/Runtime/TableManager.cs
- Create: UnityRuntimePackage/com.company.unity-table-runtime/Runtime/TableRuntimeSettings.cs
- Test: src/TableTool.Tests/Runtime/RuntimeFormatDetectionTests.cs

**Interfaces:**
~~~csharp
public interface ITableDataReader
{
    bool CanRead(ReadOnlySpan<byte> data);
    TableRuntimeDocument Read(ReadOnlySpan<byte> data);
}
public static TableRuntimeDocument ReadAuto(byte[] data, string manifestFormat = null);
~~~

- [ ] Step 1: Write failing tests for manifest preference, UTB1 detection, JSON detection, unknown format, schema mismatch, key lookup and GetSingleton.
- [ ] Step 2: Run focused runtime tests and verify failure.
- [ ] Step 3: Implement readers and automatic detection without UnityEngine dependencies in the testable portion.
- [ ] Step 4: Add Unity-facing wrappers and reload/load callback interfaces.
- [ ] Step 5: Run runtime tests and verify deterministic lookup behavior.

### Task 6: Implement recursive-root Windows GUI and rules view

**Files:**
- Create: src/TableTool.Gui/App.xaml
- Create: src/TableTool.Gui/App.xaml.cs
- Create: src/TableTool.Gui/MainWindow.xaml
- Create: src/TableTool.Gui/MainWindow.xaml.cs
- Create: src/TableTool.Gui/Rules/FieldRulesText.cs
- Create: src/TableTool.Gui/Services/GuiExportService.cs

**Interfaces:**
- GUI invokes Core and Serialization services.
- Rules view is read-only and displays table format, field targets, comment/test markers, separators, outputs and runtime detection.
- GUI has placeholders for platform launchers and future CLI/CI integration.

- [ ] Step 1: Write a view-model test for rules text and export option mapping.
- [ ] Step 2: Run the test and confirm failure.
- [ ] Step 3: Implement the minimal WPF window with one input-root selector, data-output selector, code-output selector, scan/validate/export buttons, output format checkboxes, result list and rules tab.
- [ ] Step 4: Add clear error messages and disable export when validation contains errors.
- [ ] Step 5: Build the GUI project when a .NET SDK is available.

### Task 7: Add sample tables and Chinese documentation

**Files:**
- Create: samples/Skill.xlsx
- Create: samples/Item.xlsx
- Create: samples/README.md
- Create: README.md
- Create: docs/README.md
- Create: docs/TableFormat.md
- Create: docs/FieldRules.md
- Create: docs/ExportFormats.md
- Create: docs/RuntimeIntegration.md
- Create: docs/Troubleshooting.md
- Create: docs/ExtensionPoints.md

**Interfaces:**
- Samples include scalar values, all three array dimensions with #/|/;, a singleton table, client/server markers, ignored columns, ignored rows, test rows and test columns.
- Documentation explains actual source and output paths and the current build limitation if the SDK is unavailable.

- [ ] Step 1: Write sample grids that follow the six-row format.
- [ ] Step 2: Add documentation with copy-pasteable Unity Package installation and GUI usage steps.
- [ ] Step 3: Generate sample JSON/bytes after export code is available.
- [ ] Step 4: Verify all referenced paths and rule examples against the design spec.

### Task 8: Final verification and release layout

**Files:**
- Create: build/README.md
- Create: release/README.md
- Create: release/UnityRuntimePackage/...
- Create: release/samples/...

- [ ] Step 1: Run the full test command and build command.
- [ ] Step 2: Check release layout against the design spec.
- [ ] Step 3: Verify no Diff tool, CLI executable, remote configuration or platform GUI implementation was added.
- [ ] Step 4: Report verified checks and any SDK/tooling limitations.
