$ErrorActionPreference = "SilentlyContinue"

$disk = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='C:'"
$totalGB = [math]::Round($disk.Size / 1GB, 2)
$freeGB = [math]::Round($disk.FreeSpace / 1GB, 2)
$usedGB = [math]::Round(($disk.Size - $disk.FreeSpace) / 1GB, 2)
$pctFree = [math]::Round(($disk.FreeSpace / $disk.Size) * 100, 1)

function Get-SizeMB($targetPath) {
    if (Test-Path $targetPath) {
        $measure = Get-ChildItem -Path $targetPath -Recurse -File -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum
        if ($measure -and $measure.Sum) {
            return [math]::Round($measure.Sum / 1MB, 2)
        }
    }
    return 0
}

$userSid = ([System.Security.Principal.WindowsIdentity]::GetCurrent()).User.Value
$rb = Get-SizeMB "C:\`$Recycle.Bin\$userSid"
$temp1 = Get-SizeMB "$env:TEMP"
$temp2 = Get-SizeMB "C:\Windows\Temp"
$nv = Get-SizeMB "C:\ProgramData\NVIDIA Corporation\NVIDIA app\UpdateFramework\ota-artifacts"

# CapCut old versions
$capcutOld = 0
$capcutApps = "$env:USERPROFILE\AppData\Local\CapCut\Apps"
if (Test-Path $capcutApps) {
    $versions = Get-ChildItem -Path $capcutApps -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^\d+(\.\d+)+' } | Sort-Object Name
    if ($versions.Count -gt 1) {
        $oldOnes = $versions | Where-Object { $_.FullName -ne $versions[-1].FullName }
        foreach ($old in $oldOnes) {
            $capcutOld += Get-SizeMB $old.FullName
        }
    }
}

# VS Code
$vsc1 = Get-SizeMB "$env:APPDATA\Code\CachedExtensionVSIXs"
$vsc2 = Get-SizeMB "$env:APPDATA\Code\Crashpad\reports"
$vsc3 = Get-SizeMB "$env:APPDATA\Code\CachedData"
$vsc = [math]::Round(($vsc1 + $vsc2 + $vsc3), 2)

# Dev
$dev1 = Get-SizeMB "$env:LOCALAPPDATA\npm-cache"
$dev2 = Get-SizeMB "$env:LOCALAPPDATA\pip\cache"
$dev = [math]::Round(($dev1 + $dev2), 2)

# Discord
$discord = Get-SizeMB "$env:APPDATA\discord\Cache"

# Check Scheduled Task
schtasks /query /tn "StorageOptimizerAutoClean" 2>&1 | Out-Null
$hasTask = ($LASTEXITCODE -eq 0)

[PSCustomObject]@{
    disk = [PSCustomObject]@{
        totalGB = $totalGB
        freeGB = $freeGB
        usedGB = $usedGB
        percentFree = $pctFree
    }
    categories = @(
        [PSCustomObject]@{ id = "recycle_bin"; name = "Recycle Bin"; sizeMB = $rb; desc = "Deleted files and build caches" },
        [PSCustomObject]@{ id = "temp_files"; name = "Temporary System & User Files"; sizeMB = [math]::Round(($temp1 + $temp2), 2); desc = "Windows and application temp files" },
        [PSCustomObject]@{ id = "nvidia_artifacts"; name = "NVIDIA Driver Update Artifacts"; sizeMB = $nv; desc = "Downloaded GPU driver packages and setup files" },
        [PSCustomObject]@{ id = "capcut_old"; name = "Outdated CapCut Versions"; sizeMB = [math]::Round($capcutOld, 2); desc = "Older CapCut builds leftover after updates" },
        [PSCustomObject]@{ id = "vscode_cache"; name = "VS Code Caches & Crashpad Dumps"; sizeMB = $vsc; desc = "Cached extension installers and crash logs" },
        [PSCustomObject]@{ id = "dev_cache"; name = "npm & pip Caches"; sizeMB = $dev; desc = "Node and Python package download caches" },
        [PSCustomObject]@{ id = "discord_cache"; name = "Discord Cache"; sizeMB = $discord; desc = "Discord media and shader caches" }
    )
    scheduledTask = [PSCustomObject]@{
        enabled = $hasTask
    }
} | ConvertTo-Json -Depth 4

