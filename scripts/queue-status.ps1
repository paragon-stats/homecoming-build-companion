<#
.SYNOPSIS
  Show what's captured vs still pending in CAPTURE_QUEUE.md.

.DESCRIPTION
  Walks community/CAPTURE_QUEUE.md to enumerate target URLs, then walks every
  *.md in community/ to read each capture's frontmatter url. A queue row is
  "done" if any capture's url matches (substring or query-stripped). Prints
  a colored progress table.
#>
[CmdletBinding()]
param(
    [string]$SourceRoot    # private source-material checkout; else $env:COH_SOURCE_ROOT, else ..\homecoming-source-material
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'SourceRoot.ps1')
$SourceRoot    = Resolve-SourceRoot $SourceRoot
$CommunityRoot = Join-Path $SourceRoot 'community'
$Queue = Join-Path $CommunityRoot 'CAPTURE_QUEUE.md'
if (-not (Test-Path $Queue)) { throw "CAPTURE_QUEUE.md not found at $Queue" }

# Parse queue
$rxRow = '^\|\s*(?<n>\d+)\s*\|\s*\[(?<title>[^\]]+)\]\((?<url>https?://[^\)]+)\)\s*\|\s*`(?<target>[^`]+)`'
$queueRows = foreach ($line in (Get-Content $Queue)) {
    if ($line -match $rxRow) {
        # Strip URL fragment so #comment-... anchors don't break match
        $cleanUrl = ($Matches['url'] -split '#')[0]
        [pscustomobject]@{
            N      = [int]$Matches['n']
            Title  = $Matches['title']
            Url    = $Matches['url']
            CleanUrl = $cleanUrl
            Target = $Matches['target']
        }
    }
}

# Parse all captures' urls
$capturedUrls = @{}
$captureFiles = Get-ChildItem $CommunityRoot -Recurse -Filter '*.md' -File |
    Where-Object { $_.Name -notin @('CAPTURE.md','CAPTURE_QUEUE.md','INDEX.md') }
foreach ($f in $captureFiles) {
    $text = Get-Content $f.FullName -Raw
    if ($text -match '(?ms)\A---\s*\r?\n(.*?)\r?\n---\s*\r?\n') {
        $body = $Matches[1]
        if ($body -match '(?m)^url:\s*(?<u>.+?)\s*$') {
            $u = $Matches['u'].Trim('"').Trim("'")
            $cleanU = ($u -split '#')[0]
            $rel = $f.FullName.Substring($CommunityRoot.Length + 1).Replace('\','/')
            $capturedUrls[$cleanU] = $rel
        }
    }
}

# Match
$done = @()
$pending = @()
foreach ($q in $queueRows) {
    $match = $capturedUrls.Keys | Where-Object { $_ -eq $q.CleanUrl } | Select-Object -First 1
    if ($match) {
        $done += [pscustomobject]@{
            N = $q.N; Title = $q.Title; QueueTarget = $q.Target; ActualPath = $capturedUrls[$match]
        }
    } else {
        $pending += $q
    }
}

# Captured but not in queue
$queueUrls = $queueRows | ForEach-Object { $_.CleanUrl }
$extras = $capturedUrls.GetEnumerator() | Where-Object { $_.Key -notin $queueUrls } | ForEach-Object {
    [pscustomobject]@{ Url = $_.Key; Path = $_.Value }
}

# --- Render ---
$total = $queueRows.Count
$doneCount = $done.Count
$pct = if ($total -gt 0) { [math]::Round(100 * $doneCount / $total) } else { 0 }
Write-Host ""
Write-Host "Queue progress: $doneCount / $total  ($pct%)" -ForegroundColor Cyan
Write-Host ""

if ($done.Count -gt 0) {
    Write-Host "Done:" -ForegroundColor Green
    $done | Sort-Object N | Format-Table @{N='#';E={$_.N}},
        @{N='Title';E={$_.Title}},
        @{N='Path';E={$_.ActualPath}} -AutoSize
}

if ($pending.Count -gt 0) {
    Write-Host "Pending:" -ForegroundColor Yellow
    $pending | Sort-Object N | Format-Table @{N='#';E={$_.N}},
        @{N='Title';E={$_.Title}},
        @{N='URL';E={$_.Url}} -AutoSize -Wrap
}

if ($extras.Count -gt 0) {
    Write-Host "Captured (not in queue):" -ForegroundColor Magenta
    $extras | Format-Table -AutoSize
}
