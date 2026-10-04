namespace TableTool.Gui.Rules;

public static class FieldRulesText
{
    public const string Content = """
UnityTableTool 字段规则

表结构：
第 1 行 table: 表名；第 2 行字段说明；第 3 行字段类型；第 4 行字段名；第 5 行客户端服务器区分；第 6 行默认值；第 7 行开始是数据。

字段范围：空或 cs = 客户端和服务器；c = 客户端；s = 服务器。
空字段名不会导出。

注释和测试：
## 开头的行或列不参与打表。
#test 和 #ceshi 开头的行或列会校验，但不会进入最终数据。

类型和拆分：
int() 使用 #；int()() 外层按 |、内层按 #；int()()() 外层按 ;、中层按 |、内层按 #。
分隔符固定，最多三维；错误层级或空元素会阻止导出。

单例表：
第一行追加 type:single；前几行用 id、type、data 语义列识别，desc 可选，列顺序任意；正式配置生成一个单例对象，运行时使用 GetSingleton。

输出：
C# 输出到代码目录；JSON、UTB1 bytes 和 manifest 输出到数据目录。JSON 用于调试和开发环境；UTB1 bytes 用于正式运行时。
Runtime Package 会优先使用 manifest 声明，其次检测 UTB1 magic，最后检测 JSON。
""";
}
