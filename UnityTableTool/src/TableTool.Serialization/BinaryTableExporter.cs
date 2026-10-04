using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using TableTool.Core.Models;

namespace TableTool.Serialization;

public sealed class BinaryTableExporter
{
    public byte[] Export(TableDocument document)
    {
        var payload = Encoding.UTF8.GetBytes(new JsonTableExporter().Export(document));
        var tableName = Encoding.UTF8.GetBytes(document.Schema.Name);
        var schemaHash = Encoding.UTF8.GetBytes(SchemaHasher.Compute(document.Schema));
        var crc = Crc32.Compute(payload);
        var headerLength = 4 + 4 + 4 + tableName.Length + 4 + schemaHash.Length + 4;
        var output = new byte[headerLength + payload.Length + 4];
        var offset = 0;
        Encoding.ASCII.GetBytes("UTB1").CopyTo(output, offset);
        offset += 4;
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(offset, 4), 1);
        offset += 4;
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(offset, 4), tableName.Length);
        offset += 4;
        tableName.CopyTo(output, offset);
        offset += tableName.Length;
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(offset, 4), schemaHash.Length);
        offset += 4;
        schemaHash.CopyTo(output, offset);
        offset += schemaHash.Length;
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(offset, 4), payload.Length);
        offset += 4;
        payload.CopyTo(output, offset);
        offset += payload.Length;
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(offset, 4), crc);
        return output;
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
