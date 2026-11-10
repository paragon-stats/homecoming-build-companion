using System.Buffers.Binary;
using System.Text;

namespace CohEngine.Core.BuildFile;

/// <summary>A forward-only reader over a <c>.mxd</c> binary buffer, with .NET <c>BinaryReader</c> encodings.</summary>
public sealed class BinaryCursor
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly byte[] buffer;
    private int position;

    /// <summary>Initializes a new instance of the <see cref="BinaryCursor"/> class.</summary>
    /// <param name="buffer">The bytes to read.</param>
    /// <param name="position">The offset to start at.</param>
    public BinaryCursor(byte[] buffer, int position = 0)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        this.buffer = buffer;
        this.position = position;
    }

    /// <summary>Gets the current byte offset.</summary>
    public int Position => position;

    /// <summary>Gets the total buffer length in bytes.</summary>
    public int Length => buffer.Length;

    /// <summary>Reads raw bytes.</summary>
    /// <param name="count">How many bytes.</param>
    /// <returns>A copy of the bytes.</returns>
    /// <exception cref="EndOfStreamException">E110: fewer than <paramref name="count"/> bytes remain.</exception>
    public byte[] ReadBytes(long count)
    {
        return Take(count).ToArray();
    }

    /// <summary>Reads a 1-byte boolean (0 is false, anything else true).</summary>
    /// <returns>The value.</returns>
    public bool ReadBoolean()
    {
        return Take(1)[0] != 0;
    }

    /// <summary>Reads a signed byte.</summary>
    /// <returns>The value.</returns>
    public sbyte ReadSByte()
    {
        return (sbyte)Take(1)[0];
    }

    /// <summary>Reads a little-endian signed 32-bit integer.</summary>
    /// <returns>The value.</returns>
    public int ReadInt32()
    {
        return BinaryPrimitives.ReadInt32LittleEndian(Take(4));
    }

    /// <summary>Reads a little-endian signed 64-bit integer.</summary>
    /// <returns>The value.</returns>
    public long ReadInt64()
    {
        return BinaryPrimitives.ReadInt64LittleEndian(Take(8));
    }

    /// <summary>Reads a little-endian IEEE-754 single, widened to a double (already single-precise).</summary>
    /// <returns>The value.</returns>
    public double ReadSingle()
    {
        return BinaryPrimitives.ReadSingleLittleEndian(Take(4));
    }

    /// <summary>Reads a 7-bit encoded (LEB128) unsigned length prefix of at most five groups.</summary>
    /// <returns>The length.</returns>
    /// <exception cref="InvalidDataException">E111: the fifth group still sets the continuation bit.</exception>
    public long Read7BitLength()
    {
        long result = 0;
        for (int shift = 0; shift < 35; shift += 7)
        {
            byte group = Take(1)[0];
            result |= (long)(group & 0x7F) << shift;
            if ((group & 0x80) == 0)
            {
                return result;
            }
        }

        throw new InvalidDataException($"{BuildFileErrorCodes.LengthPrefixTooLong}: 7-bit length prefix too long; the file is corrupt or not a Mids export, re-export it from Mids");
    }

    /// <summary>Reads a length-prefixed UTF-8 string.</summary>
    /// <returns>The string.</returns>
    /// <exception cref="InvalidDataException">E119: the bytes are not valid UTF-8.</exception>
    public string ReadString()
    {
        long length = Read7BitLength();
        try
        {
            return StrictUtf8.GetString(Take(length));
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException($"{BuildFileErrorCodes.StringEncoding}: string is not valid UTF-8; the file is corrupt or not a Mids export, re-export it from Mids", exception);
        }
    }

    /// <summary>Reads the <c>length - 1</c> array-count idiom.</summary>
    /// <returns>The true count.</returns>
    public long ReadArrayCount()
    {
        return (long)ReadInt32() + 1;
    }

    private ReadOnlySpan<byte> Take(long count)
    {
        if (count > buffer.Length - position)
        {
            throw new EndOfStreamException($"{BuildFileErrorCodes.StreamTruncated}: read past end of buffer at {position} (+{count}); the file is truncated or not a Mids export, re-export it from Mids");
        }

        ReadOnlySpan<byte> chunk = buffer.AsSpan(position, (int)count);
        position += (int)count;
        return chunk;
    }
}
