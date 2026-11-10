namespace CohEngine.Core.BuildFile;

/// <summary>The error codes the build-file readers own (E110 to E119); each prefixes its message as "E110: ...".</summary>
public static class BuildFileErrorCodes
{
    /// <summary>A read ran past the end of a binary buffer.</summary>
    public const string StreamTruncated = "E110";

    /// <summary>A 7-bit encoded length prefix ran past five groups.</summary>
    public const string LengthPrefixTooLong = "E111";

    /// <summary>A share block lacks its header, payload line or well-formed header items.</summary>
    public const string ShareEnvelope = "E112";

    /// <summary>A share payload is not decodable: wrong format, bad hex, size mismatch or a zlib failure.</summary>
    public const string SharePayload = "E113";

    /// <summary>A binary build holds no magic number.</summary>
    public const string MagicMissing = "E114";

    /// <summary>A binary build was saved by a newer version than the reader knows.</summary>
    public const string VersionTooNew = "E115";

    /// <summary>A binary build uses a form the reader does not support, or names an unknown class.</summary>
    public const string Unsupported = "E116";

    /// <summary>An enum ordinal or name is outside its table.</summary>
    public const string EnumOutOfRange = "E117";

    /// <summary>A JSON document is unreadable: invalid JSON, a missing required field or a missing BuiltWith block.</summary>
    public const string DocumentUnreadable = "E118";

    /// <summary>A length-prefixed string is not valid UTF-8.</summary>
    public const string StringEncoding = "E119";
}
