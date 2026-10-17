# Storage Optimizer - Automated Background Cleanup Script
# Can be run silently on schedule or called on demand

param(
    [string[]]$Categories = @("recycle_bin", "temp_files", "nvidia_artifacts", "capcut_old", "vscode_cache", "dev_cache", "discord_cache")
)

$ErrorActionPreference = "SilentlyContinue"
$beforeBytes = (Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='C:'").FreeSpace
$log = @()

function Log-Action($msg) {
    $timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    $line = "[$timestamp] $msg"
    Write-Output $line
    $global:log += $line
}

Log-Action "Starting Storage Optimization..."

# 1. Recycle Bin
if ($Categories -contains "recycle_bin") {
    Log-Action "Cleaning Recycle Bin..."
    $userSid = ([System.Security.Principal.WindowsIdentity]::GetCurrent()).User.Value
    $rbPath = "C:\`$Recycle.Bin\$userSid"
    if (Test-Path $rbPath) {
        Get-ChildItem -Path $rbPath -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# 2. Temp Files
if ($Categories -contains "temp_files") {
    Log-Action "Cleaning User and Windows Temp files..."
    Get-ChildItem -Path "$env:TEMP" -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    Get-ChildItem -Path "C:\Windows\Temp" -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

# 3. NVIDIA App Update Artifacts
if ($Categories -contains "nvidia_artifacts") {
    Log-Action "Purging NVIDIA driver update artifacts..."
    $nvPath = "C:\ProgramData\NVIDIA Corporation\NVIDIA app\UpdateFramework\ota-artifacts"
    if (Test-Path $nvPath) {
        Get-ChildItem -Path $nvPath -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# 4. Outdated CapCut Versions
if ($Categories -contains "capcut_old") {
    Log-Action "Checking for outdated CapCut versions..."
    $capcutApps = "$env:USERPROFILE\AppData\Local\CapCut\Apps"
    if (Test-Path $capcutApps) {
        # Find all version folders (folders named with numbers/dots)
        $versions = Get-ChildItem -Path $capcutApps -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^\d+(\.\d+)+' } | Sort-Object Name
        if ($versions.Count -gt 1) {
            # Keep only the latest version, delete older ones
            $latest = $versions[-1]
            $oldOnes = $versions | Where-Object { $_.FullName -ne $latest.FullName }
            foreach ($old in $oldOnes) {
                Log-Action "Removing old CapCut version: $($old.Name)"
                Remove-Item -Path $old.FullName -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }
}

# 5. VS Code Caches & Crash Dumps
if ($Categories -contains "vscode_cache") {
    Log-Action "Cleaning VS Code caches and crashpad reports..."
    $vscodePaths = @(
        "$env:APPDATA\Code\CachedExtensionVSIXs",
        "$env:APPDATA\Code\Crashpad\reports",
        "$env:APPDATA\Code\CachedData",
        "$env:APPDATA\Code\Cache"
    )
    foreach ($p in $vscodePaths) {
        if (Test-Path $p) {
            Get-ChildItem -Path $p -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

# 6. Developer Package Caches (npm, pip)
if ($Categories -contains "dev_cache") {
    Log-Action "Cleaning npm and pip caches..."
    if (Test-Path "$env:LOCALAPPDATA\npm-cache") {
        Remove-Item -Path "$env:LOCALAPPDATA\npm-cache" -Recurse -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path "$env:LOCALAPPDATA\pip\cache") {
        Remove-Item -Path "$env:LOCALAPPDATA\pip\cache" -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# 7. Discord Cache
if ($Categories -contains "discord_cache") {
    Log-Action "Cleaning Discord cache..."
    $dc = "$env:APPDATA\discord\Cache"
    if (Test-Path $dc) {
        Remove-Item -Path "$dc\*" -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$afterBytes = (Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='C:'").FreeSpace
$freedBytes = [math]::Max(0, ($afterBytes - $beforeBytes))
$freedMB = [math]::Round($freedBytes / 1MB, 2)
$freedGB = [math]::Round($freedBytes / 1GB, 2)
$currentFreeGB = [math]::Round($afterBytes / 1GB, 2)

Log-Action "Optimization Finished! Freed: $freedMB MB ($freedGB GB). Current C: Free Space: $currentFreeGB GB."

