using System.Buffers.Binary;
using System.Text;

using CohEngine.Core.BuildFile;
using CohEngine.Core.Maths;

namespace CohEngine.Core.Tests.BuildFile;

/// <summary>Port of <c>test_binary.py</c>: the .NET <c>BinaryReader</c> primitives.</summary>
public sealed class BinaryCursorTests
{
    private static byte[] DotnetString(string text)
    {
        byte[] raw = Encoding.UTF8.GetBytes(text);
        List<byte> output = [];
        int length = raw.Length;
        while (true)
        {
            byte group = (byte)(length & 0x7F);
            length >>= 7;
            if (length == 0)
            {
                output.Add(group);
                break;
            }

            output.Add((byte)(group | 0x80));
        }

        return [.. output, .. raw];
    }

    private static byte[] Int32Bytes(int value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        return bytes;
    }

    [Fact]
    public void Scalars_round_trip()
    {
        byte[] int64 = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(int64, 9_000_000_000);
        byte[] single = new byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(single, 1.5f);
        BinaryCursor cursor = new([.. Int32Bytes(-12345), .. int64, .. single, 1, 0xF9]);

        Assert.Equal(-12345, cursor.ReadInt32());
        Assert.Equal(9_000_000_000, cursor.ReadInt64());
        Assert.Equal(F32.Round(1.5), cursor.ReadSingle());
        Assert.True(cursor.ReadBoolean());
        Assert.Equal(-7, cursor.ReadSByte());
    }

    [Fact]
    public void Strings_short_and_long()
    {
        string longValue = new('X', 200);
        BinaryCursor cursor = new([.. DotnetString("Pool.Leaping"), .. DotnetString(longValue)]);

        Assert.Equal("Pool.Leaping", cursor.ReadString());
        Assert.Equal(longValue, cursor.ReadString());
    }

    [Fact]
    public void Array_count_idiom_adds_one()
    {
        BinaryCursor cursor = new([.. Int32Bytes(0), .. Int32Bytes(4)]);

        Assert.Equal(1, cursor.ReadArrayCount());
        Assert.Equal(5, cursor.ReadArrayCount());
    }

    [Fact]
    public void Array_count_of_int_max_does_not_wrap()
    {
        Assert.Equal(2_147_483_648L, new BinaryCursor(Int32Bytes(int.MaxValue)).ReadArrayCount());
    }

    [Fact]
    public void Boolean_zero_is_false()
    {
        Assert.False(new BinaryCursor([0]).ReadBoolean());
    }

    [Fact]
    public void Reading_past_the_end_is_E110()
    {
        BinaryCursor cursor = new([1, 2]);

        EndOfStreamException exception = Assert.Throws<EndOfStreamException>(() => cursor.ReadInt32());

        Assert.StartsWith("E110: read past end of buffer at 0 (+4)", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_string_length_past_the_buffer_is_E110()
    {
        BinaryCursor cursor = new([0xFF, 0xFF, 0xFF, 0xFF, 0x0F]);

        Assert.Throws<EndOfStreamException>(() => cursor.ReadString());
    }

    [Fact]
    public void A_seven_bit_length_of_six_groups_is_E111()
    {
        BinaryCursor cursor = new([0x80, 0x80, 0x80, 0x80, 0x80]);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => cursor.Read7BitLength());

        Assert.Equal(5, cursor.Length);
        Assert.StartsWith("E111: 7-bit length", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_string_that_is_not_utf8_is_E119()
    {
        BinaryCursor cursor = new([2, 0xC3, 0x28]);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => cursor.ReadString());

        Assert.StartsWith("E119:", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Position_tracks_reads_and_bytes_are_copied()
    {
        BinaryCursor cursor = new([.. Int32Bytes(1), .. Int32Bytes(2)]);

        Assert.Equal(0, cursor.Position);
        Assert.Equal([1, 0, 0, 0], cursor.ReadBytes(4));
        Assert.Equal(4, cursor.Position);
        Assert.Equal(8, cursor.Length);
        Assert.Equal(2, cursor.ReadInt32());
    }

    [Fact]
    public void A_start_position_skips_the_head()
    {
        Assert.Equal(7, new BinaryCursor([1, 2, 7], 2).ReadSByte());
    }
}
