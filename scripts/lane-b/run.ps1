<#
.SYNOPSIS
    Lane B — extract canonical Homecoming game data from the City of Data
    raw-data zip distribution.

.DESCRIPTION
    Replaces the original Pigg Wrangler / Bin Crawler pipeline. The CoD project
    publishes pre-extracted JSON for the entire Homecoming dataset as a single
    zip (~97 MB compressed, ~44,000 JSON files). We unzip it into
    homecoming-build-companion\data\canonical\ and write a manifest.

    Source structure (top-level dirs in the zip):
    archetypes/        — every class record (player ATs + enemy ranks)
    tables/            — per-class tables (damage scalars, level curves)
    powers/            — every power, in subdirs by powerset/group
    boost_sets/        — IO sets with bonuses
    entities/          — pets/critters
    entity_tags/, tags/, exclusion_groups/, recharge_groups/ — categorization

.PARAMETER Zip
    Path to the raw-data zip. Default: %USERPROFILE%\Downloads\raw_data_homecoming-*.zip
    (most recent match).

.PARAMETER OutDir
    Destination directory. Default: <repo>\..\homecoming-build-companion\data\canonical

.PARAMETER Force
    Overwrite existing canonical data even if zip SHA256 matches the manifest.

.NOTES
    Idempotent: skips re-extraction when the zip's SHA256 matches the cached
    hash in OutDir\manifest.json.
#>
[CmdletBinding()]
param(
    [string]$Zip,
    [string]$OutDir = (Join-Path $PSScriptRoot '..\..\..\homecoming-build-companion\data\canonical'),
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$OutDir = [System.IO.Path]::GetFullPath($OutDir)

# Extract a zip, mapping ':' to '_' in each path segment. Same rule as
# coh_engine.attack_profile.canonical_power_path: NTFS reads 'name:stream' as an alternate
# data stream, which Expand-Archive turned into empty files.
function Expand-ZipSanitized {
    param([string]$ZipPath, [string]$Destination)
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $seen = @{}
    $archive = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        foreach ($entry in $archive.Entries) {
            if ($entry.FullName.EndsWith('/')) { continue }
            $rel = ($entry.FullName -split '/' | ForEach-Object { $_.Replace(':', '_') }) -join '/'
            if ($seen.ContainsKey($rel)) {
                throw "E41: zip entries '$($seen[$rel])' and '$($entry.FullName)' both map to '$rel'; rename one in the extractor"
            }
            $seen[$rel] = $entry.FullName
            $target = [System.IO.Path]::GetFullPath((Join-Path $Destination $rel))
            if (-not $target.StartsWith($Destination, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "E42: zip entry '$($entry.FullName)' resolves outside $Destination; refusing to extract"
            }
            [void][System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($target))
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
        }
    } finally {
        $archive.Dispose()
    }
}

# --- 1. Locate the zip ---
if (-not $Zip) {
    $candidates = Get-ChildItem "$env:USERPROFILE\Downloads\raw_data_homecoming-*.zip" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending
    if ($candidates.Count -eq 0) {
        throw "No raw_data_homecoming-*.zip found in $env:USERPROFILE\Downloads. Pass -Zip <path>."
    }
    $Zip = $candidates[0].FullName
    Write-Host "Auto-selected most recent zip: $Zip" -ForegroundColor DarkGray
}
if (-not (Test-Path $Zip)) { throw "Zip not found: $Zip" }

$zipInfo = Get-Item $Zip
Write-Host "Source zip: $Zip" -ForegroundColor Cyan
Write-Host "  Size: $([math]::Round($zipInfo.Length/1MB,1)) MB"
Write-Host "  Modified: $($zipInfo.LastWriteTime)"

# --- 2. Hash the zip; skip if unchanged ---
Write-Host "Hashing zip..." -ForegroundColor Cyan
$zipHash = (Get-FileHash $Zip -Algorithm SHA256).Hash
Write-Host "  SHA256: $zipHash"

$manifestPath = Join-Path $OutDir 'manifest.json'
$prevHash = $null
if (Test-Path $manifestPath) {
    try {
        $prev = Get-Content $manifestPath -Raw | ConvertFrom-Json
        $prevHash = $prev.source_zip.sha256
    } catch { $prevHash = $null }
}

if (-not $Force -and $prevHash -eq $zipHash) {
    Write-Host "Canonical data already extracted from this zip (manifest matches). Use -Force to re-extract." -ForegroundColor Yellow
    return
}

# --- 3. Prepare output dir ---
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }

if ($Force -and (Test-Path $OutDir)) {
    Write-Host "Force flag: clearing existing canonical/* (preserving manifest.json until re-write)..." -ForegroundColor Yellow
    Get-ChildItem $OutDir -Directory | Remove-Item -Recurse -Force
}

# --- 4. Extract ---
Write-Host "Extracting (this takes ~30-60 sec for ~44k files)..." -ForegroundColor Cyan
$swExtract = [System.Diagnostics.Stopwatch]::StartNew()
Expand-ZipSanitized -ZipPath $Zip -Destination $OutDir
$swExtract.Stop()
Write-Host "  Extracted in $([math]::Round($swExtract.Elapsed.TotalSeconds,1)) sec"

# --- 5. Inventory ---
Write-Host "Inventorying output..." -ForegroundColor Cyan
$dirs = Get-ChildItem $OutDir -Directory
$inventory = foreach ($d in $dirs) {
    $files = Get-ChildItem $d.FullName -Recurse -File
    [pscustomobject]@{
        directory = $d.Name
        file_count = $files.Count
        total_bytes = ($files | Measure-Object -Sum Length).Sum
    }
}
$inventory | Sort-Object directory | Format-Table -AutoSize

# --- 6. Write manifest ---
$totalFiles = ($inventory | Measure-Object -Sum file_count).Sum
$totalBytes = ($inventory | Measure-Object -Sum total_bytes).Sum
$manifest = [ordered]@{
    schema_version  = 1
    generated_at    = (Get-Date -Format 'o')
    source_zip      = [ordered]@{
        path     = $Zip
        size     = $zipInfo.Length
        modified = $zipInfo.LastWriteTime.ToString('o')
        sha256   = $zipHash
    }
    extraction = [ordered]@{
        out_dir    = $OutDir
        file_count = $totalFiles
        total_bytes = $totalBytes
        directories = $inventory
    }
    notes = @(
        "Canonical Homecoming game data from City of Data v3 raw-data export.",
        "Format is CoD's snake_case JSON. See docs/canonical-data-shape.md for field reference.",
        "Powers reference each other and set bonuses by full path (e.g. set_bonus.set_bonus.luck_of_the_gambler)."
    )
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -Path $manifestPath -Encoding UTF8
Write-Host "`nWrote $manifestPath" -ForegroundColor Green
Write-Host "  $totalFiles files, $([math]::Round($totalBytes/1MB,1)) MB"
Write-Host "`nDone. Canonical data: $OutDir" -ForegroundColor Green
