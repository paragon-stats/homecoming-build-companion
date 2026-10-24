<#
.SYNOPSIS
    Cross-check data/diff/current_overrides.json against the canonical zip
    data and write a drift report.

.DESCRIPTION
    Override entity names follow patch-notes display style (e.g.
    "Tanker.Battle_Axe.Cleave"). Canonical zip files use snake_case paths
    (e.g. data/canonical/powers/tanker_melee/battle_axe/cleave.json).

    Resolver:
    1. Build a search index from data/canonical/all_power_search.json,
        keyed by lowercase display fullname → file path.
    2. For each override (entity, field), find candidate canonical files
        by matching either an exact display fullname or by suffix
        (powerset.power last two segments) within an AT-category subtree.
    3. Read each candidate's value for the field; compare against the
        override's "value".
    4. Emit per-power markdown reports under data/diff/powers/ when there's
        a drift, and a top-level data/diff/SUMMARY.md.
#>
[CmdletBinding()]
param(
    [string]$ProjectRoot = (Join-Path $PSScriptRoot '..\..\homecoming-build-companion')
)

$ErrorActionPreference = 'Stop'
$ProjectRoot = [System.IO.Path]::GetFullPath($ProjectRoot)
$CanonRoot   = Join-Path $ProjectRoot 'data\canonical'
$DiffDir     = Join-Path $ProjectRoot 'data\diff'
$DiffPowers  = Join-Path $DiffDir 'powers'
$Overrides   = Join-Path $DiffDir 'current_overrides.json'

foreach ($d in @($DiffDir, $DiffPowers)) {
    if (-not (Test-Path $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null }
}
if (-not (Test-Path $Overrides)) { throw "Missing $Overrides — run parse-patches.ps1 first." }
if (-not (Test-Path $CanonRoot))  { throw "Missing $CanonRoot — Lane B not extracted yet." }

# AT-display-name → canonical subdirs to search (most-likely first).
$AtSubdirs = @{
    'Tanker'     = @('tanker_melee','tanker_defense','tanker_melee_aux')
    'Brute'      = @('brute_melee','brute_defense','brute_melee_aux')
    'Scrapper'   = @('scrapper_melee','scrapper_defense','scrapper_melee_aux')
    'Stalker'    = @('stalker_melee','stalker_defense','stalker_melee_aux')
    'Sentinel'   = @('sentinel_ranged','sentinel_defense')
    'Blaster'    = @('blaster_ranged','blaster_support','blaster_ranged_aux')
    'Corruptor'  = @('corruptor_ranged','corruptor_buff','corruptor_ranged_aux')
    'Defender'   = @('defender_buff','defender_ranged','defender_ranged_aux')
    'Controller' = @('controller_control','controller_buff')
    'Dominator'  = @('dominator_control','dominator_assault')
    'Mastermind' = @('mastermind_pets','mastermind_buff')
    'Peacebringer' = @('peacebringer','peacebringer_attacks','peacebringer_defense')
    'Warshade'   = @('warshade','warshade_attacks','warshade_defense')
    'Pool'       = @('pool')
    'Inherent'   = @('inherent')
}

function Get-CanonValue {
    param([string]$JsonPath, [string]$Field)
    if (-not (Test-Path $JsonPath)) { return $null }
    try {
        $obj = Get-Content $JsonPath -Raw | ConvertFrom-Json
    } catch {
        return $null
    }
    if ($obj.PSObject.Properties[$Field]) { return $obj.$Field }
    return $null
}

# Apply unit normalization so cross-check compares like with like.
# - Patch notes record arcs in degrees ("90"); canonical zip stores them in
#   radians (~1.5708). Same for some other fields where the wiki uses
#   user-facing units. Convert canonical → patch-units before comparison.
function ConvertTo-PatchUnits {
    param([string]$Field, $CanonValue)
    if ($null -eq $CanonValue) { return $null }
    switch ($Field) {
        'arc' {
            # rad → deg
            return [math]::Round([double]$CanonValue * (180.0 / [math]::PI), 4)
        }
        default { return [double]$CanonValue }
    }
}

# Pick the canonical file whose AT-subdir best matches the override entity's
# AT prefix (or section keyword). This handles "Blaster_Secondary_Revamp.X"
# style entities where the AT is encoded in the prefix.
function Select-BestCandidate {
    param([string]$Entity, [string[]]$Candidates)
    if ($Candidates.Count -le 1) { return $Candidates }
    $entityLower = $Entity.ToLower()
    $atHints = @{
        'tanker_'      = @('tanker_melee','tanker_defense')
        'brute_'       = @('brute_melee','brute_defense')
        'scrapper_'    = @('scrapper_melee','scrapper_defense')
        'stalker_'     = @('stalker_melee','stalker_defense')
        'sentinel_'    = @('sentinel_ranged','sentinel_defense')
        'blaster_secondary' = @('blaster_support')
        'blaster_'     = @('blaster_ranged','blaster_support')
        'corruptor_'   = @('corruptor_ranged','corruptor_buff')
        'defender_'    = @('defender_buff','defender_ranged')
        'controller_'  = @('controller_control','controller_buff')
        'dominator_'   = @('dominator_control','dominator_assault')
        'mastermind_'  = @('mastermind_pets','mastermind_buff')
    }
    foreach ($kv in $atHints.GetEnumerator()) {
        if ($entityLower -like "*$($kv.Key)*") {
            $matched = $Candidates | Where-Object {
                $c = $_
                $kv.Value | Where-Object { $c -match "[\\/]$_[\\/]" }
            }
            if ($matched) { return @($matched) }
        }
    }
    return $Candidates
}

# Build a single index over data/canonical/powers once. Map of:
#   "powerset/power"  → @(full_paths)
$Script:PowerIndex = $null
function Initialize-PowerIndex {
    if ($Script:PowerIndex) { return }
    $Script:PowerIndex = @{}
    Write-Host "Indexing canonical powers/ ..." -ForegroundColor DarkGray
    foreach ($f in Get-ChildItem -Path (Join-Path $CanonRoot 'powers') -Recurse -Filter '*.json') {
        $rel = $f.FullName.Substring((Join-Path $CanonRoot 'powers').Length + 1).Replace('\','/')
        # rel = "tanker_melee/battle_axe/cleave.json" — last two components are key.
        # File names already carry ':' as '_' (attack_profile.canonical_power_path); Resolve-CanonPath maps lookups the same way.
        $parts = $rel -split '/'
        if ($parts.Count -ge 2) {
            $key = ($parts[-2] + '/' + ($parts[-1] -replace '\.json$','')).ToLower()
            if (-not $Script:PowerIndex.ContainsKey($key)) {
                $Script:PowerIndex[$key] = @()
            }
            $Script:PowerIndex[$key] += $f.FullName
        }
    }
    Write-Host "  $($Script:PowerIndex.Count) (powerset/power) keys indexed" -ForegroundColor DarkGray
}

function Resolve-CanonPath {
    param([string]$Entity)
    Initialize-PowerIndex
    $segs = $Entity -split '\.'
    if ($segs.Count -lt 2) { return @() }

    # Try AT-prefix routing first (most specific)
    $head = $segs[0]
    if ($AtSubdirs.ContainsKey($head)) {
        $tail = $segs[1..($segs.Count - 1)] | ForEach-Object { $_.ToLower().Replace(':', '_') }
        $candidates = @()
        foreach ($sub in $AtSubdirs[$head]) {
            $relPath = ($tail -join '/') + '.json'
            $full = Join-Path $CanonRoot ('powers/' + $sub + '/' + $relPath)
            if (Test-Path $full) { $candidates += $full }
        }
        if ($candidates) { return $candidates }
    }

    # Fall back to powerset/power suffix lookup via the index. This catches
    # patches whose entity prefix doesn't include an AT (e.g. parsed from a
    # "Powerset Revamp" section header where the AT is implicit).
    if ($segs.Count -ge 2) {
        $power    = $segs[-1].ToLower().Replace(':', '_')
        $powerset = $segs[-2].ToLower().Replace(':', '_')
        $key = "$powerset/$power"
        if ($Script:PowerIndex.ContainsKey($key)) {
            return @($Script:PowerIndex[$key])
        }
        # Last-ditch: just the power name (when even the powerset wasn't parsed)
        $bare = Get-ChildItem -Path (Join-Path $CanonRoot 'powers') -Recurse -Filter "$power.json" -ErrorAction SilentlyContinue
        if ($bare) { return @($bare.FullName) }
    }
    return @()
}

# --- Load overrides ---
Write-Host "Loading $Overrides ..." -ForegroundColor Cyan
$ov = Get-Content $Overrides -Raw | ConvertFrom-Json

$drifts   = @()
$matches_ = @()
$unresolved = @()

foreach ($entityProp in $ov.overrides.PSObject.Properties) {
    $entity = $entityProp.Name
    $fields = $entityProp.Value
    $candidates = Resolve-CanonPath $entity
    if (-not $candidates -or $candidates.Count -eq 0) {
        foreach ($fProp in $fields.PSObject.Properties) {
            $unresolved += [pscustomobject]@{
                entity = $entity
                field  = $fProp.Name
                override_value = $fProp.Value.value
                source_patch = $fProp.Value.source_patch
                reason = 'no canonical file matched'
            }
        }
        continue
    }
    # Disambiguate: when many canonical files share a power name (multi-AT),
    # narrow to the one that matches the entity's AT prefix. Wrap in @() so a
    # single-element result stays an array under PowerShell's auto-unrolling.
    $candidates = @(Select-BestCandidate -Entity $entity -Candidates @($candidates))

    foreach ($fProp in $fields.PSObject.Properties) {
        $field = $fProp.Name
        $patchVal = $fProp.Value.value
        # Multiple candidate canonical files for one (entity, field) means we
        # genuinely can't pick. Surface as an ambiguity escalation rather than
        # a flood of "drift" entries.
        if ($candidates.Count -gt 1) {
            $unresolved += [pscustomobject]@{
                entity = $entity; field = $field
                override_value = $patchVal
                source_patch = $fProp.Value.source_patch
                reason = "ambiguous: $($candidates.Count) canonical files match — needs manual disambiguation. Files: $((@($candidates) | ForEach-Object { $_.Substring($CanonRoot.Length + 1) }) -join '; ')"
            }
            continue
        }
        $cand = [string]$candidates[0]
        $rawCanon = Get-CanonValue -JsonPath $cand -Field $field
        $canonVal = ConvertTo-PatchUnits -Field $field -CanonValue $rawCanon
        $rec = [pscustomobject]@{
            entity         = $entity
            field          = $field
            override_value = $patchVal
            canonical_value = $canonVal
            canonical_value_raw = $rawCanon
            canonical_file  = $cand.Substring($CanonRoot.Length + 1)
            source_patch   = $fProp.Value.source_patch
            history        = $fProp.Value.history
        }
        if ($null -eq $rawCanon) {
            $unresolved += [pscustomobject]@{
                entity = $entity; field = $field
                override_value = $patchVal
                source_patch = $fProp.Value.source_patch
                reason = "canonical file $($cand.Substring($CanonRoot.Length + 1)) has no field '$field'"
            }
        } elseif ([math]::Abs([double]$canonVal - [double]$patchVal) -gt 0.001) {
            $drifts += $rec
        } else {
            $matches_ += $rec
        }
    }
}

Write-Host ("Drifts: {0}; matches: {1}; unresolved: {2}" -f $drifts.Count, $matches_.Count, $unresolved.Count) -ForegroundColor Yellow

# --- Per-power markdown for each drift ---
foreach ($d in $drifts) {
    $hist = $d.history | ForEach-Object { "$($_.patch): $($_.from) → $($_.to)" }
    $histBlock = ($hist -join "`r`n") + ""
    $slug = ($d.entity -replace '[^A-Za-z0-9_.-]','_')
    $out = Join-Path $DiffPowers "$slug.$($d.field).md"
    $md = @"
# Drift: $($d.entity).$($d.field)

| Source | Value |
|---|---:|
| Patch override (latest) | $($d.override_value) |
| Canonical zip | $($d.canonical_value) |

- Override source patch: **$($d.source_patch)**
- Canonical file: ``$($d.canonical_file)``

## History (chronological)

$histBlock

## Resolution

The patch notes are authoritative for live Homecoming. Treat the override
value ($($d.override_value)) as current; the canonical zip is stale on
this field.
"@
    Set-Content -Path $out -Value $md -Encoding UTF8
}

# --- Top-level SUMMARY.md ---
$drift_grouped = $drifts | Group-Object source_patch | Sort-Object Count -Descending
$drift_table = ($drifts | Sort-Object entity, field | ForEach-Object {
    "| $($_.entity) | $($_.field) | $($_.override_value) | $($_.canonical_value) | $($_.source_patch) |"
}) -join "`r`n"

$unresolved_table = ($unresolved | Sort-Object entity, field | ForEach-Object {
    "| $($_.entity) | $($_.field) | $($_.override_value) | $($_.source_patch) | $($_.reason) |"
}) -join "`r`n"

$summary = @"
# Lane diff summary

Cross-check of patch-history overrides (data/diff/current_overrides.json)
against the canonical zip (data/canonical/).

Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm')

## Counts

| Bucket | Count |
|---|---:|
| Override agrees with canonical | $($matches_.Count) |
| **Override drifts from canonical** | **$($drifts.Count)** |
| Unresolved (no canonical file or field) | $($unresolved.Count) |

## Drift by patch

| Patch | Drift count |
|---|---:|
$(($drift_grouped | ForEach-Object { "| $($_.Name) | $($_.Count) |" }) -join "`r`n")

## Drifts (override = current Homecoming truth; canonical = stale)

| Entity | Field | Override | Canonical | Source patch |
|---|---|---:|---:|---|
$drift_table

## Unresolved (couldn't map override to a canonical file/field — needs manual review)

| Entity | Field | Override value | Source patch | Reason |
|---|---|---:|---|---|
$unresolved_table

## Resolution rule (escalation)

- When override and canonical agree → no action.
- When they disagree → the **override wins**; the canonical zip is older than
    the patch. Per-power detail in data/diff/powers/<entity>.<field>.md.
- When unresolved → the override entity name didn't map to a canonical file.
    Likely causes: (a) patch refers to an inherent or non-power feature
    (e.g. "Tanker_AT_inherent_changes"), (b) parsed entity prefix is a date or
    section heading that wasn't filtered out, (c) typo in the patch notes
    (e.g. "Shadwo Maul" misspelled). Walk the unresolved list and fix scripts/
    parse-patches.ps1's Build-EntityPath skip list, or add manual aliases.
"@
Set-Content -Path (Join-Path $DiffDir 'SUMMARY.md') -Value $summary -Encoding UTF8
Write-Host ("Wrote {0}" -f (Join-Path $DiffDir 'SUMMARY.md')) -ForegroundColor Green
