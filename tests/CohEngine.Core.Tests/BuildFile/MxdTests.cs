using System.Globalization;
using System.IO.Compression;
using System.Text;

using CohEngine.Core.BuildFile;

namespace CohEngine.Core.Tests.BuildFile;

/// <summary>Port of <c>test_mxd.py</c>: the share wrapper and payload decode layer.</summary>
public sealed class MxdTests
{
    internal static string MakeShare(byte[] buffer)
    {
        using MemoryStream output = new();
        using (ZLibStream zlib = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(buffer);
        }

        byte[] compressed = output.ToArray();
        string encoded = Convert.ToHexString(compressed);
        return $"|MxDz;{buffer.Length};{compressed.Length};{encoded.Length};HEX;|\r\n" + BreakString(encoded);
    }

    private static string BreakString(string payload)
    {
        const int Width = 67;
        StringBuilder builder = new();
        for (int start = 0; start < payload.Length; start += Width)
        {
            builder.Append('|').Append(payload.AsSpan(start, Math.Min(Width, payload.Length - start))).Append("|\r\n");
        }

        return builder.ToString().TrimEnd('\r', '\n');
    }

    private static string[] HeaderItems(string share)
    {
        return share[..share.IndexOf('\r', StringComparison.Ordinal)].Split(';');
    }

    private static byte[] Kilobyte()
    {
        return [.. Enumerable.Range(0, 1024).Select(value => (byte)value)];
    }

    [Fact]
    public void Classify_recognizes_the_formats()
    {
        Assert.Equal("mxd", Mxd.Classify("|MxDz;10;8;16;HEX;|\r\n|AABB|"));
        Assert.Equal("mbd", Mxd.Classify("|MBD;10;8;16;BASE64;|\r\ndGVzdA=="));
        Assert.Equal("unkbase64", Mxd.Classify("dGVzdGRhdGE="));
        Assert.Null(Mxd.Classify("not a share block !!!"));
        Assert.Null(Mxd.Classify("   "));
        Assert.Null(Mxd.Classify("|MxDz;1;2;x;HEX;|"));
        Assert.Null(Mxd.Classify("|MxDz;1;2;3|"));
        Assert.Null(Mxd.Classify("|MxDz;;2;3;HEX;|"));
        Assert.Null(Mxd.Classify("a=b"));
        Assert.Null(Mxd.Classify("ab==="));
        Assert.Null(Mxd.Classify("|----|\r\n|garbage line with spaces|"));
        Assert.Null(Mxd.Classify("dGVzdA==\r\ndGVzdA=="));
    }

    [Fact]
    public void Decode_round_trips_across_line_wrapping()
    {
        Assert.Equal(Kilobyte(), Mxd.DecodeMxd(MakeShare(Kilobyte())));
    }

    [Fact]
    public void A_single_line_share_block_reads_like_the_multi_line_form()
    {
        string multi = MakeShare(Kilobyte());
        string single = multi.Replace("\r\n", string.Empty, StringComparison.Ordinal);

        Assert.Equal("mxd", Mxd.Classify(single));
        Assert.Equal(Mxd.DecodeMxd(multi), Mxd.DecodeMxd(single));
    }

    [Fact]
    public void Share_block_fields_are_read()
    {
        ShareBlock block = Mxd.DecodeShareBlock("|MxDz;100;40;80;HEX;|\r\n|DEADBEEF|");

        Assert.Equal(new ShareBlock("MxDz", 100, 40, 80, "HEX", "DEADBEEF"), block);
    }

    [Theory]
    [InlineData("|MxDz;100;40;HEX;|\r\n|DEADBEEF|", "E112: share header must have exactly 5 items, got 4")]
    [InlineData("|MxDz;abc;40;80;HEX;|\r\n|DEADBEEF|", "E112: share header sizes must be integers")]
    [InlineData("|MxDz;100;40;80;HEX;|", "E112: share block needs a header line and at least one payload line")]
    [InlineData("|----|\r\n|AABBCC|", "E112: no |MxDz;…| or |MBD;…| header line found")]
    public void A_malformed_envelope_is_E112(string text, string message)
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Mxd.DecodeShareBlock(text));

        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void Payload_collection_stops_at_the_trailing_banner()
    {
        Assert.Equal("DEADBEEF", Mxd.DecodeShareBlock("|MxDz;10;4;8;HEX;|\r\n||\r\n|DEADBEEF|\r\n|--------|").Payload);
    }

    [Theory]
    [InlineData("XYZ123")]
    [InlineData("ABC")]
    public void Non_hex_is_E113(string data)
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Mxd.HexDecode(data));

        Assert.Equal("E113: mxd payload is not valid hex", exception.Message);
    }

    [Fact]
    public void A_non_mxd_block_is_E113()
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Mxd.DecodeMxd("|MBD;10;8;16;BASE64;|\r\ndGVzdA=="));

        Assert.Equal("E113: not an mxd share block", exception.Message);
    }

    [Fact]
    public void A_compressed_size_mismatch_is_E113()
    {
        string share = MakeShare(Encoding.ASCII.GetBytes("hello world hello world"));
        string[] items = HeaderItems(share);
        string bad = share.Replace($";{items[2]};", $";{int.Parse(items[2], CultureInfo.InvariantCulture) + 1};", StringComparison.Ordinal);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Mxd.DecodeMxd(bad));

        Assert.Contains("compressed size mismatch", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Bad_zlib_is_E113()
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Mxd.DecodeMxd("|MxDz;10;6;12;HEX;|\r\n|FFFFFFFFFFFF|"));

        Assert.Equal("E113: mxd zlib inflate failed", exception.Message);
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(2000, 1024)]
    public void The_declared_uncompressed_size_cuts_the_buffer(int declared, int expected)
    {
        string share = MakeShare(Kilobyte());
        string[] items = HeaderItems(share);
        string resized = share.Replace($"|MxDz;{items[1]};", $"|MxDz;{declared};", StringComparison.Ordinal);

        Assert.Equal(expected, Mxd.DecodeMxd(resized).Length);
    }
}
