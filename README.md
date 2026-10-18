# OmniClean

Version: 1.0.0
Universal Windows Storage Optimizer and Performance Utility.

OmniClean is a native, lightweight Windows desktop application engineered to inspect system drives, reclaim disk space from redundant caches, discover large or duplicate files, trim idle memory working sets, and optimize overall system responsiveness. It operates as a single standalone executable with zero external runtime dependencies.

---

## Capabilities Matrix

### What OmniClean Can Do

1. **Storage Drive Analysis and Multi-Volume Support**
   - Automatically detects all active local fixed drives (C:, D:, E:, etc.).
   - Computes capacity, free space, used space, and usage percentage with color-coded safety indicators.
   - Monitors storage metrics dynamically before and after cleaning routines.

2. **Multi-Target Cache and Garbage Purging**
   - **Windows Recycle Bin**: Cleans all user recycle bins via native Win32 `SHEmptyRecycleBin` APIs without confirmation prompts or sound disruptions.
   - **User & System Temp**: Clears temporary directories (`%TEMP%` and `C:\Windows\Temp`).
   - **Universal Browser Cache**: Purges temporary disk cache, GPU shader caches, and code caches across Google Chrome, Microsoft Edge, Brave Browser, Mozilla Firefox, and Opera.
   - **Windows Update Downloads**: Removes stale download payloads in `C:\Windows\SoftwareDistribution\Download` after updates are applied.
   - **Crash Dumps and WER**: Clears user crash dump repositories (`%LOCALAPPDATA%\CrashDumps`) and Windows Error Reporting queue and archive directories.
   - **NVIDIA GPU Update Staging**: Removes leftover driver installer archives in `ProgramData\NVIDIA Corporation\NVIDIA app\UpdateFramework\ota-artifacts`.
   - **CapCut Version Management**: Scans video editing installation folders and prunes obsolete version builds while safeguarding the active build.
   - **VS Code Development Caches**: Purges cached extension `.vsix` installers, Crashpad reports, and editor cached data.
   - **Developer Package Caches**: Clears `npm-cache` and `pip cache` directories to reclaim disk space from accumulated node and python packages.
   - **Discord Media & Shader Caches**: Clears discord cache folders without resetting account sessions.

3. **Dry Run Simulation Mode**
   - Provides a non-destructive simulation toggle (`Dry Run (Simulation Only)`).
   - Allows users to preview potential disk space reclamation and inspect affected directory paths without deleting a single file.

4. **Working Set Memory Optimization**
   - Flushes idle memory pages across active processes via the Windows PSAPI `EmptyWorkingSet` subsystem.
   - Reclaims physical RAM without terminating or restarting background applications.
   - Features real-time RAM load percentage and capacity tracking.

5. **Large File Discovery and Size Filters**
   - Scans user directories recursively to locate hidden files exceeding 100 MB.
   - Features 1-click filter pills: All (>100 MB), Media/Archives (>500 MB), and Huge (>1 GB).
   - Displays file extension badges, file names, file paths, size badges, and provides instant buttons to reveal files in Windows Explorer or safely delete them.

6. **Cryptographic Duplicate File Finder and 1-Click Clean**
   - Employs a fast two-phase duplicate discovery algorithm:
     - Phase 1: Groups candidates by exact file byte size.
     - Phase 2: Computes SHA-256 cryptographic hashes for size-matched files to ensure 100% byte-level certainty before identifying duplicates.
   - Distinguishes original files with green protected badges and redundant copies with copy badges.
   - Features a 1-click `Delete All Duplicate Copies` action that safely eliminates all duplicate files while keeping 100% of original files intact.

7. **Windows Startup Application Manager**
   - Reads configured auto-launch applications from the Windows Registry (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run` and `HKLM\Software\Microsoft\Windows\CurrentVersion\Run`).
   - Interactive toggle switches: Click to toggle between ACTIVE (emerald badge) and DISABLED (slate badge) states to accelerate PC boot times.

8. **Silent Background Automation**
   - Supports seamless integration with Windows Task Scheduler via native command line execution.
   - Schedules weekly maintenance runs executing `OmniClean.exe --silent-clean` in headless mode.

9. **Diagnostic Activity Logging and Export**
   - Displays real-time operation timestamps and progress details in a built-in terminal console.
   - Provides `Clear` and `Export Log` actions to save cleanup histories and scan reports as standard text files.

10. **Hardware and System Telemetry HUD**
    - Identifies processor architecture, core counts, CPU model, total physical RAM, and live system uptime without requiring external diagnostic tools.

---

### What OmniClean Does Not Do (Safety Boundaries)

OmniClean is intentionally engineered with strict safety constraints to prevent data loss or system instability:

1. **Does Not Delete Personal User Documents**
   - OmniClean never touches or removes user files in Documents, Desktop, Pictures, Music, or OneDrive directories during general storage cleanups.
   - Duplicates and large files are only removed when manually reviewed and explicitly deleted by the user.

2. **Does Not Erase Browser Logins, Passwords, or Cookies**
   - Browser cleaning is strictly constrained to cached media, shaders, and temporary rendering files (`Cache`, `Code Cache`, `GPUCache`).
   - Saved passwords, cookies, session tokens, bookmarks, and browsing history are never modified.

3. **Does Not Perform Destructive Registry Cleaning**
   - OmniClean does not run speculative registry sweeps that can corrupt COM interfaces, file associations, or system stability.
   - Registry interaction is exclusively limited to reading and toggling documented startup keys under `Software\Microsoft\Windows\CurrentVersion\Run`.

4. **Does Not Perform SSD Defragmentation**
   - Solid State Drives (SSDs) and NVMe drives experience unnecessary flash cell wear when subjected to traditional defragmentation. OmniClean does not defragment storage media.

5. **Does Not Modify Windows Protected System Files**
   - OmniClean respects Windows File Protection and System File Checker (`SFC`) boundaries. Crucial system DLLs, driver stores, and WinSxS components remain untouched.

6. **Does Not Run Unwanted Background Resident Processes**
   - OmniClean has zero tray icons, zero background daemon services, and zero continuous resource consumption when closed.
   - When automation is enabled, it relies entirely on the built-in Windows Task Scheduler.

---

## Technical Architecture

- **Language**: C# 5 / .NET Framework 4.5+ (Natively pre-installed on Windows 10, Windows 11, and Windows Server).
- **GUI Engine**: Windows Presentation Foundation (WPF) with dark theme and hardware acceleration.
- **Navigation Layout**: Left Sidebar navigation with active indicator bars and dedicated workspace canvas.
- **Interactive Feedback**: Dynamic card hover effects with illumination highlights.
- **Binary Footprint**: ~158 KB standalone executable with embedded application icon.
- **External Dependencies**: Zero. No NuGet dependencies, no Node.js runtime, no Python runtime.
- **Build System**: Compiles using the native Microsoft Visual C# compiler (`csc.exe`).

---

## Compilation Instructions

OmniClean can be compiled directly on any Windows workstation without installing Visual Studio or third-party SDKs.

### Option 1: Automated Script

Execute `build.bat` from the root directory.

### Option 2: PowerShell Compilation

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

| Command | Functionality |
| :--- | :--- |
| `OmniClean.exe` | Launches the interactive graphical user interface. |
| `OmniClean.exe --silent-clean` | Executes a headless, background cleanup of temporary caches without opening the GUI window. |

---

## Repository and CI/CD Pipeline

- **Source Repository**: `https://github.com/Junaed93/OmniClean.git`
- **Release Automation**: GitHub Actions workflow (`.github/workflows/release.yml`) automatically compiles and attaches `OmniClean.exe` to official GitHub Releases on tag pushes (`v1.0.0`, `v*`).
- **Binary Distribution**: Standalone executables are distributed exclusively through GitHub Releases; no binary `.exe` files are tracked in the Git repository tree.

---

## License

This project is licensed under the MIT License. Free for personal, organizational, and educational use.
