// 用途：为生成代码的编译测试提供最小 Unity 类型声明，不模拟引擎行为。
// 编写日期：2026-10-15
// 作者：Codex（按用户需求修改）

namespace TableTool.Tests.Serialization;

internal static class UnityTypeStubs
{
    public const string Source = """
        // Purpose: Supply constructor signatures for generated-code compilation tests.
        // Written: 2026-10-15
        // Author: Codex
        namespace UnityEngine
        {
            public struct Vector2 { public Vector2(float x, float y) { } }
            public struct Vector3 { public Vector3(float x, float y, float z) { } }
            public struct Vector4 { public Vector4(float x, float y, float z, float w) { } }
            public struct Quaternion { public Quaternion(float x, float y, float z, float w) { } }
            public struct Color { public Color(float r, float g, float b, float a) { } }
        }
        """;
}
