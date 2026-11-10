namespace CohEngine.Core.BuildFile;

/// <summary>A parsed share-text envelope: header sizes plus the joined payload string.</summary>
/// <param name="Magic">The header magic.</param>
/// <param name="UncompressedSize">The declared uncompressed size.</param>
/// <param name="CompressedSize">The declared compressed size.</param>
/// <param name="EncodedSize">The declared encoded size.</param>
/// <param name="Encoding">The encoding name.</param>
/// <param name="Payload">The payload lines joined.</param>
public sealed record ShareBlock(string Magic, int UncompressedSize, int CompressedSize, int EncodedSize, string Encoding, string Payload);
