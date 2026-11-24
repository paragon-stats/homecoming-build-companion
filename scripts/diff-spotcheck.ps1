<#
.SYNOPSIS
  Lane A vs Lane B vs City of Data spot-check.

.DESCRIPTION
  Without a programmatic Lane A we can't auto-diff every power. Instead, you
  give this script a list of high-importance power full names and for each it:
    1. Reads the value from data\canonical\powers.json (Lane B).
    2. Prompts you to enter the value displayed in the running MidsReborn app
       for that power (Lane A — manual lookup).
    3. Optionally records a value from cod.uberguy.net (third validation, also
       manual via the Chrome + Claude extension workflow).

  Writes a per-power markdown report to homecoming-build-companion\data\diff\powers\
  and rolls up to homecoming-build-companion\data\diff\SUMMARY.md.

  Curate the spot-check list over time. Suggested seeds:
    - Tanker Inherent Gauntlet                  (verifies AT inherent records)
    - Tanker.Invulnerability.Unstoppable        (signature tier-9)
    - Tanker.Super_Strength.Foot_Stomp          (signature AoE)
    - Pool.Speed.Hasten                         (universal recharge tool)
    - Pool.Fighting.Tough                       (resist-stack baseline)
    - Inherent.Inherent.Sprint                  (movement baseline)
    - Boost Set: Luck of the Gambler            (popular set bonuses)
    - Incarnate.Alpha.Cardiac_Core_Paragon      (incarnate baseline)
#>
[CmdletBinding()]
param(
    [string]$Power,
    [string[]]$Stats = @('RechargeTime','EndCost','Accuracy','Range','CastTime'),
    [string]$ProjectRoot = (Join-Path $PSScriptRoot '..\..\homecoming-build-companion'),
    [switch]$AppendOnly  # don't prompt, just record canonical-only entry
)

$ErrorActionPreference = 'Stop'
$ProjectRoot = [System.IO.Path]::GetFullPath($ProjectRoot)
$DiffDir = Join-Path $ProjectRoot 'data\diff'
$DiffPowers = Join-Path $DiffDir 'powers'
foreach ($d in @($DiffDir, $DiffPowers)) {
    if (-not (Test-Path $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null }
}

$canonical = Join-Path $ProjectRoot 'data\canonical\powers.json'
if (-not (Test-Path $canonical)) {
    Write-Warning "Canonical Lane B output not found: $canonical"
    Write-Warning "Run tools\lane-b\run.ps1 first."
    return
}

if (-not $Power) {
    Write-Host "Usage: diff-spotcheck.ps1 -Power 'Tanker.Invulnerability.Unstoppable'"
    Write-Host "       diff-spotcheck.ps1 -Power '...' -Stats RechargeTime,EndCost"
    Write-Host "       diff-spotcheck.ps1 -Power '...' -AppendOnly  # skip Lane A prompts"
    return
}

$db = Get-Content $canonical -Raw | ConvertFrom-Json
$obj = $db.$Power
if ($null -eq $obj) {
    Write-Error "Power '$Power' not found in canonical data."
}

$rows = foreach ($s in $Stats) {
    $canonVal = $obj.$s
    if ($AppendOnly) {
        $midsVal = '<not checked>'
        $codVal  = '<not checked>'
    } else {
        $midsVal = Read-Host "[$Power] $s — value shown in MidsReborn app"
        $codVal  = Read-Host "[$Power] $s — value on cod.uberguy.net (or 'skip')"
    }
    $match = ($canonVal -eq $midsVal) -or ($AppendOnly)
    [pscustomobject]@{
        Stat      = $s
        Canonical = $canonVal
        Mids      = $midsVal
        CoD       = $codVal
        Agree     = $match
    }
}

$rows | Format-Table -AutoSize

# Per-power markdown
$mdPath = Join-Path $DiffPowers ("$Power.md")
$lines = @()
$lines += "# Spot-check: $Power"
$lines += ""
$lines += "Captured: $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
$lines += ""
$lines += "| Stat | Canonical (Lane B) | MidsReborn app (Lane A) | City of Data | Agree? |"
$lines += "|---|---|---|---|:-:|"
foreach ($r in $rows) {
    $a = if ($r.Agree) { 'YES' } else { 'NO' }
    $lines += "| $($r.Stat) | $($r.Canonical) | $($r.Mids) | $($r.CoD) | $a |"
}
$lines | Set-Content $mdPath -Encoding UTF8
Write-Host "Wrote $mdPath" -ForegroundColor Green

# Roll up SUMMARY.md
$summary = Join-Path $DiffDir 'SUMMARY.md'
$entry = "- [$Power]($([io.path]::GetFileName($DiffPowers))/$Power.md) — $(($rows | Where-Object { -not $_.Agree }).Count) disagreements / $($rows.Count) stats checked"
if (Test-Path $summary) {
    $existing = Get-Content $summary
    if ($existing -notcontains $entry) {
        Add-Content -Path $summary -Value $entry
    }
} else {
    @(
        "# Lane diff summary",
        "",
        "Per-power spot-checks comparing Lane B canonical (game files) vs Lane A (MidsReborn app) vs City of Data v2.0.",
        "",
        $entry
    ) | Set-Content $summary -Encoding UTF8
}
Write-Host "Updated $summary" -ForegroundColor Green
