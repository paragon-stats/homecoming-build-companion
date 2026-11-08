<#
.SYNOPSIS
  Regenerate <source root>/community/INDEX.md from frontmatter.

.DESCRIPTION
  Walks community/ for *.md files (skipping CAPTURE.md and INDEX.md itself),
  reads each file's YAML frontmatter, and rebuilds INDEX.md grouped by topic.
  Lets us never hand-maintain the index — every capture's frontmatter IS the
  index entry.
#>
[CmdletBinding()]
param(
    [string]$SourceRoot    # private source-material checkout; else $env:COH_SOURCE_ROOT, else ..\homecoming-source-material
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'SourceRoot.ps1')
$SourceRoot    = Resolve-SourceRoot $SourceRoot
$CommunityRoot = Join-Path $SourceRoot 'community'
if (-not (Test-Path $CommunityRoot)) { throw "E04: expected a directory at $CommunityRoot, which was not found; set -SourceRoot or COH_SOURCE_ROOT" }

function Read-Frontmatter {
    param([string]$Path)
    $text = Get-Content $Path -Raw
    if (-not $text) { return $null }
    if ($text -notmatch '(?ms)\A---\s*\r?\n(.*?)\r?\n---\s*\r?\n') { return $null }
    $body = $Matches[1]
    $fm = @{}
    foreach ($line in ($body -split "`n")) {
        if ($line -match '^(\w+):\s*(.*?)\s*$') {
            $key = $Matches[1]
            $val = $Matches[2]
            if ($val -match '^\[(.*)\]$') {
                $val = @($Matches[1].Split(',') | ForEach-Object { $_.Trim().Trim('"').Trim("'") })
            } else {
                # Strip surrounding quotes from scalar values so "none" parses as none.
                $val = $val.Trim('"').Trim("'")
            }
            $fm[$key] = $val
        }
    }
    return $fm
}

$captures = Get-ChildItem $CommunityRoot -Recurse -Filter '*.md' -File |
    Where-Object { $_.Name -notin @('CAPTURE.md','CAPTURE_QUEUE.md','INDEX.md') } |
    ForEach-Object {
        $fm = Read-Frontmatter $_.FullName
        if ($null -eq $fm) {
            Write-Warning "No frontmatter: $($_.FullName)"
            return
        }
        $rel = $_.FullName.Substring($CommunityRoot.Length + 1).Replace('\','/')
        [pscustomobject]@{
            Path        = $rel
            Title       = $fm.title
            Url         = $fm.url
            Source      = $fm.source
            Authors     = $fm.authors
            DatePosted  = $fm.date_posted
            DateCaptured = $fm.date_captured
            Topics      = $fm.topic_tags
            Trust       = $fm.trust
            Conflicts   = $fm.contradicts_data
            MultiPost   = $fm.multi_post
            PostCount   = $fm.post_count
        }
    }

if ($captures.Count -eq 0) {
    Write-Host "No captures yet. Index will be a placeholder." -ForegroundColor Yellow
}

# Group by topic — first topic tag wins for grouping; multi-tagged entries listed once under primary tag.
$byTopic = @{}
foreach ($c in $captures) {
    $primary = if ($c.Topics -is [array] -and $c.Topics.Count -gt 0) { $c.Topics[0] } `
               elseif ($c.Topics) { $c.Topics } `
               else { 'uncategorized' }
    if (-not $byTopic.ContainsKey($primary)) { $byTopic[$primary] = @() }
    $byTopic[$primary] += $c
}

$lines = @()
$lines += "# Community Knowledge Index"
$lines += ""
$lines += "Auto-generated from frontmatter in community/**/*.md by ``scripts/regen-index.ps1``."
$lines += "Do not hand-edit — your changes will be overwritten."
$lines += ""
$lines += "Last regenerated: $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
$lines += ""

if ($captures.Count -eq 0) {
    $lines += "_No captures yet. See [CAPTURE.md](CAPTURE.md) for the manual capture workflow._"
} else {
    foreach ($topic in ($byTopic.Keys | Sort-Object)) {
        $lines += "## $topic"
        $lines += ""
        foreach ($c in ($byTopic[$topic] | Sort-Object DatePosted -Descending)) {
            $authors = if ($c.Authors -is [array]) { $c.Authors -join ', ' } else { $c.Authors }
            $conflictBadge = if ($c.Conflicts -and $c.Conflicts -ne 'none') { ' ⚠️' } else { '' }
            $multiBadge    = if ("$($c.MultiPost)" -ieq 'true' -and $c.PostCount) { " [multi-post: $($c.PostCount)]" } `
                              elseif ("$($c.MultiPost)" -ieq 'true') { ' [multi-post]' } else { '' }
            $lines += "- [$($c.Title)]($($c.Path)) — $authors, $($c.DatePosted)$multiBadge$conflictBadge"
        }
        $lines += ""
    }
}

$out = Join-Path $CommunityRoot 'INDEX.md'
$lines | Set-Content $out -Encoding UTF8
Write-Host "Wrote $out  ($($captures.Count) captures indexed)" -ForegroundColor Green
