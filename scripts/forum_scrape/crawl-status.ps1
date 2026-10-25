<#
.SYNOPSIS
    Progress of the forum crawls, read from files on disk.

.DESCRIPTION
    Reads each run's topic ledger and artifact index, plus the process list to tell a
    live run from a finished one. It never contacts the forum, and every file is opened
    sharing writes, so a running crawl keeps appending while this reads.

    The completed fraction comes from section_sizes.jsonl, which the crawl writes before it
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

function Read-Ledger {
    <#
        Stream a ledger the crawler may be appending to, right now, from another process.

        The share mode is the whole point. [IO.File]::ReadLines and Get-Content both open
        with FileShare.Read, which denies a writer -- the crawler's next append fails with
        PermissionError and the run dies. That is not theoretical: it killed a 19-section
        crawl at 90.7%, ten hours in, because this script was run to look at its progress.
        A reporting tool must not be able to do that, so the handle explicitly permits the
        writes it is reading alongside.
    #>
    param([string]$Path)

    $stream = [IO.FileStream]::new(
        $Path, [IO.FileMode]::Open, [IO.FileAccess]::Read,
        [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
    $reader = [IO.StreamReader]::new($stream)
    try {
        while ($null -ne ($line = $reader.ReadLine())) { $line }
    }
    finally {
        $reader.Dispose()
        $stream.Dispose()
    }
}

function Get-LedgerProgress {
    param([string]$Dir)

    # Distinct, not line count: the ledger is append-only across runs, so a thread a
    # later run re-checked has one line per run.
    $threads = [Collections.Generic.HashSet[string]]::new()
    $ledger = Join-Path $Dir 'topics.jsonl'
    if (Test-Path $ledger) {
        foreach ($line in Read-Ledger (Resolve-Path $ledger)) {
            if ($line -match '"topic_url":\s*"([^"]+)"') { [void]$threads.Add($Matches[1]) }
        }
    }

    # One entry per section, latest wins. This file is append-only too, so a section
    # walked twice has two rows and summing them doubles the denominator.
    $sizes = Join-Path $Dir 'section_sizes.jsonl'
    $bySection = [ordered]@{}
    if (Test-Path $sizes) {
        foreach ($line in Read-Ledger (Resolve-Path $sizes)) {
            if ($line -match '"forum_url":\s*"([^"]+)".*?"topics":\s*(\d+)') {
                $bySection[$Matches[1]] = [int]$Matches[2]
            }
        }
    }
    $total = ($bySection.Values | Measure-Object -Sum).Sum
    [PSCustomObject]@{ Done = $threads.Count; Total = [int]$total; Sections = $bySection.Count }
}

function Get-LineCount {
    param([string]$Path)
    if (Test-Path $Path) { @(Read-Ledger (Resolve-Path $Path)).Count } else { 0 }
}

function Show-CrawlStatus {
    param([string]$Root)
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
    # Not a reason to stop reporting: there is no console to clear when this runs as a
    # job or through a pipe.
    if ($Watch) { try { Clear-Host } catch { Write-Verbose "no console to clear: $_" } }
    Show-CrawlStatus -Root $Root
    $stillRunning = @(Get-CrawlProcess).Count -gt 0
    if ($Watch -and $stillRunning) { Start-Sleep -Seconds $Every }
} while ($Watch -and $stillRunning)
