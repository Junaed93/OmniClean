# OmniClean

Universal Windows Storage Optimizer and Performance Utility.

OmniClean is a native, lightweight Windows desktop application designed to inspect system drives, reclaim disk space from redundant caches, remove obsolete application files, and optimize overall system responsiveness. It runs as a standalone executable with zero external runtime dependencies.

---

## Key Features

- **Universal Hardware and OS Support**: Fully compatible with Windows 10, Windows 11, and Windows Server across x64 and x86 architectures.
- **Dynamic System Inspection**: Detects host processor specifications, physical RAM capacity, graphics hardware, and Windows system root dynamically at runtime without hardcoded paths.
- **Multi-Drive Selection**: Allows users to inspect and optimize any attached fixed storage volume (C:, D:, E:, etc.).
- **Universal Browser Cache Cleaner**: Scans and cleans user-level cache and shader data across Google Chrome, Microsoft Edge, Brave Browser, Mozilla Firefox, and Opera without modifying active logins, history, or saved passwords.
- **Windows System Cleanup**:
  - Empties the Windows Recycle Bin using the native Win32 SHEmptyRecycleBin API.
  - Cleans user temporary directories (%TEMP%) and Windows system temporary files.
  - Purges completed Windows Update download files located in SoftwareDistribution\Download.
  - Clears Windows Error Reporting (WER) queue archives and crash dump files.
- **Developer and Application Caches**:
  - Package manager caches: npm-cache and pip cache.
  - Visual Studio Code extension installers, crash reports, and cached editor data.
  - Discord media and shader caches.
  - Obsolete CapCut version builds (removes older versions while preserving the latest active release).
  - Outdated NVIDIA GPU driver update installer packages.
- **Large File Finder**: Identifies hidden large files (>100 MB) consuming disk space, with quick navigation to reveal files in Windows Explorer.
- **Startup Optimizer**: Identifies auto-launch programs configured in the Windows Registry (HKCU\Run and HKLM\Run) and allows one-click disabling to reduce boot times and free memory.
- **Silent Background Automation**: Integrates with the Windows Task Scheduler to run maintenance routines automatically and silently on a weekly schedule.

---

## Technical Architecture

- **Language**: C# / .NET Framework 4.5+ (Pre-installed natively on all modern Windows installations).
- **GUI Framework**: Windows Presentation Foundation (WPF) with hardware-accelerated rendering.
- **Binary Size**: ~65 KB standalone executable with embedded application icon.
- **Dependencies**: None. Compiles using the built-in Microsoft Visual C# compiler (csc.exe).
- **Silent Execution Mode**: Supports the `--silent-clean` CLI parameter for headless automated operation via scheduled tasks.

---

## Compilation Instructions

OmniClean can be compiled directly on any Windows system using the native C# compiler without requiring Visual Studio or external package managers.

### 1-Click Build Script

Run `build.bat` from the root directory.

### Manual PowerShell Build

Execute the following commands in PowerShell:

```powershell
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$wpfDir = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"

& $csc /target:winexe /platform:anycpu /optimize+ /lib:"$wpfDir" `
  /r:PresentationFramework.dll /r:PresentationCore.dll /r:WindowsBase.dll `
  /r:System.Xaml.dll /r:System.dll /r:System.Core.dll `
  /win32icon:"app.ico" /out:"OmniClean.exe" "OmniClean.cs"
```

---

## Command Line Usage

| Command | Description |
| :--- | :--- |
| `OmniClean.exe` | Launches the graphical user interface. |
| `OmniClean.exe --silent-clean` | Executes a silent background cleanup of safe temporary files and caches without opening the GUI. |

---

## Project Structure

```text
OmniClean/
|-- OmniClean.cs       # Complete C# WPF application source code
|-- OmniClean.exe      # Compiled standalone executable
|-- app.ico            # Application icon asset
|-- build.bat          # Automated compilation script
|-- cleaner.ps1        # Standalone PowerShell cleanup utility
|-- scan.ps1           # Standalone PowerShell storage analyzer
|-- README.md          # Project documentation
`-- .gitignore         # Version control exclusion rules
```

---

## License

This project is licensed under the MIT License. Free for personal and commercial use.
