<#
.SYNOPSIS
  Bridge between the Claude browser extension (or claude.ai chat) and the
  source root's community/ tree. Two modes:

    1. -Paste     Take markdown from the Windows clipboard, save into
                  inbox/, then ingest.
    2. -Drain     Process every *.md file already in inbox/, ingesting
                  each in turn. Good if you saved several captures and
                  want to drop them all at once.
    3. -Prompt    Just print the extraction prompt (with the URL filled
                  in) to give to the browser-extension Claude.

  Default with no flags: print usage.

.PARAMETER Url
  Forum URL the capture came from. Used for the prompt and as a sanity check
  against the captured frontmatter (frontmatter.url should match).

.PARAMETER Target
  Optional explicit target path under community/, e.g.
  "mechanics/ppm-information-guide.md". If omitted, derived from the
  capture's topic_tags[0].

.PARAMETER Force
  Overwrite an existing destination file.
#>
[CmdletBinding(DefaultParameterSetName='Help')]
param(
    [Parameter(ParameterSetName='Paste',  Mandatory)]
    [switch]$Paste,

    [Parameter(ParameterSetName='Drain',  Mandatory)]
    [switch]$Drain,

    [Parameter(ParameterSetName='Prompt', Mandatory)]
    [switch]$Prompt,

    [string]$Url,
    [string]$SourceRoot,   # passed to the ingester
    [string]$Target,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$BuildRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$Inbox     = Join-Path $BuildRoot 'inbox'
$Ingest    = Join-Path $PSScriptRoot 'ingest-forum-capture.ps1'
if (-not (Test-Path $Inbox)) { New-Item -ItemType Directory -Path $Inbox -Force | Out-Null }

function Get-PromptText {
    param([string]$ForUrl)
    $u = if ($ForUrl) { $ForUrl } else { '<paste the URL here>' }
    $today = Get-Date -Format 'yyyy-MM-dd'
    $fence = [string]::new([char]0x60, 3)
    $opener = "$($fence)markdown"
    $closer = $fence
    return @"
You're reading a Homecoming City of Heroes forum thread. Extract it into a
markdown file matching the format below.

IMPORTANT — multi-post guides. Many threads spread the guide across several
sequential posts by the same author (post 1: intro / table of contents,
post 2: section A, post 3: section B, ...). Walk the entire thread and
treat ALL posts by the original poster (and any users the OP explicitly
names as co-authors) as one continuous guide. Concatenate them in
chronological order, preserving the OP's section structure and headings.
If the OP edits a post over time, capture the latest version.

Replies from other users are community discussion. Include them only if
they materially correct, extend, or codify the guide content — and
attribute clearly ("as @user noted in reply 17, ..."). Skip flames,
signatures, brief praise, off-topic chatter.

URL: $u

# Frontmatter rules (apply to every emitted block below)
# - ``trust:`` is exactly one of: first-party, community-consensus, single-author-opinion.
#   Forum user-rank labels (Members, Retired Community Rep, etc.) are not valid trust values.
# - ``authors:`` is a single-line YAML array, e.g. [Bopper, tidge].
# - All listed keys are required.

# Embedded builds — capture each as its own block

When the thread contains a complete build dump — a "Hero Plan" / "Villain Plan"
forum-export block, a [b]Hero Plan...[/b] BBCode block, or a DataLink URL —
extract it as a SEPARATE markdown block, after the parent capture. Each
build is its own loadable artifact and gets its own file when ingested,
so it carries enough context to stand alone.

When the thread contains zero embedded builds, emit only the parent capture
and skip the per-build blocks entirely.

# Required output

Emit one PARENT capture block, immediately followed by zero or more BUILD
sub-capture blocks. Use the templates exactly. Output only fenced markdown
blocks, no other commentary.

## Block 1 — PARENT capture

$opener
---
title: <thread or post title>
url: $u
source: forums.homecomingservers.com
authors: [<every author whose content you included>]
date_posted: <OP's first-post date, YYYY-MM-DD, or unknown>
date_captured: $today
captured_by: local-capture
topic_tags: [<3-5 tags; first one MUST be one of: archetypes, powersets, mechanics, meta-builds, tools, patches, validation, _meta>]
trust: <first-party | community-consensus | single-author-opinion>
multi_post: <true | false>
post_count: <number of OP/co-author posts you concatenated, e.g. 1, 4, 12>
contradicts_data: <none | "claim X conflicts with data/Y.json — see notes">
---

## Summary

3-5 bullet conclusions covering the WHOLE guide.

- Top-line claim 1
- Top-line claim 2
- Top-line claim 3

## Verbatim excerpt

> Substantive paragraphs from the OP's posts in chronological order.
> If the guide spans multiple posts, mark each with "### Post N — Section: <name>".

## Notable replies (only if materially additive)

- @user_name (date) — one-line summary + verbatim excerpt.

## Build attachments

List every embedded build emitted in the per-build blocks below, plus any
unattached build references (DataLink-only, .mxd attachments, etc.). When
the thread has no embedded builds, the section is empty.

- <build title> — see per-build block — file: builds/<at>/<slug>.md — <archetype>, <primary>/<secondary>, <one-line summary>
- DataLink only: <verbatim URL> — importable via MidsReborn Ctrl+I
- .mxd file attachment: <filename> — referenced in the OP; not retrieved
$closer

## Block 2+ — BUILD sub-capture (one per embedded build)

$opener
---
title: <build name from the post, or "<AT>/<Primary>/<Secondary> — <theme>">
parent_guide: <relative path to the parent capture>
parent_url: $u
post_url: <URL of the specific post containing this build, with #post-N anchor when available>
build_author: <forum handle who posted this build>
date_posted: <YYYY-MM-DD of the post containing the build>
date_captured: $today
captured_by: local-capture
archetype: <Tanker | Brute | Scrapper | Stalker | Sentinel | Blaster | Corruptor | Defender | Controller | Dominator | Mastermind | Peacebringer | Warshade | Arachnos Soldier | Arachnos Widow>
primary: <powerset name>
secondary: <powerset name>
ancillary: <ancillary / patron pool, or "none">
build_goal: <one phrase, e.g. "soft-cap defense", "max damage", "perma-Hasten farmer">
build_format: <forum-export | datalink | mxd | mixed>
verified: false
contradicts_data: <none | flag>
suggested_filename: builds/<archetype-lowercase>/<slug>.md
---

## Context

2-4 sentence summary covering: what the parent guide says about this build,
why it exists, what it's optimized for, when to use it. Pull from the parent
guide so this file reads cleanly on its own.

## Verbatim build

The complete forum-export block, BBCode and all, copied verbatim from the
post. Preserve the source formatting.

## DataLink

When the post includes a DataLink URL, paste it verbatim here. Omit the
section when no DataLink is posted.

## Notes from the author (if any)

Author commentary that accompanies the build in the same post, copied
verbatim. Omit the section when no author commentary is present.
$closer
"@
}

function Get-SlugFromBlock {
    param([string]$Block)
    $slug = 'capture-{0}' -f (Get-Date -Format 'yyyyMMdd-HHmmss-fff')
    if ($Block -match '(?m)^title:\s*(?<t>.+?)\s*$') {
        $slugSrc = $Matches['t'].Trim('"').Trim("'")
        $slug = ($slugSrc -replace "[^A-Za-z0-9 -]","" -replace '\s+','-').ToLower()
        if ($slug.Length -gt 80) { $slug = $slug.Substring(0,80).TrimEnd('-') }
    }
    return $slug
}

function Save-FromClipboard {
    param([string]$Url, [string]$Target, [switch]$Force)
    Add-Type -AssemblyName PresentationCore | Out-Null
    $clip = [System.Windows.Clipboard]::GetText()
    if (-not $clip) { throw "Clipboard is empty. Copy the markdown from the Claude extension first." }

    # The extension's response now contains one PARENT block plus zero-or-more
    # BUILD sub-capture blocks, each wrapped in ```markdown ... ``` fences.
    # Extract every fenced block and process them in order.
    $rxBlock = '(?ms)```(?:markdown)?\s*\r?\n(?<content>.*?)\r?\n```'
    $blockMatches = [regex]::Matches($clip, $rxBlock)

    if ($blockMatches.Count -eq 0) {
        # Fall back: if the response wasn't fenced at all, treat the whole
        # clipboard as a single block (legacy single-frontmatter response).
        if ($clip -match '(?ms)\A---\s*\r?\n') {
            $blockMatches = @(@{ Groups = @{ content = @{ Value = $clip } } })
        } else {
            throw "Clipboard contains neither a fenced markdown block nor leading YAML frontmatter. Re-run the extension prompt."
        }
    }

    $savedFiles = @()
    foreach ($m in $blockMatches) {
        $block = if ($m.Groups) { $m.Groups['content'].Value } else { $m.Groups.content.Value }
        if ($block -notmatch '(?ms)\A---\s*\r?\n') {
            Write-Warning "Skipping a block that doesn't start with frontmatter."
            continue
        }
        $slug = Get-SlugFromBlock -Block $block
        $tmp = Join-Path $script:Inbox "$slug.md"
        # Avoid name collisions when two blocks share a slug
        $i = 2
        while (Test-Path $tmp) {
            $tmp = Join-Path $script:Inbox ("{0}-{1}.md" -f $slug, $i)
            $i++
        }
        Set-Content -Path $tmp -Value $block -Encoding UTF8
        $savedFiles += $tmp
        Write-Host "Saved → $tmp" -ForegroundColor Green
    }

    # Optional URL sanity check on the parent (first block)
    if ($Url -and $savedFiles.Count -gt 0) {
        $first = Get-Content $savedFiles[0] -Raw
        if ($first -notmatch [Regex]::Escape(($Url -split '#')[0])) {
            Write-Warning "First-block frontmatter does not mention $Url — verify before ingesting."
        }
    }

    # Hand off to the ingester for each saved file in order. The first file
    # is the parent capture; subsequent files are per-build sub-captures.
    foreach ($f in $savedFiles) {
        $args_ = @{ Path = $f }
        # Only the first ingest honors -Target; subsequent files route by
        # their `suggested_filename` field (handled by the ingester).
        if ($Target -and $f -eq $savedFiles[0]) { $args_.Target = $Target }
        if ($Force) { $args_.Force = $true }
        if ($SourceRoot) { $args_.SourceRoot = $SourceRoot }
        & $script:Ingest @args_
    }
}

function Invoke-Drain {
    param([string]$Target, [switch]$Force)
    $files = Get-ChildItem -Path $Inbox -Filter '*.md' -File -ErrorAction SilentlyContinue
    if (-not $files) {
        Write-Host "Inbox is empty: $Inbox" -ForegroundColor Yellow
        return
    }
    foreach ($f in $files) {
        Write-Host "`n--- Ingesting $($f.Name) ---" -ForegroundColor Cyan
        $args_ = @{ Path = $f.FullName }
        if ($Target) { $args_.Target = $Target }
        if ($Force)  { $args_.Force  = $true }
        if ($SourceRoot) { $args_.SourceRoot = $SourceRoot }
        try {
            & $Ingest @args_
            # Move out of inbox after successful ingest
            $done = Join-Path $Inbox '.processed'
            if (-not (Test-Path $done)) { New-Item -ItemType Directory -Path $done -Force | Out-Null }
            Move-Item -Path $f.FullName -Destination (Join-Path $done $f.Name) -Force
            Write-Host "  → moved to $($done)\$($f.Name)" -ForegroundColor DarkGray
        } catch {
            Write-Warning "  Ingest failed: $_"
            Write-Warning "  Left $($f.FullName) in inbox for review."
        }
    }
}

switch ($PSCmdlet.ParameterSetName) {
    'Prompt' { Get-PromptText -ForUrl $Url; break }
    'Paste'  { Save-FromClipboard -Url $Url -Target $Target -Force:$Force; break }
    'Drain'  { Invoke-Drain -Target $Target -Force:$Force; break }
    default {
        Write-Host @"
capture.ps1 — bridge from Claude browser extension to community/ folder.

Usage:
  capture.ps1 -Prompt -Url <forum-url>
      Print the extraction prompt with the URL filled in. Copy this and
      paste into the Claude extension on the open page. (Or just keep the
      template handy in CAPTURE_QUEUE.md — they're equivalent.)

  capture.ps1 -Paste  [-Url <forum-url>] [-Target <community/sub/file.md>]
      Take markdown from clipboard, save to inbox/, then ingest.

  capture.ps1 -Drain  [-Target ...]
      Process every *.md in inbox/ in turn. Successful ingests move to
      inbox/.processed/.

Examples:
  capture.ps1 -Prompt -Url https://forums.homecomingservers.com/topic/5290-procs-per-minute-ppm-information-guide/
  capture.ps1 -Paste  -Url https://forums.homecomingservers.com/topic/5290-procs-per-minute-ppm-information-guide/
  capture.ps1 -Drain
"@
    }
}
