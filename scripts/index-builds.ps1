<#
.SYNOPSIS
  Walk extracted Mids build archives, parse filename / path metadata, emit a
  searchable index JSON inside <source root>/community/.

.DESCRIPTION
  Inputs: a staging dir holding extracted .mxd files (any nesting), e.g.
  homecoming-build-companion/scratch/build-archives/.

  Output: <source root>/community/builds_archive_index.json — one record per
  build, with:
    rel_path        relative to the staging root
    file_size       bytes
    issue           "i25", "i19", etc. (parsed from "[iNN]" tag in filename)
    at              archetype display name (Blaster, Tanker, ...)
    at_short        archetype shortname as used in filenames (Blast, Tank, ...)
    primary_short   first powerset shortname (AR, Bs, Arch, ...)
    secondary_short second powerset shortname (Gadgets, DarkA, ...)
    ancillary_short optional 3rd powerset (Mace, Body, ...)
    pool_codes      single-letter codes seen between sets (bhjl, bcfn, ...)
    raw_filename    full filename for re-parsing later

  Filename grammar (best-effort):
    "<P1> <P2> [<P3>] [<pool_codes>] <AT_short> - <theme_name> [<hash>] - [iNN].mxd"
  Where the leading 2-3 word tokens are the powerset shortnames, the optional
  4-letter lowercase token is pool flags, AT_short follows, and the trailing
  "[iNN]" is the issue version. Anything unparseable goes into a `parse_warnings`
  array on the record.
#>
[CmdletBinding()]
param(
    [string]$StageDir = (Join-Path $PSScriptRoot '..\scratch\build-archives'),
    [string]$SourceRoot    # private source-material checkout; else $env:COH_SOURCE_ROOT, else ..\homecoming-source-material
)

$ErrorActionPreference = 'Stop'
$StageDir    = [System.IO.Path]::GetFullPath($StageDir)
. (Join-Path $PSScriptRoot 'SourceRoot.ps1')
$SourceRoot  = Resolve-SourceRoot $SourceRoot
$OutDir      = Join-Path $SourceRoot 'community'
$Out         = Join-Path $OutDir 'builds_archive_index.json'

if (-not (Test-Path $StageDir)) { throw "Missing stage dir: $StageDir" }
if (-not (Test-Path $OutDir))   { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }

# AT-shortname → display-name mapping (filenames use abbreviations)
$AtShortMap = @{
    'Blast'  = 'Blaster';     'Cor'   = 'Corruptor';   'Def'   = 'Defender'
    'Cont'   = 'Controller';  'Dom'   = 'Dominator';   'MM'    = 'Mastermind'
    'Tank'   = 'Tanker';      'Brute' = 'Brute';       'Scrap' = 'Scrapper'
    'Stalk'  = 'Stalker';     'Sent'  = 'Sentinel'
    'Pb'     = 'Peacebringer';'Ws'    = 'Warshade'
    'Soldier'= 'Arachnos Soldier'; 'Widow' = 'Arachnos Widow'
    'Crab'   = 'Arachnos Soldier'; 'Bane'  = 'Arachnos Soldier'
    'Fort'   = 'Arachnos Widow';   'Night' = 'Arachnos Widow'
}

# Some folders explicitly group by AT — let folder context win when filename
# AT-short is ambiguous (e.g. when "Veat Heat" folder contains Pb / Ws / Crab).
$FolderAtHint = @{
    'Blaster'             = 'Blaster'
    'Controller Dominator' = $null    # could be either
    'Defender Corruptor'   = $null
    'Mastermind'           = 'Mastermind'
    'Sentinel'             = 'Sentinel'
    'Melee'                = $null    # dispatch by powerset
    'Veat Heat'            = $null    # dispatch by AT-short token
    'Peacebringer'         = 'Peacebringer'
    'Warshade'             = 'Warshade'
}

function Get-ArchiveBuildRecord {
    param([System.IO.FileInfo]$File, [string]$Stage)

    $rel = $File.FullName.Substring($Stage.Length + 1).Replace('\','/')
    $name = $File.BaseName
    $warnings = @()

    # Issue tag like "[i25]"
    $issue = ''
    if ($name -match '\[i(\d+)\]') { $issue = "i$($Matches[1])" }

    # Strip the "[i25]" tail and trailing "- " separators
    $stripped = $name -replace '\s*-\s*\[i\d+\]\s*$',''

    # The "theme + hash" tail is everything after " - "; left side is the
    # set/pool/AT prefix.
    $head = $stripped
    if ($stripped -match '^(?<lhs>.+?)\s+-\s+(?<rhs>.+)$') {
        $head = $Matches['lhs'].Trim()
    }

    # Tokenize the head by spaces. Last token = AT shortname. Optional
    # second-to-last token of all-lowercase 3-5 letters = pool flags.
    $tokens = $head -split '\s+'
    if ($tokens.Count -lt 2) {
        $warnings += "too few tokens: '$head'"
    }

    $atShort = ''
    $poolCodes = ''
    $sets = @()

    if ($tokens.Count -ge 1) {
        $last = $tokens[-1]
        if ($AtShortMap.ContainsKey($last)) {
            $atShort = $last
        } else {
            $warnings += "unrecognised AT-short '$last'"
        }
    }
    if ($tokens.Count -ge 2 -and $atShort) {
        $maybePool = $tokens[-2]
        if ($maybePool -cmatch '^[a-z]{3,6}$') {
            $poolCodes = $maybePool
            $sets = $tokens[0..($tokens.Count - 3)]
        } else {
            $sets = $tokens[0..($tokens.Count - 2)]
        }
    }

    $primary   = if ($sets.Count -ge 1) { $sets[0] } else { '' }
    $secondary = if ($sets.Count -ge 2) { $sets[1] } else { '' }
    $ancillary = if ($sets.Count -ge 3) { $sets[2] } else { '' }

    # Folder hint
    $folder = ($rel -split '/' | Select-Object -SkipLast 1) -join '/'
    $topFolder = ($rel -split '/')[0]
    $atDisplay = if ($atShort) { $AtShortMap[$atShort] } else { '' }
    if (-not $atDisplay) {
        # Try folder name
        foreach ($k in $FolderAtHint.Keys) {
            if ($folder -match "(?:^|/)$([Regex]::Escape($k))(?:/|$)" -and $FolderAtHint[$k]) {
                $atDisplay = $FolderAtHint[$k]; break
            }
        }
    }

    return [pscustomobject]@{
        rel_path        = $rel
        file_size       = [int]$File.Length
        issue           = $issue
        at              = $atDisplay
        at_short        = $atShort
        primary_short   = $primary
        secondary_short = $secondary
        ancillary_short = $ancillary
        pool_codes      = $poolCodes
        raw_filename    = $File.Name
        folder          = $folder
        parse_warnings  = $warnings
    }
}

Write-Host "Scanning $StageDir ..." -ForegroundColor Cyan
$files = Get-ChildItem -Path $StageDir -Recurse -Filter '*.mxd' -File
Write-Host "  $($files.Count) .mxd files found"

$records = $files | ForEach-Object { Get-ArchiveBuildRecord -File $_ -Stage $StageDir }

# Summaries
$byAt   = $records | Group-Object at | Sort-Object Count -Descending
$byIssue = $records | Group-Object issue | Sort-Object Count -Descending
$warnedCount = ($records | Where-Object { $_.parse_warnings.Count -gt 0 }).Count

$index = [ordered]@{
    schema_version = 1
    generated_at   = (Get-Date -Format 'o')
    stage_root     = $StageDir
    total_builds   = $records.Count
    by_archetype   = @($byAt   | ForEach-Object { @{ at    = $_.Name; count = $_.Count } })
    by_issue       = @($byIssue | ForEach-Object { @{ issue = $_.Name; count = $_.Count } })
    parse_warnings_total = $warnedCount
    builds         = $records
}

$index | ConvertTo-Json -Depth 10 | Set-Content -Path $Out -Encoding UTF8
Write-Host "Wrote $Out ($([math]::Round((Get-Item $Out).Length / 1MB, 2)) MB)" -ForegroundColor Green
Write-Host "  $($records.Count) builds indexed"
Write-Host "  $warnedCount with parse warnings"

$byAt | Format-Table @{N='Archetype';E={ if ($_.Name) { $_.Name } else { '<unknown>' } }}, @{N='Builds';E={$_.Count}} -AutoSize
