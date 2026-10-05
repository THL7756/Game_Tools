namespace TableTool.Gui.Rules;

public static class FieldTypeExamplesText
{
    public const string Content = """
字段类型示例

基础类型
int       整数，例如：100
long      长整数，例如：9000000000
float     单精度小数，例如：1.25
double    双精度小数，例如：3.1415926
bool      布尔值，例如：true 或 false
string    文本，例如：hello

Unity 常用类型
Vector2      例如：1,2
Vector3      例如：1,2,3
Vector4      例如：1,2,3,4
Color        例如：1,0.5,0,1
Quaternion   例如：0,0,0,1

数组类型
int()       一维数组，使用 # 分隔，例如：1#2#3
int()()     二维数组，行使用 |、列使用 #，例如：1#2|3#4
int()()()   三维数组，层使用 ;、行使用 |、列使用 #，例如：1#2|3#4;5#6|7#8

数组后缀最多三层，字段类型必须写在“字段类型”行，字段名写在下一行。
""";
}
