<#
.SYNOPSIS
  Ingest one Chrome+extension-captured forum-guide markdown file into the
  source root's community/ tree. Validates frontmatter, copies to the right
  folder, and re-runs the community INDEX.md regenerator.

.DESCRIPTION
  Source: a markdown file you saved after asking Claude (in the browser
  extension) to extract a forum thread per the format in community/CAPTURE.md.

  Required frontmatter fields: title, url, source, date_captured, captured_by,
  topic_tags (array, first entry = topic folder), trust.

  Optional but recommended: authors, date_posted, contradicts_data.

  Target folder is taken from -Target if passed, else the script derives it
  from `topic_tags[0]` mapped to one of the standard folders:
    archetypes, powersets, mechanics, meta-builds, tools, patches, validation,
    _meta.

  After copy, runs scripts/regen-index.ps1 so community/INDEX.md is fresh.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Path,                # path to the local .md file you saved

    [string]$Target,              # optional, e.g. "mechanics/ppm-information-guide.md"

    [string]$SourceRoot,   # private source-material checkout; else $env:COH_SOURCE_ROOT, else ..\homecoming-source-material

    [switch]$Force                # overwrite if destination exists
)

$ErrorActionPreference = 'Stop'
$Path        = [System.IO.Path]::GetFullPath($Path)
. (Join-Path $PSScriptRoot 'SourceRoot.ps1')
$SourceRoot = Resolve-SourceRoot $SourceRoot
$CommunityRoot = Join-Path $SourceRoot 'community'

if (-not (Test-Path $Path)) { throw "Source not found: $Path" }
if (-not (Test-Path $CommunityRoot)) { throw "E04: expected a directory at $CommunityRoot, which was not found; set -SourceRoot or COH_SOURCE_ROOT" }

# Standard topic folder names. First topic_tag is matched against these
# (case-insensitive) and falls back to "_meta" if no match.
$TopicFolders = @('archetypes','powersets','mechanics','meta-builds','tools','patches','validation','_meta')

function Read-Frontmatter {
    param([string]$FilePath)
    $text = Get-Content $FilePath -Raw
    if ($text -notmatch '(?ms)\A---\s*\r?\n(.*?)\r?\n---\s*\r?\n') {
        throw "No YAML frontmatter found in $FilePath. Add a --- ... --- block per community/CAPTURE.md."
    }
    $body = $Matches[1]
    $fm = @{}
    foreach ($line in ($body -split "`n")) {
        if ($line -match '^(\w+):\s*(.*?)\s*$') {
            $key = $Matches[1]
            $val = $Matches[2]
            if ($val -match '^\[(.*)\]$') {
                $val = @($Matches[1].Split(',') | ForEach-Object { $_.Trim().Trim('"').Trim("'") })
            } else {
                $val = $val.Trim('"').Trim("'")
            }
            $fm[$key] = $val
        }
    }
    return $fm
}

function Test-IsBuildSubcapture {
    param([hashtable]$Fm)
    # A build sub-capture has the build-specific frontmatter shape:
    # archetype, primary, parent_url, suggested_filename — distinct from a
    # parent guide capture which has topic_tags and trust.
    return ($Fm.ContainsKey('archetype') -and
            $Fm.ContainsKey('primary')   -and
            $Fm.ContainsKey('parent_url'))
}

function Test-ParentFrontmatter {
    param([hashtable]$Fm)
    $required = @('title','url','source','date_captured','captured_by','topic_tags','trust')
    $missing = $required | Where-Object { -not $Fm.ContainsKey($_) -or -not $Fm[$_] }
    if ($missing) { throw "Parent capture frontmatter missing required field(s): $($missing -join ', ')" }
    if (-not ($Fm.topic_tags -is [array])) {
        throw "topic_tags must be a YAML list, e.g. [mechanics, procs]. Got: $($Fm.topic_tags)"
    }
    $valid_trust = @('first-party','community-consensus','single-author-opinion')
    if ($Fm.trust -notin $valid_trust) {
        throw "trust must be one of: $($valid_trust -join ', '). Got: $($Fm.trust)"
    }
}

function Test-BuildFrontmatter {
    param([hashtable]$Fm)
    $required = @('title','archetype','primary','secondary','build_goal','build_format','parent_url','captured_by','suggested_filename')
    $missing = $required | Where-Object { -not $Fm.ContainsKey($_) -or -not $Fm[$_] }
    if ($missing) { throw "Build sub-capture frontmatter missing required field(s): $($missing -join ', ')" }
}

function Get-DerivedTarget {
    param([hashtable]$Fm, [string]$SourceFile)
    $primary = ($Fm.topic_tags[0]).ToLower()
    $folder  = if ($primary -in $TopicFolders) { $primary } else { '_meta' }
    $slug    = (Split-Path $SourceFile -Leaf) -replace '\.md$',''
    return "$folder/$slug.md"
}

function Get-BuildTarget {
    param([hashtable]$Fm, [string]$SourceFile)
    # Use the suggested_filename from frontmatter; fall back to
    # builds/<archetype>/<slug>.md if missing.
    $suggested = $Fm.suggested_filename
    if ($suggested) {
        return $suggested -replace '^/+',''
    }
    $at = ($Fm.archetype -as [string]).ToLower() -replace '\s+','-'
    $slug = (Split-Path $SourceFile -Leaf) -replace '\.md$',''
    return "builds/$at/$slug.md"
}

# --- Process ---
$fm = Read-Frontmatter -FilePath $Path

if (Test-IsBuildSubcapture -Fm $fm) {
    Test-BuildFrontmatter -Fm $fm
    if (-not $Target) {
        $Target = Get-BuildTarget -Fm $fm -SourceFile $Path
        Write-Host "Build sub-capture detected. Target: $Target" -ForegroundColor DarkCyan
    }
} else {
    Test-ParentFrontmatter -Fm $fm
    if (-not $Target) {
        $Target = Get-DerivedTarget -Fm $fm -SourceFile $Path
        Write-Host "Parent capture. Target derived from topic_tags[0]='$($fm.topic_tags[0])': $Target" -ForegroundColor DarkGray
    }
}
$dest = Join-Path $CommunityRoot $Target
$destDir = Split-Path $dest -Parent
if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir -Force | Out-Null }

if ((Test-Path $dest) -and -not $Force) {
    throw "Destination exists: $dest. Pass -Force to overwrite."
}

Copy-Item -Path $Path -Destination $dest -Force
Write-Host "Copied → $dest" -ForegroundColor Green

# --- Re-run index ---
$regen = Join-Path $PSScriptRoot 'regen-index.ps1'
if (Test-Path $regen) {
    Write-Host "Re-generating community/INDEX.md ..." -ForegroundColor Cyan
    & $regen -SourceRoot $SourceRoot
}
