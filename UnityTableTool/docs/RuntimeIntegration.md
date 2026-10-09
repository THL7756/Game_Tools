# Runtime Package 接入

将 UnityRuntimePackage/com.company.unity-table-runtime 加入 Unity 项目的 Packages 目录，或在 Packages/manifest.json 使用本地路径引用。

Runtime Package 只消费工具已经生成的客户端数据。表结构、字段端别、默认值、主键、引用和 Excel/Sheet 规则在工具打表阶段完成；Runtime 不重复校验 schemaHash，该字段只保留用于诊断。读取器仍会检查 JSON 结构和 UTB1 的 magic、版本、长度与 CRC，避免损坏输入导致未定义行为。

工具和 Runtime 共用 Runtime/Shared 下的类型和值解析源码。工具端的 TypeDescriptor、ValueParser 是兼容入口，实际规则只有一份。

    var manager = new Company.UnityTableRuntime.TableManager();
    manager.Load("Skill", bytesOrJson);
    var skill = manager.Get("Skill", "1001");
    var config = manager.GetSingleton("GlobalConfig");

客户端生成的 SkillData.cs 会包含一个轻量的 SkillTableAdapter，适配器只负责把行字段映射到生成类型：

    var skill = manager.Get("Skill", "1001", SkillTableAdapter.FromRow);
    var allSkills = manager.GetAll("Skill", SkillTableAdapter.FromRow);
    var config = manager.GetSingleton("GlobalConfig", GlobalConfigTableAdapter.FromRow);

TableRowView.Read<T> 使用导出数据中的字段定义、默认值和数组分隔符完成基础类型、enum、数组及结构值分量读取。Unity 结构类型由生成代码构造，Runtime Package 本身不依赖 UnityEngine。

正式构建推荐使用 bytes；开发和调试可以使用 JSON。可以通过 TableRuntimeSettings.LoadBytes 接入 AssetBundle、Addressables 或其他客户端资源来源：

    manager.LoadClient("Skill", new TableRuntimeSettings
    {
        LoadBytes = tableName => LoadClientAsset(tableName)
    });
