# scan.ps1 - Produces a single JSON document describing disk usage,
# cleanable categories and basic system telemetry for the web UI.
# This script must ONLY write JSON to stdout (no logs, no stray output).

$ErrorActionPreference = "SilentlyContinue"
. (Join-Path $PSScriptRoot 'targets.ps1')

# --- System drive (all cleanup targets live on C:) ---
$disk = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='C:'"
if ($disk -and $disk.Size -gt 0) {
    $totalGB = [math]::Round($disk.Size / 1GB, 2)
    $freeGB = [math]::Round($disk.FreeSpace / 1GB, 2)
    $usedGB = [math]::Round(($disk.Size - $disk.FreeSpace) / 1GB, 2)
    $pctFree = [math]::Round(($disk.FreeSpace / $disk.Size) * 100, 1)
} else {
    $totalGB = 0; $freeGB = 0; $usedGB = 0; $pctFree = 0
}

# --- Category sizes (shared definitions in targets.ps1) ---
$categoryList = @()
foreach ($t in (Get-OmniCleanTargets)) {
    $categoryList += [PSCustomObject]@{
        id    = $t.id
        name  = $t.name
        sizeMB = Get-OmniCleanTargetSizeMB $t
        desc  = $t.desc
    }
}

# --- Scheduled task state ---
schtasks /query /tn "StorageOptimizerAutoClean" 2>&1 | Out-Null
$hasTask = ($LASTEXITCODE -eq 0)

# --- Lightweight system telemetry for the header pill ---
$cpuObj = Get-CimInstance Win32_Processor | Select-Object -First 1
$cpu = if ($cpuObj -and $cpuObj.Name) { $cpuObj.Name.Trim() } else { '' }
$ramObj = Get-CimInstance Win32_ComputerSystem
$ramGB = if ($ramObj -and $ramObj.TotalPhysicalMemory) {
    [math]::Round($ramObj.TotalPhysicalMemory / 1GB, 0)
} else { 0 }
$gpuObj = Get-CimInstance Win32_VideoController | Select-Object -First 1
$gpu = if ($gpuObj -and $gpuObj.Name) { $gpuObj.Name.Trim() } else { '' }
$arch = if ([Environment]::Is64BitOperatingSystem) { 'x64' } else { 'x86' }

[PSCustomObject]@{
    disk = [PSCustomObject]@{
        totalGB     = $totalGB
        freeGB      = $freeGB
        usedGB      = $usedGB
        percentFree = $pctFree
    }
    categories = $categoryList
    scheduledTask = [PSCustomObject]@{
        enabled = $hasTask
    }
    system = [PSCustomObject]@{
        cpu   = $cpu
        ramGB = $ramGB
        gpu   = $gpu
        arch  = $arch
    }
} | ConvertTo-Json -Depth 5
