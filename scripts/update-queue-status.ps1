<#
.SYNOPSIS
  Walk community/*.md, classify each file as done / empty / pending, and
  rewrite CAPTURE_QUEUE.md with a Status column. Idempotent — re-run any
  time you've added captures.

.DESCRIPTION
  A capture is "done" when:
    - file exists,
    - it has YAML frontmatter,
    - frontmatter has a non-empty `url`,
    - the body after the closing `---` has at least 200 non-whitespace chars.

  Anything less than that with a file present is "empty" (a placeholder
  someone created but never filled).

  Anything not on disk is "pending".

  The script also discovers captures on disk that have no row in the queue
  ("off-queue") and inserts them at the bottom under a "Captured off-queue"
  table.
#>
[CmdletBinding()]
param(
    [string]$SourceRoot,   # private source-material checkout; else $env:COH_SOURCE_ROOT, else ..\homecoming-source-material
    [int]$MinBodyChars   = 200
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'SourceRoot.ps1')
$SourceRoot    = Resolve-SourceRoot $SourceRoot
$CommunityRoot = Join-Path $SourceRoot 'community'
$Queue         = Join-Path $CommunityRoot 'CAPTURE_QUEUE.md'
if (-not (Test-Path $Queue)) { throw "CAPTURE_QUEUE.md not found at $Queue" }

# --- 1. Inventory community/*.md and classify each ---
function Get-CaptureStatus {
    param([System.IO.FileInfo]$File)
    if ($File.Length -eq 0) {
        return [pscustomobject]@{ status = 'empty'; reason = '0-byte file'; url = $null }
    }
    $text = Get-Content $File.FullName -Raw
    if ($text -notmatch '(?ms)\A---\s*\r?\n(.*?)\r?\n---\s*\r?\n(?<body>.*)$') {
        return [pscustomobject]@{ status = 'empty'; reason = 'no frontmatter'; url = $null }
    }
    $fmText = $Matches[1]
    $body   = $Matches['body']
    $url = $null
    if ($fmText -match '(?m)^url:\s*(?<u>.+?)\s*$') {
        $url = $Matches['u'].Trim('"').Trim("'")
    }
    if (-not $url) {
        return [pscustomobject]@{ status = 'empty'; reason = 'no url field'; url = $null }
    }
    $bodyClean = ($body -replace '\s','')
    if ($bodyClean.Length -lt $MinBodyChars) {
        return [pscustomobject]@{ status = 'empty'; reason = "body has $($bodyClean.Length) non-ws chars (<$MinBodyChars)"; url = $url }
    }
    return [pscustomobject]@{ status = 'done'; reason = ''; url = $url }
}

# Normalize a forum URL to its canonical topic form. The forum has many URL
# variants for the same thread (#comment-NNN anchors, /page/N/ navigation,
# /?do=findComment query strings). Strip them all so matching is robust.
function Get-NormalizedTopicUrl {
    param([string]$Url)
    if (-not $Url) { return $null }
    $u = $Url.Trim()
    # Drop fragment
    $u = ($u -split '#')[0]
    # Drop query string
    $u = ($u -split '\?')[0]
    # Reduce to https://forums.homecomingservers.com/topic/<NNN-slug>/  for HC URLs.
    if ($u -match '^(?<base>https?://forums\.homecomingservers\.com/topic/[^/]+)') {
        return ($Matches['base']).TrimEnd('/') + '/'
    }
    # Otherwise just trim trailing slash inconsistency
    return $u.TrimEnd('/') + '/'
}

$captures = @{}
$captureFiles = Get-ChildItem $CommunityRoot -Recurse -Filter '*.md' -File |
    Where-Object { $_.Name -notin @('CAPTURE.md','CAPTURE_QUEUE.md','INDEX.md') }

foreach ($f in $captureFiles) {
    $rel = $f.FullName.Substring($CommunityRoot.Length + 1).Replace('\','/')
    $info = Get-CaptureStatus -File $f
    $captures[$rel] = [pscustomobject]@{
        path = $rel
        size = $f.Length
        status = $info.status
        reason = $info.reason
        url = $info.url
    }
}

# Build url → list-of-paths map (a single guide may have a primary capture + an empty placeholder).
$urlMap = @{}
foreach ($c in $captures.Values) {
    if (-not $c.url) { continue }
    $clean = Get-NormalizedTopicUrl $c.url
    if (-not $urlMap.ContainsKey($clean)) { $urlMap[$clean] = @() }
    $urlMap[$clean] += $c
}

# --- 2. Read queue, parse rows, classify each ---
$queueLines = Get-Content $Queue
$rxRowOld = '^\|\s*(?<n>\d+)\s*\|\s*\[(?<title>[^\]]+)\]\((?<url>https?://[^\)]+)\)\s*\|\s*`(?<target>[^`]+)`\s*\|\s*(?<topics>[^|]+?)\s*\|\s*(?<trust>[^|]+?)\s*\|'
$rxRowWithStatus = '^\|\s*(?<status>[^\|]+?)\s*\|\s*(?<n>\d+)\s*\|\s*\[(?<title>[^\]]+)\]\((?<url>https?://[^\)]+)\)\s*\|\s*`(?<target>[^`]+)`\s*\|\s*(?<topics>[^|]+?)\s*\|\s*(?<trust>[^|]+?)\s*\|'

# Patterns for the various header / separator rows we'll detect and rewrite.
$rxOldHeader = '^\|\s*#\s*\|\s*URL\s*\|\s*Target file\s*\|\s*Topic\s*\|\s*Trust\s*\|'
$rxNewHeader = '^\|\s*Status\s*\|\s*#\s*\|\s*URL\s*\|\s*Target file\s*\|\s*Topic\s*\|\s*Trust\s*\|'
$rxOldSep    = '^\|\s*-{2,}\s*\|\s*-{2,}\s*\|\s*-{2,}\s*\|\s*-{2,}\s*\|\s*-{2,}\s*\|\s*$'
$rxNewSep    = '^\|\s*-{2,}\s*\|\s*-{2,}\s*\|\s*-{2,}\s*\|\s*-{2,}\s*\|\s*-{2,}\s*\|\s*-{2,}\s*\|\s*$'

function Get-StatusForRow {
    # A queue row may resolve via URL match (capture has frontmatter url),
    # OR via target-path match (empty placeholder file exists at the target
    # path with no frontmatter and thus no url). Walk both sources; prefer a
    # `done` over an `empty` if both exist.
    param([string]$Url, [string]$TargetPath)
    $candidates = @()
    if ($Url) {
        $clean = Get-NormalizedTopicUrl $Url
        if ($urlMap.ContainsKey($clean)) { $candidates += $urlMap[$clean] }
    }
    if ($TargetPath -and $captures.ContainsKey($TargetPath)) {
        $candidates += $captures[$TargetPath]
    }
    if ($candidates.Count -eq 0) {
        return @{ icon = '⏳'; label = 'pending'; path = '' }
    }
    $doneOnes = @($candidates | Where-Object { $_.status -eq 'done' })
    if ($doneOnes.Count -gt 0) {
        return @{ icon = '✅'; label = 'done'; path = $doneOnes[0].path }
    }
    $emptyOnes = @($candidates | Where-Object { $_.status -eq 'empty' })
    if ($emptyOnes.Count -gt 0) {
        $paths = ($emptyOnes | ForEach-Object { $_.path } | Select-Object -Unique) -join ', '
        return @{ icon = '⚠️'; label = 'empty'; path = $paths }
    }
    return @{ icon = '⏳'; label = 'pending'; path = '' }
}

# --- 3. Rewrite queue ---
$out = New-Object System.Collections.Generic.List[string]
$queuedUrls = New-Object System.Collections.Generic.List[string]

foreach ($line in $queueLines) {
    if ($line -match $rxRowWithStatus) {
        # Row already has Status column — refresh
        $url = $Matches['url']
        $tgt = $Matches['target']
        $st = Get-StatusForRow -Url $url -TargetPath $tgt
        $rowOut = "| $($st.icon) $($st.label) | $($Matches['n']) | [$($Matches['title'])]($url) | ``$tgt`` | $($Matches['topics']) | $($Matches['trust']) |"
        $out.Add($rowOut) | Out-Null
        $queuedUrls.Add((Get-NormalizedTopicUrl $url)) | Out-Null
    }
    elseif ($line -match $rxRowOld) {
        # Row without Status column — add one
        $url = $Matches['url']
        $tgt = $Matches['target']
        $st = Get-StatusForRow -Url $url -TargetPath $tgt
        $rowOut = "| $($st.icon) $($st.label) | $($Matches['n']) | [$($Matches['title'])]($url) | ``$tgt`` | $($Matches['topics']) | $($Matches['trust']) |"
        $out.Add($rowOut) | Out-Null
        $queuedUrls.Add((Get-NormalizedTopicUrl $url)) | Out-Null
    }
    elseif ($line -match $rxOldHeader) {
        $out.Add('| Status | #   | URL | Target file | Topic | Trust |') | Out-Null
    }
    elseif ($line -match $rxNewHeader) {
        $out.Add($line) | Out-Null
    }
    elseif ($line -match $rxOldSep) {
        $out.Add('| --- | --- | --- | --- | --- | --- |') | Out-Null
    }
    elseif ($line -match $rxNewSep) {
        $out.Add($line) | Out-Null
    }
    else {
        $out.Add($line) | Out-Null
    }
}

# Strip any pre-existing "Captured off-queue" section we might have appended last time.
$rejoined = ($out -join "`r`n")
$rejoined = $rejoined -replace '(?ms)\r?\n## Captured off-queue.*$',''
$rejoined = $rejoined.TrimEnd() + "`r`n"

# Find off-queue captures: anything in $captures whose url isn't in the queue.
$offQueue = @()
foreach ($c in $captures.Values | Sort-Object path) {
    if (-not $c.url) { continue }
    $clean = Get-NormalizedTopicUrl $c.url
    if ($queuedUrls -notcontains $clean) {
        # But also skip empty files (no point listing 0-byte placeholders here — they'll show up under the queue rows that target them).
        if ($c.status -eq 'done') {
            $offQueue += $c
        }
    }
}

if ($offQueue.Count -gt 0) {
    $rejoined += "`r`n## Captured off-queue`r`n`r`n"
    $rejoined += "Captures on disk whose URL isn't a queue row above. Add them to the queue if they belong, or leave as a free-form addition.`r`n`r`n"
    $rejoined += "| Status | Path | URL |`r`n"
    $rejoined += "| --- | --- | --- |`r`n"
    foreach ($o in $offQueue) {
        $rejoined += "| ✅ done | ``$($o.path)`` | $($o.url) |`r`n"
    }
}

Set-Content -Path $Queue -Value $rejoined -Encoding UTF8

# --- 4. Print summary ---
$queueRows = ($out | Where-Object { $_ -match $rxRowWithStatus }).Count
$doneCount = (($out | Where-Object { $_ -match $rxRowWithStatus -and $_ -match '✅ done' })).Count
$emptyCount = (($out | Where-Object { $_ -match $rxRowWithStatus -and $_ -match '⚠️ empty' })).Count
$pendingCount = (($out | Where-Object { $_ -match $rxRowWithStatus -and $_ -match '⏳ pending' })).Count

Write-Host ""
Write-Host "CAPTURE_QUEUE.md updated:" -ForegroundColor Green
Write-Host "  Queue rows: $queueRows"
Write-Host "  ✅ done:    $doneCount" -ForegroundColor Green
Write-Host "  ⚠️ empty:   $emptyCount" -ForegroundColor Yellow
Write-Host "  ⏳ pending: $pendingCount" -ForegroundColor DarkGray
Write-Host "  Off-queue captures: $($offQueue.Count)" -ForegroundColor Magenta

if ($emptyCount -gt 0) {
    Write-Host ""
    Write-Host "Empty files (placeholders that need re-capturing):" -ForegroundColor Yellow
    foreach ($c in ($captures.Values | Where-Object { $_.status -eq 'empty' })) {
        Write-Host "  $($c.path)  ($($c.reason))" -ForegroundColor DarkYellow
    }
}
