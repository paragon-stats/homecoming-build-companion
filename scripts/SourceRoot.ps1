# Dot-sourced by the capture scripts. Resolves the private source-material checkout the way
# scripts/forum_scrape/source_root.py does: the parameter, then $env:COH_SOURCE_ROOT, then
# ..\homecoming-source-material beside the main checkout (a harness worktree resolves to the main one).

function Get-MainCheckout {
    $repo = Split-Path -Parent $PSScriptRoot
    $common = git -C $repo rev-parse --path-format=absolute --git-common-dir 2>$null
    if ($LASTEXITCODE -eq 0 -and $common) { return Split-Path -Parent $common }
    return $repo
}

function Resolve-SourceRoot {
    param([string]$Flag)
    if ($Flag) { $root = $Flag }
    elseif ($env:COH_SOURCE_ROOT) { $root = $env:COH_SOURCE_ROOT }
    else { $root = Join-Path (Split-Path -Parent (Get-MainCheckout)) 'homecoming-source-material' }
    $root = [System.IO.Path]::GetFullPath($root)
    if (-not (Test-Path -LiteralPath $root -PathType Container)) {
        $found = if (Test-Path -LiteralPath $root) { 'exists but is not a directory' } else { 'was not found' }
        throw "E04: expected a directory at $root, which $found; set -SourceRoot or COH_SOURCE_ROOT"
    }
    return $root
}
