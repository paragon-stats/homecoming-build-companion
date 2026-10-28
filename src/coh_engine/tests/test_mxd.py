"""Tests for the .mxd share-wrapper and payload decode layer."""

import zlib

import pytest

from coh_engine.buildfile.mxd import (
    classify,
    decode_mxd,
    decode_share_block,
    hex_decode,
)


def _break_string(payload: str, width: int = 67) -> str:
    """Mirror ModernZlib.BreakString: width-char chunks, each wrapped in ``|``."""
    return "\r\n".join(f"|{payload[i : i + width]}|" for i in range(0, len(payload), width))


def _make_mxd_share(buffer: bytes) -> str:
    """Build a valid .mxd share block from a raw binary buffer."""
    compressed = zlib.compress(buffer, zlib.Z_BEST_COMPRESSION)
    encoded = compressed.hex().upper()
    header = f"|MxDz;{len(buffer)};{len(compressed)};{len(encoded)};HEX;|"
    return header + "\r\n" + _break_string(encoded)


def test_classify_recognizes_formats() -> None:
    assert classify("|MxDz;10;8;16;HEX;|\r\n|AABB|") == "mxd"
    assert classify("|MBD;10;8;16;BASE64;|\r\ndGVzdA==") == "mbd"
    assert classify("dGVzdGRhdGE=") == "unkbase64"
    assert classify("not a share block !!!") is None
    assert classify("   ") is None


def test_decode_mxd_round_trip() -> None:
    original = bytes(range(256)) * 4  # 1 KiB, spans BreakString line wrapping
    share = _make_mxd_share(original)
    assert decode_mxd(share) == original


def test_decode_share_block_fields() -> None:
    block = decode_share_block("|MxDz;100;40;80;HEX;|\r\n|DEADBEEF|")
    assert block.magic == "MxDz"
    assert block.uncompressed_size == 100
    assert block.compressed_size == 40
    assert block.encoded_size == 80
    assert block.encoding == "HEX"
    assert block.payload == "DEADBEEF"


def test_decode_share_block_rejects_wrong_item_count() -> None:
    with pytest.raises(ValueError, match="exactly 5 items"):
        decode_share_block("|MxDz;100;40;HEX;|\r\n|DEADBEEF|")


def test_decode_share_block_rejects_non_integer_sizes() -> None:
    with pytest.raises(ValueError, match="sizes must be integers"):
        decode_share_block("|MxDz;abc;40;80;HEX;|\r\n|DEADBEEF|")


def test_decode_share_block_needs_payload_line() -> None:
    with pytest.raises(ValueError, match="at least one payload line"):
        decode_share_block("|MxDz;100;40;80;HEX;|")


def test_decode_share_block_stops_at_trailing_banner() -> None:
    # A pipes-only separator line is skipped; payload collection then halts at the
    # trailing "|----|" banner rather than treating its dashes as payload.
    block = decode_share_block("|MxDz;10;4;8;HEX;|\r\n||\r\n|DEADBEEF|\r\n|--------|")
    assert block.payload == "DEADBEEF"


def test_hex_decode_rejects_non_hex() -> None:
    with pytest.raises(ValueError, match="not valid hex"):
        hex_decode("XYZ123")


def test_decode_mxd_rejects_non_mxd() -> None:
    with pytest.raises(ValueError, match="not an mxd share block"):
        decode_mxd("|MBD;10;8;16;BASE64;|\r\ndGVzdA==")


def test_decode_mxd_rejects_compressed_size_mismatch() -> None:
    original = b"hello world" * 8
    compressed = zlib.compress(original, zlib.Z_BEST_COMPRESSION)
    encoded = compressed.hex().upper()
    # Declare a wrong compressed size.
    bad = f"|MxDz;{len(original)};{len(compressed) + 1};{len(encoded)};HEX;|\r\n" + _break_string(encoded)
    with pytest.raises(ValueError, match="compressed size mismatch"):
        decode_mxd(bad)


def test_decode_mxd_rejects_bad_zlib() -> None:
    junk = b"\xff\xff\xff\xff\xff\xff"
    encoded = junk.hex().upper()
    bad = f"|MxDz;10;{len(junk)};{len(encoded)};HEX;|\r\n" + _break_string(encoded)
    with pytest.raises(ValueError, match="zlib inflate failed"):
        decode_mxd(bad)


def test_classify_no_header_and_not_base64() -> None:
    assert classify("|----|\r\n|garbage line with spaces|") is None


def test_decode_share_block_no_header_raises() -> None:
    with pytest.raises(ValueError, match=r"no .* header line found"):
        decode_share_block("|----|\r\n|AABBCC|")


def test_read_mxd_build_missing_magic_raises() -> None:
    from coh_engine.buildfile.mxd import read_mxd_build

    with pytest.raises(ValueError, match="magic number"):
        read_mxd_build(b"\x00" * 8, {}, {})


def test_read_mxd_build_newer_version_raises() -> None:
    import struct

    from coh_engine.buildfile.mxd import _MAGIC, read_mxd_build

    buf = _MAGIC + struct.pack("<f", 9.99)
    with pytest.raises(ValueError, match="newer version"):
        read_mxd_build(buf, {}, {})


def test_single_line_share_block_reads_like_the_multi_line_form() -> None:
    original = bytes(range(256)) * 4
    multi = _make_mxd_share(original)
    single = multi.replace("\r\n", "")
    assert "\n" not in single
    assert classify(single) == "mxd"
    assert decode_mxd(single) == decode_mxd(multi) == original
