#nullable enable
// 用途：读取带 UTB1 magic、版本、长度和 CRC 的客户端二进制表数据。
// 编写日期：2026-10-10
// 作者：Codex（按用户需求修改）

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Company.UnityTableRuntime
{
    public static class BinaryTableDataReader
    {
        public static bool CanRead(byte[] data) => data.Length >= 4 && Encoding.ASCII.GetString(data, 0, 4) == "UTB1";

        public static TableRuntimeDocument Read(byte[] data)
        {
            if (!CanRead(data))
                throw new FormatException("UTB1 magic is missing.");
            var offset = 4;
            var version = ReadInt(data, ref offset);
            if (version != 1)
                throw new FormatException($"Unsupported UTB1 version {version}.");
            var tableName = ReadString(data, ref offset);
            _ = ReadString(data, ref offset);
            var payloadLength = ReadInt(data, ref offset);
            if (payloadLength < 0 || payloadLength > data.Length - offset - 4)
                throw new FormatException("UTB1 payload length is invalid.");
            var payload = data.AsSpan(offset, payloadLength).ToArray();
            offset += payloadLength;
            var expectedCrc = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));
            if (Crc32.Compute(payload) != expectedCrc)
                throw new FormatException("UTB1 payload CRC does not match.");
            if (offset + 4 != data.Length)
                throw new FormatException("UTB1 contains trailing data.");
            return JsonTableDataReader.Read(payload);
        }

        private static int ReadInt(byte[] data, ref int offset)
        {
            if (offset + 4 > data.Length)
                throw new FormatException("UTB1 header is truncated.");
            var value = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));
            offset += 4;
            return value;
        }

        private static string ReadString(byte[] data, ref int offset)
        {
            var length = ReadInt(data, ref offset);
            if (length < 0 || length > data.Length - offset)
                throw new FormatException("UTB1 string length is invalid.");
            var value = Encoding.UTF8.GetString(data, offset, length);
            offset += length;
            return value;
        }

        private static class Crc32
        {
            private static readonly uint[] Table = BuildTable();

            public static uint Compute(ReadOnlySpan<byte> data)
            {
                var crc = 0xffffffffu;
                foreach (var value in data)
                    crc = Table[(crc ^ value) & 0xff] ^ crc >> 8;
                return ~crc;
            }

            private static uint[] BuildTable()
            {
                var table = new uint[256];
                for (uint i = 0; i < table.Length; i++)
                {
                    var value = i;
                    for (var bit = 0; bit < 8; bit++)
                        value = (value & 1) == 1 ? 0xedb88320u ^ value >> 1 : value >> 1;
                    table[i] = value;
                }
                return table;
            }
        }
    }
}
