"""Reader for the ``.mxd`` legacy binary build format — shared-text decode layer.

Covers the format's outer envelope (spec § data-and-build-formats, B.1-B.2):

- **Share wrapper** (``BuildManager.cs:320-351``): a ``|MxDz;u;c;e;HEX;|`` header line
    followed by ``BreakString``-wrapped payload lines. Exactly 5 ``;``-items; integer
    sizes; payload is every line after the header joined with ``|`` bookends stripped.
- **Payload decode** (``BuildManager.cs:364-382`` + ``ModernZlib.cs``): validate hex,
    decode to bytes, reject if byte length != the header compressed size, then zlib
    (RFC1950, ``Z_BEST_COMPRESSION``) inflate and resize to the uncompressed size.

The semantic parse of the decompressed buffer (``MxDReadSaveData`` -> power/slot
records, StaticIndex resolution, Homecoming migrations) is :func:`read_mxd_build`,
built on :class:`coh_engine.buildfile.binary.Cursor` and the harness StaticIndex
maps in :mod:`coh_engine.buildfile.dbindex`.

The ``.mxd`` format is legacy (Mids' current builds are ``.mbd``); this reader
exists to import older shared builds. New work targets ``.mbd``.
"""

import re
import zlib
from dataclasses import dataclass, field

from coh_engine.archetypes import ArchetypeDb
from coh_engine.buildfile.binary import Cursor
from coh_engine.buildfile.dbindex import EnhIndexEntry
from coh_engine.buildfile.mbd import Enhancement, PowerEntry, Slot, SubPower
from coh_engine.maths import f32

_MXD_HEADER = re.compile(r"^\|MxDz;[0-9]+;[0-9]+;[0-9]+;HEX;\|")
_MBD_HEADER = re.compile(r"^\|MBD;[0-9]+;[0-9]+;[0-9]+;BASE64;\|")
# Loose prefix used to locate the header line so a malformed header still yields
# a specific validation error (item count / non-integer sizes), as Mids does.
_HEADER_PREFIX = re.compile(r"^\|(?:MxDz|MBD);")
_UNK_BASE64 = re.compile(r"^[A-Za-z0-9+/]+={0,2}$")
_HEX = re.compile(r"\A[0-9A-Fa-f]+\Z")
# A payload line, after stripping the `|` bookends, is pure hex (mxd) or base64
# (mbd) — never the banner's dashes. This bound lets the reader accept the full
# MxDBuildSaveString output (leading "Do not modify" banner + trailing dashes).
_PAYLOAD_LINE = re.compile(r"\A[0-9A-Za-z+/=]+\Z")


def _lines(text: str) -> list[str]:
    """Split a share block into lines; ``||`` marks a line break in single-line files.

    MidsReborn does the same before classifying or decoding
    (``MidsCharacterFileFormat.cs:1157,1254``).
    """
    return [ln for ln in re.split(r"[\r\n]+", text.replace("||", "|\n|")) if ln != ""]


def _find_header_index(lines: list[str]) -> int | None:
    """Return the index of the first ``|MxDz;…`` / ``|MBD;…`` header line."""
    for i, line in enumerate(lines):
        if _HEADER_PREFIX.match(line):
            return i
    return None


def classify(text: str) -> str | None:
    """Classify a share block, tolerating a leading banner (``DataClassifier.cs``).

    Mids classifies already-stripped content; a real ``.mxd``/``.mbd`` export
    prefixes a "Do not modify" banner, so the header line is located rather than
    assumed to be line 0. Returns ``"mxd"``, ``"mbd"``, ``"unkbase64"``, or ``None``.
    """
    lines = _lines(text)
    for line in lines:
        if _MXD_HEADER.match(line):
            return "mxd"
        if _MBD_HEADER.match(line):
            return "mbd"
    stripped = text.strip()
    if stripped and "\n" not in stripped and _UNK_BASE64.match(stripped):
        return "unkbase64"
    return None


@dataclass(frozen=True, slots=True)
class ShareBlock:
    """A parsed share-text envelope: header sizes plus the joined payload string."""

    magic: str
    uncompressed_size: int
    compressed_size: int
    encoded_size: int
    encoding: str
    payload: str


def decode_share_block(text: str) -> ShareBlock:
    """Parse a share-text envelope (``BuildManager.cs:320-351``).

    Raises:
        ValueError: if there are fewer than 2 lines, the header is not exactly 5
            ``;``-separated items, or the sizes are not integers.
    """
    lines = _lines(text)
    header_index = _find_header_index(lines)
    if header_index is None:
        raise ValueError("no |MxDz;…| or |MBD;…| header line found")
    if header_index == len(lines) - 1:
        raise ValueError("share block needs a header line and at least one payload line")
    header = lines[header_index].strip("|").strip()
    items = [it for it in header.split(";") if it != ""]
    if len(items) != 5:
        raise ValueError(f"share header must have exactly 5 items, got {len(items)}")
    try:
        uncompressed = int(items[1])
        compressed = int(items[2])
        encoded = int(items[3])
    except ValueError as exc:
        raise ValueError("share header sizes must be integers") from exc
    # Collect payload lines after the header, stopping at the first non-payload
    # line (the trailing banner of dashes, or trailing metadata).
    payload_parts: list[str] = []
    for line in lines[header_index + 1 :]:
        token = line.replace("|", "").strip()
        if token == "":
            continue
        if not _PAYLOAD_LINE.match(token):
            break
        payload_parts.append(token)
    payload = "".join(payload_parts)
    return ShareBlock(
        magic=items[0],
        uncompressed_size=uncompressed,
        compressed_size=compressed,
        encoded_size=encoded,
        encoding=items[4],
        payload=payload,
    )


def hex_decode(data: str) -> bytes:
    """Decode an uppercase/lowercase hex string to bytes (``ModernZlib.cs:71-83``).

    Raises:
        ValueError: if ``data`` is not valid hex.
    """
    if not _HEX.match(data):
        raise ValueError("mxd payload is not valid hex")
    return bytes.fromhex(data)


def decode_mxd(text: str) -> bytes:
    """Decode a ``.mxd`` share block to its decompressed binary buffer.

    Reproduces ``BuildManager.cs:364-382``: hex-decode, reject on a compressed-size
    mismatch, zlib inflate, then resize to the declared uncompressed size.

    Raises:
        ValueError: on wrong classification, invalid hex, a compressed-size
            mismatch, or a zlib inflate failure.
    """
    if classify(text) != "mxd":
        raise ValueError("not an mxd share block")
    block = decode_share_block(text)
    raw = hex_decode(block.payload)
    if len(raw) != block.compressed_size:
        raise ValueError(f"mxd compressed size mismatch: header {block.compressed_size}, actual {len(raw)}")
    try:
        buffer = zlib.decompress(raw)
    except zlib.error as exc:
        raise ValueError("mxd zlib inflate failed") from exc
    return buffer[: block.uncompressed_size]


# --- Semantic binary parse (MxDReadSaveData, MidsCharacterFileFormat.cs:338-750) ---

_MAGIC = bytes([0x4D, 0x78, 0x44, 0x0C])  # 'M','x','D',12
_PRIOR_VERSION = f32(3.10)
_THIS_VERSION = f32(3.20)

# eEnhRelative / eEnhGrade ordinals -> the same names the .mbd stores (Enums.cs).
_EENH_RELATIVE = (
    "None",
    "MinusThree",
    "MinusTwo",
    "MinusOne",
    "Even",
    "PlusOne",
    "PlusTwo",
    "PlusThree",
    "PlusFour",
    "PlusFive",
)
_EENH_GRADE = ("None", "TrainingO", "DualO", "SingleO")

# Powerset FullName migrations applied on .mxd read (MidsCharacterFileFormat.cs:459-467).
_POWERSET_MIGRATIONS = {
    "Pool.Leadership_beta": "Pool.Leadership",
    "Blaster_Support.Atomic_Manipulation": "Blaster_Support.Radiation_Manipulation",
    "Pool.Fitness": "Pool.Invisibility",
}

# Homecoming power-FullName migration (MidsCharacterFileFormat.cs:655-674). The
# Inherent variant only migrates for the first 24 grid slots. Legacy StaticIndex
# remaps (ReplTable, Fitness nID) and the Levels_MainPowers fallback are deferred
# — they need harness dumps the reader does not yet have.
_POWER_MIGRATIONS = {"Pool.Flight.Afterburner": "Pool.Flight.Evasive_Maneuvers"}


def _enum_name(table: tuple[str, ...], ordinal: int, kind: str) -> str:
    """Map an enum ordinal to its name, rejecting an out-of-range (corrupt) byte.

    ReadSlotData casts the raw ``SByte`` to the enum; a negative or too-large value
    would silently wrap (``-1 -> "PlusFive"``) or raise ``IndexError`` if indexed
    directly, so a corrupt build is rejected with a clear error instead.
    """
    if 0 <= ordinal < len(table):
        return table[ordinal]
    raise ValueError(f"corrupt .mxd: {kind} ordinal {ordinal} out of range")


def _migrate_power_name(name: str, index: int) -> str:
    """Apply the Homecoming Afterburner -> Evasive_Maneuvers power-name migration."""
    if name in _POWER_MIGRATIONS:
        return _POWER_MIGRATIONS[name]
    if name == "Inherent.Inherent.Afterburner" and index < 24:
        return "Pool.Flight.Evasive_Maneuvers"
    return name


@dataclass(frozen=True, slots=True)
class MxdBuild:
    """A build parsed from a ``.mxd`` binary buffer.

    Parallels :class:`coh_engine.buildfile.mbd.Build` without the ``BuiltWith``
    metadata (the ``.mxd`` format carries none). Power/slot ``level`` values are
    the raw stored 0-based levels (no ``.mbd``-style ``+1`` offset).
    """

    format: str
    class_name: str
    origin_uid: str
    alignment: int
    name: str
    power_sets: tuple[str, ...]
    last_power: int
    power_entries: tuple[PowerEntry, ...] = field(default_factory=tuple)


def _format_for_version(version: float) -> str:
    """Map a file version to a field-set format (``MidsCharacterFileFormat.cs:406-422``)."""
    if version > _THIS_VERSION:
        raise ValueError(f"mxd saved by a newer version ({version}); cannot read")
    if version < _PRIOR_VERSION:
        return "legacy"
    if version < _THIS_VERSION:
        return "prior"
    return "current"


def _scan_magic(buffer: bytes) -> int:
    """Return the offset just past the 4-byte magic number, scanning byte-by-byte."""
    pos = 0
    while pos + 4 <= len(buffer):
        if buffer[pos : pos + 4] == _MAGIC:
            return pos + 4
        pos += 1
    raise ValueError("mxd magic number MxD\x0c not found")


def _read_slot_data(
    cur: Cursor,
    enh_index: dict[int, EnhIndexEntry],
    f_version: float,
) -> Enhancement | None:
    """Read one slot's enhancement (``ReadSlotData``, MidsCharacterFileFormat.cs:1381-1425).

    Non-qualified builds reference the enhancement by ``StaticIndex``; the byte
    layout that follows depends on the enhancement's ``TypeID``.
    """
    static_index = cur.read_int32()
    entry = enh_index.get(static_index)
    if entry is None:  # -1 sentinel or unresolved -> empty slot
        return None
    if entry.type_id in ("Normal", "SpecialO"):
        relative_level = _enum_name(_EENH_RELATIVE, cur.read_sbyte(), "relative level")
        grade = _enum_name(_EENH_GRADE, cur.read_sbyte(), "grade")
        return Enhancement(uid=entry.uid, grade=grade, io_level=1, relative_level=relative_level)
    if entry.type_id in ("InventO", "SetO"):
        io_level = cur.read_sbyte()
        io_level = min(io_level, 49)
        relative_level = "Even"
        if f_version > 1.0:
            relative_level = _enum_name(_EENH_RELATIVE, cur.read_sbyte(), "relative level")
        return Enhancement(uid=entry.uid, grade="None", io_level=io_level, relative_level=relative_level)
    # eType.None: reference present but no trailing fields.
    return Enhancement(uid=entry.uid, grade="None", io_level=1, relative_level="Even")


def _resolve_power(cur: Cursor, power_index: dict[int, str]) -> tuple[int, str]:
    """Read a non-qualified power reference (``Int32`` StaticIndex) -> (index, FullName).

    Qualified-names builds (a String UID reference) are rejected up front in
    :func:`read_mxd_build`, so only the StaticIndex form is handled here.
    """
    static_index = cur.read_int32()
    return static_index, power_index.get(static_index, "")


def read_mxd_build(
    buffer: bytes,
    power_index: dict[int, str],
    enh_index: dict[int, EnhIndexEntry],
    archetypes: ArchetypeDb | None = None,
) -> MxdBuild:
    """Parse a decompressed ``.mxd`` binary buffer into an :class:`MxdBuild`.

    Reproduces the byte-consumption order of ``MxDReadSaveData``
    (``MidsCharacterFileFormat.cs:338-750``); the character-grid re-sorting and
    powerset bookkeeping that method also performs are UI/model concerns and are
    not reproduced — only the build data is extracted.

    When ``archetypes`` is given, an unresolvable class UID is rejected as Mids
    does. Qualified-names builds (never written by Mids) are rejected rather than
    silently misparsed.

    Raises:
        ValueError: if the magic number is absent, the version is too new, the
            build uses qualified names, or (with ``archetypes``) the class UID is
            invalid.
    """
    cur = Cursor(buffer, _scan_magic(buffer))
    f_version = cur.read_single()
    fmt = _format_for_version(f_version)

    qualified = cur.read_boolean()
    if qualified:
        raise ValueError("qualified-names .mxd builds are not supported")
    has_sub_power = cur.read_boolean()
    class_name = cur.read_string()
    if archetypes is not None and archetypes.nid_from_uid_class(class_name) < 0:
        raise ValueError(f"invalid class UID: {class_name!r}")
    origin_uid = cur.read_string()
    alignment = cur.read_int32() if f_version > 1 else 0
    name = cur.read_string()

    powerset_count = cur.read_array_count()
    power_sets = tuple(_POWERSET_MIGRATIONS.get(n := cur.read_string(), n) for _ in range(powerset_count))

    last_power = cur.read_int32() - 1

    power_count = cur.read_array_count()
    entries: list[PowerEntry] = []
    for index in range(power_count):
        static_index, power_name = _resolve_power(cur, power_index)
        power_name = _migrate_power_name(power_name, index)
        level = 0
        stat_include = proc_include = False
        variable_value = inherent_slots_used = 0
        sub_powers: list[SubPower] = []
        if static_index > -1 or power_name:
            level = cur.read_sbyte()
            stat_include = cur.read_boolean()
            if fmt in ("current", "prior"):
                proc_include = cur.read_boolean()
            variable_value = cur.read_int32()
            if fmt == "current":
                inherent_slots_used = cur.read_int32()
            if has_sub_power:
                sub_count = cur.read_sbyte() + 1
                for _ in range(sub_count):
                    _, sub_name = _resolve_power(cur, power_index)
                    sub_powers.append(SubPower(power_name=sub_name, stat_include=cur.read_boolean()))

        slot_count = cur.read_sbyte() + 1
        slots: list[Slot] = []
        for _ in range(slot_count):
            slot_level = cur.read_sbyte()
            is_inherent = cur.read_boolean() if fmt == "current" else False
            enhancement = _read_slot_data(cur, enh_index, f_version)
            flipped = _read_slot_data(cur, enh_index, f_version) if cur.read_boolean() else None
            slots.append(
                Slot(level=slot_level, is_inherent=is_inherent, enhancement=enhancement, flipped_enhancement=flipped)
            )

        entries.append(
            PowerEntry(
                power_name=power_name,
                level=level,
                stat_include=stat_include,
                proc_include=proc_include,
                variable_value=variable_value,
                inherent_slots_used=inherent_slots_used,
                sub_power_entries=tuple(sub_powers),
                slot_entries=tuple(slots),
            )
        )

    return MxdBuild(
        format=fmt,
        class_name=class_name,
        origin_uid=origin_uid,
        alignment=alignment,
        name=name,
        power_sets=power_sets,
        last_power=last_power,
        power_entries=tuple(entries),
    )


def read_mxd(
    text: str,
    power_index: dict[int, str],
    enh_index: dict[int, EnhIndexEntry],
    archetypes: ArchetypeDb | None = None,
) -> MxdBuild:
    """Decode a ``.mxd`` share block and parse it into an :class:`MxdBuild`."""
    return read_mxd_build(decode_mxd(text), power_index, enh_index, archetypes)
