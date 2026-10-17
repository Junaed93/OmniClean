# OmniClean ⚡

> **Universal Windows Storage Optimizer & Performance Utility**  
> A lightweight, ultra-fast native Windows tool to automatically reclaim disk space, purge system junk, and keep your PC running at peak performance.

---

## 🚀 Features

- **⚡ Universal Compatibility**: Runs out of the box on Windows 10, Windows 11, and Windows Server (x64 and x86). Zero external dependencies or runtimes required.
- **🖥️ Dynamic Hardware Detection**: Automatically detects host CPU (Intel/AMD), physical RAM, active GPU, and Windows system drive.
- **💾 Multi-Drive Selector**: Inspects and cleans any connected fixed drive (`C:`, `D:`, `E:`, etc.).
- **🌐 Universal Web Browser Cleaning**: Cleans cache and shader data across Google Chrome, Microsoft Edge, Brave Browser, Mozilla Firefox, and Opera without touching logins or bookmarks.
- **🗑️ Deep Windows Cleaning**:
  - Windows Recycle Bin (via native Win32 `SHEmptyRecycleBin` API)
  - System & User Temp Files (`%TEMP%` and `C:\Windows\Temp`)
  - Windows Update Download Cache (`SoftwareDistribution\Download`)
  - Windows Error Reporting (WER) and memory dump files
- **📦 Power-User & Developer Caches**:
  - npm and pip download caches
  - VS Code extension and crashpad caches
  - Discord client media and shader cache
  - Outdated CapCut version remnants
  - NVIDIA driver update installation packages (`ota-artifacts`)
- **⏰ Silent Background Auto-Scheduler**:
  - 1-click toggle to schedule silent background cleanup every Sunday at 12:00 PM via Windows Task Scheduler.
- **📊 Real-Time Activity Log**:
  - Transparent live logging of all files and folders being scanned and cleaned.

---

## 🛠️ How to Build from Source

OmniClean is written in C# and WPF. It compiles with the built-in Microsoft .NET Framework C# compiler (`csc.exe`) available on all Windows installations.

### 1-Click Build:
Simply double-click `build.bat`, or run the following command in PowerShell:

```powershell
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$wpfDir = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"

& $csc /target:winexe /platform:anycpu /optimize+ /lib:"$wpfDir" `
  /r:PresentationFramework.dll /r:PresentationCore.dll /r:WindowsBase.dll `
  /r:System.Xaml.dll /r:System.dll /r:System.Core.dll `
  /win32icon:"app.ico" /out:"OmniClean.exe" "OmniClean.cs"
```

The resulting `OmniClean.exe` is a standalone, portable 54 KB binary.

---

## 🏃 Usage

1. Run `OmniClean.exe`.
2. Click **🔄 Rescan Storage** to inspect your drive.
3. Select the categories you wish to purge.
4. Click **⚡ Clean Selected Storage**.
5. *(Optional)* Toggle **Enable Weekly Silent Auto-Clean** to let Windows maintain your drive automatically.

---

## 📜 License

MIT License. Free for personal and commercial use.
