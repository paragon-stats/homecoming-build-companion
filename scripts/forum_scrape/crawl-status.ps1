<#
.SYNOPSIS
    Progress of the forum crawls, read from files on disk.

.DESCRIPTION
    Reads each run's topic ledger and artifact index, plus the process list to tell a
    live run from a finished one. It never contacts the forum, so it is safe to run as
    often as you like and costs the crawl nothing.

    The completed fraction comes from sections.jsonl, which the crawl writes before it
    works through a section (see record_section in scrape.py). A run with no such file
    has no denominator, and its Pct column reads "-" rather than a guess.

    Threads are counted distinct. The ledger is append-only across runs, so a thread
    re-crawled by a later run has one line per run and counting lines overstates
    progress.

.PARAMETER Root
    Directory holding one subdirectory per run. Every subdirectory with a topics.jsonl
    is reported, so new runs appear without editing this script.

.PARAMETER Watch
    Redraw every -Every seconds and exit on its own once no crawler is left running.

.EXAMPLE
    .\scripts\forum_scrape\crawl-status.ps1

.EXAMPLE
    .\scripts\forum_scrape\crawl-status.ps1 -Watch
#>
[CmdletBinding()]
param(
    [string]$Root = 'tmp/crawl',
    [switch]$Watch,
    [int]$Every = 30
)

function Get-CrawlProcess {
    # Narrowed to this tool: matching every python.exe would report unrelated processes.
    # One run shows up as more than one process, so the result is unique by run name.
    Get-CimInstance Win32_Process -Filter "Name='python.exe'" |
        Where-Object { $_.CommandLine -like '*forum_scrape*' } |
        ForEach-Object {
            [PSCustomObject]@{
                Name     = Split-Path (($_.CommandLine -split '--out ')[1]).Trim() -Leaf
                Started  = $_.CreationDate
                Sections = ([regex]::Matches($_.CommandLine, '--forum ')).Count
            }
        } | Sort-Object Name -Unique
}

function Get-LedgerProgress {
    param([string]$Dir)

    # Distinct, not line count: the ledger is append-only across runs, so a thread a
    # later run re-checked has one line per run.
    $threads = [Collections.Generic.HashSet[string]]::new()
    $ledger = Join-Path $Dir 'topics.jsonl'
    if (Test-Path $ledger) {
        # ReadLines streams; Get-Content would materialise tens of thousands of objects.
        foreach ($line in [IO.File]::ReadLines((Resolve-Path $ledger))) {
            if ($line -match '"topic_url":\s*"([^"]+)"') { [void]$threads.Add($Matches[1]) }
        }
    }

    $total = 0
    $sections = 0
    $sizes = Join-Path $Dir 'sections.jsonl'
    if (Test-Path $sizes) {
        foreach ($line in [IO.File]::ReadLines((Resolve-Path $sizes))) {
            if ($line -match '"topics":\s*(\d+)') {
                $total += [int]$Matches[1]
                $sections++
            }
        }
    }
    [PSCustomObject]@{ Done = $threads.Count; Total = $total; Sections = $sections }
}

function Get-LineCount {
    param([string]$Path)
    if (Test-Path $Path) { [Linq.Enumerable]::Count([IO.File]::ReadLines((Resolve-Path $Path))) } else { 0 }
}

function Show-CrawlStatus {
    $live = @(Get-CrawlProcess)
    $rows = Get-ChildItem $Root -Directory -ErrorAction SilentlyContinue |
        Where-Object { Test-Path (Join-Path $_.FullName 'topics.jsonl') } |
        ForEach-Object {
            $name = $_.Name
            $p = Get-LedgerProgress $_.FullName
            $proc = $live | Where-Object Name -EQ $name | Select-Object -First 1
            $hours = if ($proc) { ((Get-Date) - $proc.Started).TotalHours } else { 0 }
            $rate = if ($hours -gt 0) { [int][math]::Round($p.Done / $hours) } else { 0 }
            $left = $p.Total - $p.Done

            [PSCustomObject]@{
                Crawl     = $name
                Alive     = [bool]$proc
                Threads   = $p.Done
                Of        = if ($p.Total) { $p.Total } else { '-' }
                Pct       = if ($p.Total) { '{0:N1}%' -f (100 * $p.Done / $p.Total) } else { '-' }
                Sections  = if ($proc) { '{0}/{1}' -f $p.Sections, $proc.Sections } else { $p.Sections }
                Artifacts = Get-LineCount (Join-Path $_.FullName 'index.jsonl')
                PerHr     = if ($rate) { $rate } else { '-' }
                EtaHrs    = if ($rate -and $left -gt 0) { '{0:N1}' -f ($left / $rate) } else { '-' }
            }
        }

    "{0}   {1} crawl(s) running" -f (Get-Date -Format 'HH:mm:ss'), $live.Count
    $rows | Format-Table -AutoSize
    if ($rows | Where-Object Pct -EQ '-') {
        'Pct "-" means the ledger has no section rows: the run predates them, or its listing walk has not finished.'
    }
}

do {
    # Swallowed rather than guarded: there is no console to clear when this runs as a
    # job or through a pipe, and that is not a reason to stop reporting.
    if ($Watch) { try { Clear-Host } catch { } }
    Show-CrawlStatus
    $stillRunning = @(Get-CrawlProcess).Count -gt 0
    if ($Watch -and $stillRunning) { Start-Sleep -Seconds $Every }
} while ($Watch -and $stillRunning)
