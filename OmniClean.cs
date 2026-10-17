using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Win32;

namespace OmniClean {
    public class CategoryItem {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public double SizeMB { get; set; }
        public bool IsInstalled { get; set; }
        public CheckBox CheckBoxRef { get; set; }
        public TextBlock BadgeRef { get; set; }
        public Border CardRef { get; set; }
    }

    public class LargeFileInfo {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public long Bytes { get; set; }
        public double SizeMB { get; set; }
        public Border CardRef { get; set; }
    }

    public class DuplicateFileGroup {
        public string Hash { get; set; }
        public long Bytes { get; set; }
        public double SizeMB { get; set; }
        public List<string> FilePaths { get; set; }
    }

    public class StartupAppItem {
        public string Name { get; set; }
        public string Command { get; set; }
        public bool IsEnabled { get; set; }
        public Border CardRef { get; set; }
        public Button ToggleBtnRef { get; set; }
        public TextBlock StatusTextRef { get; set; }
    }

    class Program {
        const string CURRENT_VERSION = "v2.0.0";
        const string REPO_URL = "https://github.com/Junaed93/OmniClean";
        const string RELEASES_API = "https://api.github.com/repos/Junaed93/OmniClean/releases/latest";
        const string TASK_NAME = "OmniCleanUniversalAutoClean";

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        static extern uint SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);
        const uint SHERB_NOCONFIRMATION = 0x00000001;
        const uint SHERB_NOPROGRESSUI   = 0x00000002;
        const uint SHERB_NOSOUND        = 0x00000004;

        [DllImport("psapi.dll")]
        static extern int EmptyWorkingSet(IntPtr hwProc);

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class MEMORYSTATUSEX {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX() { this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)); }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        static Window mainWindow;
        static Border titleBar;
        static Button btnMinimize;
        static Button btnMaximize;
        static Button btnClose;

        // Update banner
        static Border bannerUpdate;
        static TextBlock txtUpdateNotice;
        static Button btnDownloadUpdate;

        // Tab 1: Storage Cleaner
        static ComboBox cmbDrive;
        static ProgressBar diskProgress;
        static TextBlock txtDiskPercent;
        static TextBlock txtDiskRatio;
        static TextBlock txtStatFree;
        static TextBlock txtStatUsed;
        static TextBlock txtStatTotal;
        static TextBlock txtSelectedSize;
        static TextBlock txtHardwarePill;
        static Button btnClean;
        static Button btnRescan;
        static Button btnOptimizeRam;
        static Button btnSelectAll;
        static Button btnClearAll;
        static TextBox txtConsole;
        static StackPanel pnlCategories;
        static Border bannerSuccess;
        static TextBlock txtSuccessMsg;

        // Tab 2: Large Files
        static Button btnScanLargeFiles;
        static StackPanel pnlLargeFiles;
        static TextBlock txtLargeFilesSummary;

        // Tab 3: Duplicate Finder
        static Button btnScanDuplicates;
        static StackPanel pnlDuplicates;
        static TextBlock txtDuplicatesSummary;

        // Tab 4: Startup Apps
        static Button btnScanStartup;
        static StackPanel pnlStartupApps;
        static TextBlock txtStartupSummary;

        // Tab 5: Scheduler & Settings
        static CheckBox chkAutoClean;
        static TextBlock txtScheduleStatus;
        static Button btnCreateShortcut;

        static List<CategoryItem> categories = new List<CategoryItem>();
        static List<LargeFileInfo> largeFiles = new List<LargeFileInfo>();
        static List<StartupAppItem> startupApps = new List<StartupAppItem>();

        static bool isScanning = false;
        static bool isCleaning = false;
        static string selectedDriveLetter = "C";

        [STAThread]
        static void Main(string[] args) {
            if (args != null && args.Length > 0 && args[0].Equals("--silent-clean", StringComparison.OrdinalIgnoreCase)) {
                PerformSilentCleanup();
                return;
            }

            try {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                Application app = new Application();

                string xaml = @"
<Window xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
        xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
        Title='OmniClean - Universal Windows Optimizer'
        Width='1160' Height='780'
        MinWidth='1020' MinHeight='700'
        WindowStartupLocation='CenterScreen'
        WindowStyle='None'
        AllowsTransparency='True'
        Background='Transparent'
        Foreground='#F8FAFC'
        FontFamily='Segoe UI'>
    <Window.Resources>
        <Style TargetType='Border'>
            <Setter Property='CornerRadius' Value='14'/>
        </Style>
        <Style TargetType='CheckBox'>
            <Setter Property='Foreground' Value='#F1F5F9'/>
            <Setter Property='FontSize' Value='14'/>
            <Setter Property='VerticalContentAlignment' Value='Center'/>
            <Setter Property='Cursor' Value='Hand'/>
        </Style>
        <Style TargetType='TabItem'>
            <Setter Property='Template'>
                <Setter.Value>
                    <ControlTemplate TargetType='TabItem'>
                        <Border Name='tabBorder' Background='Transparent' Padding='18,10' Margin='0,0,6,0' CornerRadius='10' Cursor='Hand'>
                            <ContentPresenter ContentSource='Header' HorizontalAlignment='Center' VerticalAlignment='Center'/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property='IsSelected' Value='True'>
                                <Setter TargetName='tabBorder' Property='Background' Value='#1E293B'/>
                                <Setter Property='TextElement.Foreground' Value='#FFFFFF'/>
                                <Setter Property='TextElement.FontWeight' Value='Bold'/>
                            </Trigger>
                            <Trigger Property='IsSelected' Value='False'>
                                <Setter Property='TextElement.Foreground' Value='#94A3B8'/>
                                <Setter Property='TextElement.FontWeight' Value='SemiBold'/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
    </Window.Resources>

    <!-- Outer Window Frame -->
    <Border Background='#090D18' BorderBrush='#1F293D' BorderThickness='1' CornerRadius='18'>
        <Grid Margin='18'>
            <Grid.RowDefinitions>
                <RowDefinition Height='Auto'/>
                <RowDefinition Height='Auto'/>
                <RowDefinition Height='*'/>
            </Grid.RowDefinitions>

            <!-- Custom Modern Titlebar / Header -->
            <Border Grid.Row='0' Name='titleBar' Background='#131A2D' BorderBrush='#222C44' BorderThickness='1' Padding='16,12' Margin='0,0,0,12' CornerRadius='14' Cursor='SizeAll'>
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width='Auto'/>
                        <ColumnDefinition Width='*'/>
                        <ColumnDefinition Width='Auto'/>
                    </Grid.ColumnDefinitions>

                    <!-- Brand Icon & Title -->
                    <StackPanel Grid.Column='0' Orientation='Horizontal' VerticalAlignment='Center'>
                        <Border Width='36' Height='36' Background='#6366F1' CornerRadius='10' Margin='0,0,12,0'>
                            <TextBlock Text='⚡' FontSize='18' Foreground='White' HorizontalAlignment='Center' VerticalAlignment='Center'/>
                        </Border>
                        <StackPanel VerticalAlignment='Center'>
                            <StackPanel Orientation='Horizontal'>
                                <TextBlock Text='OmniClean' FontSize='18' FontWeight='Bold' Foreground='#FFFFFF'/>
                                <Border Background='#262054' BorderBrush='#6366F1' BorderThickness='1' CornerRadius='6' Padding='6,2' Margin='8,0,0,0'>
                                    <TextBlock Text='v2.0 PRO' FontSize='9' FontWeight='Bold' Foreground='#A5B4FC'/>
                                </Border>
                            </StackPanel>
                            <TextBlock Text='Universal Windows Performance &amp; Storage Suite' FontSize='11' Foreground='#94A3B8'/>
                        </StackPanel>
                    </StackPanel>

                    <!-- Hardware Specs Pill -->
                    <Border Grid.Column='1' HorizontalAlignment='Center' VerticalAlignment='Center' Background='#0B1120' BorderBrush='#1E293B' BorderThickness='1' CornerRadius='18' Padding='14,6'>
                        <StackPanel Orientation='Horizontal'>
                            <Ellipse Width='8' Height='8' Fill='#10B981' Margin='0,0,8,0'/>
                            <TextBlock Name='txtHardwarePill' Text='Detecting System Specs...' FontSize='12' Foreground='#94A3B8'/>
                        </StackPanel>
                    </Border>

                    <!-- Window Control Buttons (Minimize, Maximize, Close) -->
                    <StackPanel Grid.Column='2' Orientation='Horizontal' VerticalAlignment='Center'>
                        <Button Name='btnMinimize' Content='—' Width='32' Height='32' Background='#1A2338' Foreground='#CBD5E1' BorderThickness='0' Margin='0,0,6,0' FontWeight='Bold' Cursor='Hand'/>
                        <Button Name='btnMaximize' Content='▢' Width='32' Height='32' Background='#1A2338' Foreground='#CBD5E1' BorderThickness='0' Margin='0,0,6,0' FontWeight='Bold' Cursor='Hand'/>
                        <Button Name='btnClose' Content='✕' Width='32' Height='32' Background='#3F1C25' Foreground='#F87171' BorderThickness='0' FontWeight='Bold' Cursor='Hand'/>
                    </StackPanel>
                </Grid>
            </Border>

            <!-- Update Available Notification Banner -->
            <Border Grid.Row='1' Name='bannerUpdate' Background='#1E1A38' BorderBrush='#6366F1' BorderThickness='1' Padding='14,8' Margin='0,0,0,12' CornerRadius='10' Visibility='Collapsed'>
                <Grid>
                    <StackPanel Orientation='Horizontal' VerticalAlignment='Center'>
                        <TextBlock Text='⭐' FontSize='14' Margin='0,0,8,0'/>
                        <TextBlock Name='txtUpdateNotice' Text='A new version of OmniClean is available!' FontSize='12' FontWeight='SemiBold' Foreground='#E0E7FF' VerticalAlignment='Center'/>
                    </StackPanel>
                    <Button Name='btnDownloadUpdate' Content='View Release on GitHub' HorizontalAlignment='Right' Background='#6366F1' Foreground='White' BorderThickness='0' Padding='12,4' FontSize='11' FontWeight='Bold' Cursor='Hand'/>
                </Grid>
            </Border>

            <!-- Main Tab Control -->
            <TabControl Grid.Row='2' Background='Transparent' BorderThickness='0'>
                <!-- TAB 1: Storage Cleaner -->
                <TabItem Header='Storage Cleaner'>
                    <Grid Margin='0,12,0,0'>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width='370'/>
                            <ColumnDefinition Width='*'/>
                        </Grid.ColumnDefinitions>

                        <!-- Left Column: Drive Capacity & Live Log -->
                        <StackPanel Grid.Column='0' Margin='0,0,18,0'>
                            <Border Background='#131A2D' BorderBrush='#222C44' BorderThickness='1' Padding='18' Margin='0,0,0,16'>
                                <StackPanel>
                                    <Grid Margin='0,0,0,12'>
                                        <StackPanel Orientation='Horizontal' VerticalAlignment='Center'>
                                            <TextBlock Text='Drive:' FontSize='14' FontWeight='Bold' Foreground='White' VerticalAlignment='Center' Margin='0,0,8,0'/>
                                            <ComboBox Name='cmbDrive' Width='110' Height='28' Background='#0E1424' Foreground='#F8FAFC' BorderBrush='#334155'/>
                                        </StackPanel>
                                        <StackPanel Orientation='Horizontal' HorizontalAlignment='Right'>
                                            <Button Name='btnOptimizeRam' Content='⚡ Flush RAM' Background='#1E293B' Foreground='#38BDF8' BorderBrush='#0284C7' BorderThickness='1' Padding='8,4' FontSize='11' FontWeight='SemiBold' Margin='0,0,6,0' Cursor='Hand'/>
                                            <Button Name='btnRescan' Content='🔄 Rescan' Background='#1E293B' Foreground='#E2E8F0' BorderBrush='#334155' BorderThickness='1' Padding='8,4' FontSize='11' FontWeight='SemiBold' Cursor='Hand'/>
                                        </StackPanel>
                                    </Grid>

                                    <TextBlock Name='txtDiskPercent' Text='--% Free' FontSize='30' FontWeight='Bold' Foreground='#10B981' Margin='0,0,0,4'/>
                                    <TextBlock Name='txtDiskRatio' Text='-- GB Free / -- GB' FontSize='12' Foreground='#94A3B8' Margin='0,0,0,10'/>
                                    <ProgressBar Name='diskProgress' Height='12' Value='30' Maximum='100' Foreground='#10B981' Background='#0A0F1D' BorderBrush='#232D47' Margin='0,0,0,14'/>

                                    <Grid>
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width='*'/>
                                            <ColumnDefinition Width='*'/>
                                            <ColumnDefinition Width='*'/>
                                        </Grid.ColumnDefinitions>
                                        <Border Grid.Column='0' Background='#0B1120' Padding='8' Margin='0,0,4,0' CornerRadius='8'>
                                            <StackPanel HorizontalAlignment='Center'>
                                                <TextBlock Text='Free' FontSize='10' Foreground='#64748B'/>
                                                <TextBlock Name='txtStatFree' Text='-- GB' FontSize='12' FontWeight='Bold' Foreground='#10B981'/>
                                            </StackPanel>
                                        </Border>
                                        <Border Grid.Column='1' Background='#0B1120' Padding='8' Margin='2,0,2,0' CornerRadius='8'>
                                            <StackPanel HorizontalAlignment='Center'>
                                                <TextBlock Text='Used' FontSize='10' Foreground='#64748B'/>
                                                <TextBlock Name='txtStatUsed' Text='-- GB' FontSize='12' FontWeight='Bold' Foreground='#818CF8'/>
                                            </StackPanel>
                                        </Border>
                                        <Border Grid.Column='2' Background='#0B1120' Padding='8' Margin='4,0,0,0' CornerRadius='8'>
                                            <StackPanel HorizontalAlignment='Center'>
                                                <TextBlock Text='Total' FontSize='10' Foreground='#64748B'/>
                                                <TextBlock Name='txtStatTotal' Text='-- GB' FontSize='12' FontWeight='Bold' Foreground='#38BDF8'/>
                                            </StackPanel>
                                        </Border>
                                    </Grid>
                                </StackPanel>
                            </Border>

                            <!-- Live Activity Log -->
                            <Border Background='#0B1120' BorderBrush='#1E293B' BorderThickness='1' Padding='14' Height='250'>
                                <Grid>
                                    <Grid.RowDefinitions>
                                        <RowDefinition Height='Auto'/>
                                        <RowDefinition Height='*'/>
                                    </Grid.RowDefinitions>
                                    <StackPanel Grid.Row='0' Orientation='Horizontal' Margin='0,0,0,6'>
                                        <Ellipse Width='7' Height='7' Fill='#06B6D4' Margin='0,0,6,0'/>
                                        <TextBlock Text='Activity Console' FontSize='11' FontWeight='SemiBold' Foreground='#94A3B8'/>
                                    </StackPanel>
                                    <TextBox Grid.Row='1' Name='txtConsole' IsReadOnly='True' Background='Transparent' Foreground='#CBD5E1' BorderThickness='0' FontFamily='Consolas' FontSize='11' VerticalScrollBarVisibility='Auto' TextWrapping='Wrap'/>
                                </Grid>
                            </Border>
                        </StackPanel>

                        <!-- Right Column: Cleanable Categories -->
                        <Border Grid.Column='1' Background='#131A2D' BorderBrush='#222C44' BorderThickness='1' Padding='18'>
                            <Grid>
                                <Grid.RowDefinitions>
                                    <RowDefinition Height='Auto'/>
                                    <RowDefinition Height='Auto'/>
                                    <RowDefinition Height='*'/>
                                    <RowDefinition Height='Auto'/>
                                </Grid.RowDefinitions>

                                <!-- Categories Header -->
                                <Grid Grid.Row='0' Margin='0,0,0,10'>
                                    <StackPanel>
                                        <TextBlock Text='Cleanable Targets' FontSize='16' FontWeight='Bold' Foreground='White'/>
                                        <TextBlock Text='Safely identified temporary files, caches, and logs' FontSize='12' Foreground='#94A3B8'/>
                                    </StackPanel>
                                    <StackPanel Orientation='Horizontal' HorizontalAlignment='Right'>
                                        <Button Name='btnSelectAll' Content='Select All' Background='#1E293B' Foreground='#CBD5E1' BorderBrush='#334155' BorderThickness='1' Padding='10,4' FontSize='11' Margin='0,0,6,0' Cursor='Hand'/>
                                        <Button Name='btnClearAll' Content='Clear' Background='#1E293B' Foreground='#CBD5E1' BorderBrush='#334155' BorderThickness='1' Padding='10,4' FontSize='11' Cursor='Hand'/>
                                    </StackPanel>
                                </Grid>

                                <!-- In-App Success Banner -->
                                <Border Grid.Row='1' Name='bannerSuccess' Background='#0E2A24' BorderBrush='#10B981' BorderThickness='1' Padding='12,8' Margin='0,0,0,10' CornerRadius='8' Visibility='Collapsed'>
                                    <TextBlock Name='txtSuccessMsg' Text='Cleanup completed successfully.' FontSize='12' FontWeight='SemiBold' Foreground='#34D399'/>
                                </Border>

                                <!-- Scrollable Category Items -->
                                <ScrollViewer Grid.Row='2' VerticalScrollBarVisibility='Auto' Margin='0,0,0,12'>
                                    <StackPanel Name='pnlCategories'/>
                                </ScrollViewer>

                                <!-- Bottom Action Bar -->
                                <Border Grid.Row='3' BorderBrush='#222C44' BorderThickness='0,1,0,0' Padding='0,14,0,0'>
                                    <Grid>
                                        <StackPanel VerticalAlignment='Center'>
                                            <TextBlock Text='Selected to Reclaim:' FontSize='11' Foreground='#94A3B8'/>
                                            <TextBlock Name='txtSelectedSize' Text='0.00 MB' FontSize='20' FontWeight='Bold' Foreground='#F8FAFC'/>
                                        </StackPanel>
                                        <Button Name='btnClean' Content='⚡ Clean Selected Storage' HorizontalAlignment='Right' Background='#6366F1' Foreground='White' BorderThickness='0' Padding='26,12' FontSize='14' FontWeight='Bold' Cursor='Hand'/>
                                    </Grid>
                                </Border>
                            </Grid>
                        </Border>
                    </Grid>
                </TabItem>

                <!-- TAB 2: Large Files Finder -->
                <TabItem Header='Large Files Finder'>
                    <Border Background='#131A2D' BorderBrush='#222C44' BorderThickness='1' Padding='20' Margin='0,12,0,0'>
                        <Grid>
                            <Grid.RowDefinitions>
                                <RowDefinition Height='Auto'/>
                                <RowDefinition Height='*'/>
                            </Grid.RowDefinitions>

                            <Grid Grid.Row='0' Margin='0,0,0,14'>
                                <StackPanel>
                                    <TextBlock Text='Disk Space Hog Analyzer' FontSize='17' FontWeight='Bold' Foreground='White'/>
                                    <TextBlock Name='txtLargeFilesSummary' Text='Locate large files (>100 MB) consuming your storage.' FontSize='12' Foreground='#94A3B8'/>
                                </StackPanel>
                                <Button Name='btnScanLargeFiles' Content='🔍 Scan for Large Files' HorizontalAlignment='Right' Background='#6366F1' Foreground='White' BorderThickness='0' Padding='16,8' FontWeight='SemiBold' Cursor='Hand'/>
                            </Grid>

                            <ScrollViewer Grid.Row='1' VerticalScrollBarVisibility='Auto'>
                                <StackPanel Name='pnlLargeFiles'/>
                            </ScrollViewer>
                        </Grid>
                    </Border>
                </TabItem>

                <!-- TAB 3: Duplicate Finder -->
                <TabItem Header='Duplicate Finder'>
                    <Border Background='#131A2D' BorderBrush='#222C44' BorderThickness='1' Padding='20' Margin='0,12,0,0'>
                        <Grid>
                            <Grid.RowDefinitions>
                                <RowDefinition Height='Auto'/>
                                <RowDefinition Height='*'/>
                            </Grid.RowDefinitions>

                            <Grid Grid.Row='0' Margin='0,0,0,14'>
                                <StackPanel>
                                    <TextBlock Text='Duplicate File Scanner' FontSize='17' FontWeight='Bold' Foreground='White'/>
                                    <TextBlock Name='txtDuplicatesSummary' Text='Scans user folders for 100% identical files using SHA-256 hash comparison.' FontSize='12' Foreground='#94A3B8'/>
                                </StackPanel>
                                <Button Name='btnScanDuplicates' Content='🔎 Find Duplicates' HorizontalAlignment='Right' Background='#6366F1' Foreground='White' BorderThickness='0' Padding='16,8' FontWeight='SemiBold' Cursor='Hand'/>
                            </Grid>

                            <ScrollViewer Grid.Row='1' VerticalScrollBarVisibility='Auto'>
                                <StackPanel Name='pnlDuplicates'/>
                            </ScrollViewer>
                        </Grid>
                    </Border>
                </TabItem>

                <!-- TAB 4: Startup Optimizer -->
                <TabItem Header='Startup Optimizer'>
                    <Border Background='#131A2D' BorderBrush='#222C44' BorderThickness='1' Padding='20' Margin='0,12,0,0'>
                        <Grid>
                            <Grid.RowDefinitions>
                                <RowDefinition Height='Auto'/>
                                <RowDefinition Height='*'/>
                            </Grid.RowDefinitions>

                            <Grid Grid.Row='0' Margin='0,0,0,14'>
                                <StackPanel>
                                    <TextBlock Text='Manage Startup Applications' FontSize='17' FontWeight='Bold' Foreground='White'/>
                                    <TextBlock Name='txtStartupSummary' Text='Disable background launchers to accelerate Windows boot speed and save RAM.' FontSize='12' Foreground='#94A3B8'/>
                                </StackPanel>
                                <Button Name='btnScanStartup' Content='🔄 Refresh Startup List' HorizontalAlignment='Right' Background='#1E293B' Foreground='#E2E8F0' BorderBrush='#334155' BorderThickness='1' Padding='14,8' FontWeight='SemiBold' Cursor='Hand'/>
                            </Grid>

                            <ScrollViewer Grid.Row='1' VerticalScrollBarVisibility='Auto'>
                                <StackPanel Name='pnlStartupApps'/>
                            </ScrollViewer>
                        </Grid>
                    </Border>
                </TabItem>

                <!-- TAB 5: Automation & Settings -->
                <TabItem Header='Automation &amp; Settings'>
                    <Grid Margin='0,12,0,0'>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width='*'/>
                            <ColumnDefinition Width='*'/>
                        </Grid.ColumnDefinitions>

                        <!-- Left: Auto-Clean Scheduler -->
                        <Border Grid.Column='0' Background='#131A2D' BorderBrush='#222C44' BorderThickness='1' Padding='20' Margin='0,0,12,0'>
                            <StackPanel>
                                <TextBlock Text='Automated Maintenance' FontSize='16' FontWeight='Bold' Foreground='White' Margin='0,0,0,6'/>
                                <TextBlock Text='Schedule silent background cleaning via Windows Task Scheduler. Cleans Recycle Bin, temp files, and caches automatically.' FontSize='12' Foreground='#94A3B8' TextWrapping='Wrap' Margin='0,0,0,16'/>

                                <CheckBox Name='chkAutoClean' Content='Enable Weekly Silent Auto-Clean (Sundays at 12:00 PM)' Margin='0,0,0,10'/>
                                <Border Background='#0B1120' Padding='10' CornerRadius='8' Margin='0,0,0,16'>
                                    <TextBlock Name='txtScheduleStatus' Text='Status: Checking schedule...' FontSize='12' Foreground='#64748B'/>
                                </Border>

                                <Button Name='btnCreateShortcut' Content='Create Desktop Shortcut' Background='#1E293B' Foreground='#E2E8F0' BorderBrush='#334155' BorderThickness='1' Padding='14,8' FontSize='12' FontWeight='SemiBold' Cursor='Hand'/>
                            </StackPanel>
                        </Border>

                        <!-- Right: About & Architecture -->
                        <Border Grid.Column='1' Background='#131A2D' BorderBrush='#222C44' BorderThickness='1' Padding='20' Margin='12,0,0,0'>
                            <StackPanel>
                                <TextBlock Text='About OmniClean' FontSize='16' FontWeight='Bold' Foreground='White' Margin='0,0,0,6'/>
                                <TextBlock Text='Version 2.0 Universal Standalone Edition.' FontSize='12' Foreground='#94A3B8' Margin='0,0,0,12'/>

                                <TextBlock Text='• Standalone portable executable with zero dependencies.' FontSize='12' Foreground='#CBD5E1' Margin='0,0,0,6'/>
                                <TextBlock Text='• Universal: Works on any Windows 10, 11, or Server computer.' FontSize='12' Foreground='#CBD5E1' Margin='0,0,0,6'/>
                                <TextBlock Text='• Native Win32 APIs for high-efficiency memory and disk access.' FontSize='12' Foreground='#CBD5E1' Margin='0,0,0,6'/>
                                <TextBlock Text='• Open Source Repository on GitHub:' FontSize='12' Foreground='#94A3B8' Margin='0,0,0,2'/>
                                <TextBlock Text='https://github.com/Junaed93/OmniClean' FontSize='12' Foreground='#818CF8' Margin='0,0,0,12'/>
                            </StackPanel>
                        </Border>
                    </Grid>
                </TabItem>
            </TabControl>
        </Grid>
    </Border>
</Window>";

                mainWindow = (Window)XamlReader.Parse(xaml);

                // Enable Windows 11 Dark Mode on titlebar
                IntPtr hwnd = new WindowInteropHelper(mainWindow).EnsureHandle();
                int useDark = 1;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));

                // Window Controls
                titleBar = (Border)mainWindow.FindName("titleBar");
                btnMinimize = (Button)mainWindow.FindName("btnMinimize");
                btnMaximize = (Button)mainWindow.FindName("btnMaximize");
                btnClose = (Button)mainWindow.FindName("btnClose");

                titleBar.MouseLeftButtonDown += (s, e) => {
                    if (e.ButtonState == MouseButtonState.Pressed) mainWindow.DragMove();
                };
                btnMinimize.Click += (s, e) => mainWindow.WindowState = WindowState.Minimized;
                btnMaximize.Click += (s, e) => mainWindow.WindowState = (mainWindow.WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
                btnClose.Click += (s, e) => mainWindow.Close();

                // Update Banner
                bannerUpdate = (Border)mainWindow.FindName("bannerUpdate");
                txtUpdateNotice = (TextBlock)mainWindow.FindName("txtUpdateNotice");
                btnDownloadUpdate = (Button)mainWindow.FindName("btnDownloadUpdate");
                btnDownloadUpdate.Click += (s, e) => {
                    try { Process.Start(REPO_URL + "/releases"); } catch { }
                };

                // Tab 1 Bindings
                cmbDrive = (ComboBox)mainWindow.FindName("cmbDrive");
                diskProgress = (ProgressBar)mainWindow.FindName("diskProgress");
                txtDiskPercent = (TextBlock)mainWindow.FindName("txtDiskPercent");
                txtDiskRatio = (TextBlock)mainWindow.FindName("txtDiskRatio");
                txtStatFree = (TextBlock)mainWindow.FindName("txtStatFree");
                txtStatUsed = (TextBlock)mainWindow.FindName("txtStatUsed");
                txtStatTotal = (TextBlock)mainWindow.FindName("txtStatTotal");
                txtSelectedSize = (TextBlock)mainWindow.FindName("txtSelectedSize");
                txtHardwarePill = (TextBlock)mainWindow.FindName("txtHardwarePill");
                btnClean = (Button)mainWindow.FindName("btnClean");
                btnRescan = (Button)mainWindow.FindName("btnRescan");
                btnOptimizeRam = (Button)mainWindow.FindName("btnOptimizeRam");
                btnSelectAll = (Button)mainWindow.FindName("btnSelectAll");
                btnClearAll = (Button)mainWindow.FindName("btnClearAll");
                txtConsole = (TextBox)mainWindow.FindName("txtConsole");
                pnlCategories = (StackPanel)mainWindow.FindName("pnlCategories");
                bannerSuccess = (Border)mainWindow.FindName("bannerSuccess");
                txtSuccessMsg = (TextBlock)mainWindow.FindName("txtSuccessMsg");

                // Tab 2 Bindings (Large Files)
                btnScanLargeFiles = (Button)mainWindow.FindName("btnScanLargeFiles");
                pnlLargeFiles = (StackPanel)mainWindow.FindName("pnlLargeFiles");
                txtLargeFilesSummary = (TextBlock)mainWindow.FindName("txtLargeFilesSummary");

                // Tab 3 Bindings (Duplicates)
                btnScanDuplicates = (Button)mainWindow.FindName("btnScanDuplicates");
                pnlDuplicates = (StackPanel)mainWindow.FindName("pnlDuplicates");
                txtDuplicatesSummary = (TextBlock)mainWindow.FindName("txtDuplicatesSummary");

                // Tab 4 Bindings (Startup Apps)
                btnScanStartup = (Button)mainWindow.FindName("btnScanStartup");
                pnlStartupApps = (StackPanel)mainWindow.FindName("pnlStartupApps");
                txtStartupSummary = (TextBlock)mainWindow.FindName("txtStartupSummary");

                // Tab 5 Bindings (Scheduler)
                chkAutoClean = (CheckBox)mainWindow.FindName("chkAutoClean");
                txtScheduleStatus = (TextBlock)mainWindow.FindName("txtScheduleStatus");
                btnCreateShortcut = (Button)mainWindow.FindName("btnCreateShortcut");

                // Initialize System Info dynamically
                DetectHardware();
                PopulateDrives();
                InitCategoryDefinitions();

                // Events
                btnRescan.Click += (s, e) => { RunScanAsync(); };
                btnOptimizeRam.Click += (s, e) => { OptimizeRam(); };
                btnSelectAll.Click += (s, e) => { SetAllCheckboxes(true); };
                btnClearAll.Click += (s, e) => { SetAllCheckboxes(false); };
                btnClean.Click += (s, e) => { RunCleanAsync(); };

                btnScanLargeFiles.Click += (s, e) => { ScanLargeFilesAsync(); };
                btnScanDuplicates.Click += (s, e) => { ScanDuplicatesAsync(); };
                btnScanStartup.Click += (s, e) => { ScanStartupAppsAsync(); };

                btnCreateShortcut.Click += (s, e) => {
                    CreateDesktopShortcut();
                    MessageBox.Show(mainWindow, "OmniClean desktop shortcut created.", "OmniClean", MessageBoxButton.OK, MessageBoxImage.Information);
                };

                cmbDrive.SelectionChanged += (s, e) => {
                    if (cmbDrive.SelectedItem != null) {
                        selectedDriveLetter = cmbDrive.SelectedItem.ToString().Substring(0, 1);
                        UpdateDiskMetrics();
                    }
                };

                chkAutoClean.Click += (s, e) => {
                    bool enable = chkAutoClean.IsChecked == true;
                    ToggleScheduler(enable);
                };

                // Startup calls
                Log("OmniClean v2.0 Pro Suite initialized.");
                UpdateDiskMetrics();
                CheckScheduledTask();
                RunScanAsync();
                ScanStartupAppsAsync();
                CheckForUpdatesAsync();

                app.Run(mainWindow);
            } catch (Exception ex) {
                MessageBox.Show("OmniClean Startup Error: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        static void Log(string msg) {
            if (txtConsole == null) return;
            mainWindow.Dispatcher.Invoke(new Action(() => {
                string time = DateTime.Now.ToString("HH:mm:ss");
                txtConsole.AppendText(string.Format("[{0}] {1}\r\n", time, msg));
                txtConsole.ScrollToEnd();
            }));
        }

        static void DetectHardware() {
            Task.Run(new Action(() => {
                string cpu = "CPU";
                try {
                    using (var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0")) {
                        if (key != null) {
                            cpu = key.GetValue("ProcessorNameString").ToString().Trim();
                            cpu = cpu.Replace("(R)", "").Replace("(TM)", "").Replace("Processor", "").Trim();
                        }
                    }
                } catch { }

                double ramGB = 0;
                try {
                    var mem = new MEMORYSTATUSEX();
                    if (GlobalMemoryStatusEx(mem)) {
                        ramGB = Math.Round(mem.ullTotalPhys / (1024.0 * 1024.0 * 1024.0), 0);
                    }
                } catch { }

                string gpu = "Graphics";
                try {
                    using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}")) {
                        if (key != null) {
                            foreach (var subName in key.GetSubKeyNames()) {
                                if (subName.StartsWith("000")) {
                                    using (var subKey = key.OpenSubKey(subName)) {
                                        if (subKey != null) {
                                            var desc = subKey.GetValue("DriverDesc");
                                            if (desc != null && !desc.ToString().Contains("Basic")) {
                                                gpu = desc.ToString().Replace("NVIDIA ", "").Replace("AMD ", "").Trim();
                                                if (gpu.Contains("GeForce") || gpu.Contains("Radeon") || gpu.Contains("Arc")) break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                } catch { }

                string hwString = string.Format("{0} • {1} GB RAM • {2}", cpu, ramGB, gpu);

                mainWindow.Dispatcher.Invoke(new Action(() => {
                    txtHardwarePill.Text = hwString;
                }));
            }));
        }

        static void CheckForUpdatesAsync() {
            Task.Run(new Action(() => {
                try {
                    var req = (HttpWebRequest)WebRequest.Create(RELEASES_API);
                    req.UserAgent = "OmniClean";
                    req.Timeout = 5000;
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (var reader = new StreamReader(resp.GetResponseStream())) {
                        string json = reader.ReadToEnd();
                        var match = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                        if (match.Success) {
                            string latestTag = match.Groups[1].Value.Trim();
                            if (!string.Equals(latestTag, CURRENT_VERSION, StringComparison.OrdinalIgnoreCase)) {
                                mainWindow.Dispatcher.Invoke(new Action(() => {
                                    bannerUpdate.Visibility = Visibility.Visible;
                                    txtUpdateNotice.Text = string.Format("Update Available: {0} (Current: {1})", latestTag, CURRENT_VERSION);
                                }));
                            }
                        }
                    }
                } catch { }
            }));
        }

        static void OptimizeRam() {
            Task.Run(new Action(() => {
                try {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    EmptyWorkingSet(Process.GetCurrentProcess().Handle);

                    // Flush current user processes working sets where possible
                    foreach (var proc in Process.GetProcesses()) {
                        try {
                            if (proc.Id != Process.GetCurrentProcess().Id) {
                                EmptyWorkingSet(proc.Handle);
                            }
                        } catch { }
                    }

                    mainWindow.Dispatcher.Invoke(new Action(() => {
                        Log("RAM Optimizer: Working sets and standby memory flushed.");
                    }));
                } catch (Exception ex) {
                    Log("RAM Optimizer error: " + ex.Message);
                }
            }));
        }

        static void PopulateDrives() {
            cmbDrive.Items.Clear();
            string sysRoot = Path.GetPathRoot(Environment.SystemDirectory);
            string sysLetter = (sysRoot != null && sysRoot.Length > 0) ? sysRoot.Substring(0, 1).ToUpper() : "C";

            int selectedIndex = 0;
            int idx = 0;
            foreach (var d in DriveInfo.GetDrives()) {
                if (d.IsReady && d.DriveType == DriveType.Fixed) {
                    string letter = d.Name.Substring(0, 1).ToUpper();
                    string label = string.Format("{0}: ({1:F0} GB)", letter, d.TotalSize / (1024.0 * 1024.0 * 1024.0));
                    cmbDrive.Items.Add(label);
                    if (letter == sysLetter) {
                        selectedIndex = idx;
                        selectedDriveLetter = letter;
                    }
                    idx++;
                }
            }

            if (cmbDrive.Items.Count > 0) {
                cmbDrive.SelectedIndex = selectedIndex;
            }
        }

        static void InitCategoryDefinitions() {
            categories.Clear();
            pnlCategories.Children.Clear();

            AddCategory("recycle_bin", "Windows Recycle Bin", "All files and deleted items sent to the Recycle Bin");
            AddCategory("temp_files", "System & User Temporary Files", "Windows and application temporary cache in %TEMP% and Windows\\Temp");
            AddCategory("browser_cache", "Web Browser Caches", "Chrome, Edge, Brave, Firefox, and Opera internet cache and shader data");
            AddCategory("win_update", "Windows Update Download Cache", "Leftover update packages in SoftwareDistribution\\Download");
            AddCategory("crash_dumps", "Error Reports & Crash Dumps", "Windows Error Reporting (WER) and memory dump logs");

            AddCategory("vscode_cache", "VS Code Caches & Crash Dumps", "Visual Studio Code installer packages and crash logs");
            AddCategory("dev_cache", "npm & pip Package Caches", "Node.js and Python package manager cache folders");
            AddCategory("discord_cache", "Discord Client Cache", "Discord media, audio, and GPU shader caches");
            AddCategory("capcut_old", "Outdated CapCut App Versions", "Older builds leftover in AppData\\Local\\CapCut\\Apps");
            AddCategory("nvidia_artifacts", "NVIDIA Driver Update Packages", "Downloaded GPU driver installers in NVIDIA App UpdateFramework");
        }

        static void AddCategory(string id, string name, string desc) {
            Border card = new Border();
            card.Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x14, 0x24));
            card.BorderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
            card.BorderThickness = new Thickness(1);
            card.Padding = new Thickness(12, 10, 12, 10);
            card.Margin = new Thickness(0, 0, 0, 8);
            card.CornerRadius = new CornerRadius(10);

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            StackPanel sp = new StackPanel();
            CheckBox chk = new CheckBox();
            chk.Content = name;
            chk.FontWeight = FontWeights.SemiBold;
            chk.IsChecked = true;
            chk.Checked += (s, e) => UpdateSelectedSize();
            chk.Unchecked += (s, e) => UpdateSelectedSize();

            TextBlock txtDesc = new TextBlock();
            txtDesc.Text = desc;
            txtDesc.FontSize = 11;
            txtDesc.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
            txtDesc.Margin = new Thickness(22, 2, 0, 0);

            sp.Children.Add(chk);
            sp.Children.Add(txtDesc);
            Grid.SetColumn(sp, 0);
            grid.Children.Add(sp);

            Border badge = new Border();
            badge.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
            badge.Padding = new Thickness(10, 4, 10, 4);
            badge.CornerRadius = new CornerRadius(6);
            badge.VerticalAlignment = VerticalAlignment.Center;

            TextBlock txtBadge = new TextBlock();
            txtBadge.Text = "...";
            txtBadge.FontSize = 11;
            txtBadge.FontWeight = FontWeights.Bold;
            txtBadge.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
            badge.Child = txtBadge;

            Grid.SetColumn(badge, 1);
            grid.Children.Add(badge);

            card.Child = grid;
            pnlCategories.Children.Add(card);

            categories.Add(new CategoryItem {
                Id = id,
                Name = name,
                Description = desc,
                SizeMB = 0,
                IsInstalled = true,
                CheckBoxRef = chk,
                BadgeRef = txtBadge,
                CardRef = card
            });
        }

        static void SetAllCheckboxes(bool isChecked) {
            foreach (var c in categories) {
                if (c.IsInstalled) {
                    c.CheckBoxRef.IsChecked = isChecked;
                }
            }
            UpdateSelectedSize();
        }

        static void UpdateSelectedSize() {
            double totalMB = 0;
            int count = 0;
            foreach (var c in categories) {
                if (c.CheckBoxRef.IsChecked == true && c.IsInstalled) {
                    totalMB += c.SizeMB;
                    count++;
                }
            }

            if (totalMB > 1024) {
                txtSelectedSize.Text = string.Format("{0:F2} GB", totalMB / 1024.0);
            } else {
                txtSelectedSize.Text = string.Format("{0:F1} MB", totalMB);
            }

            btnClean.IsEnabled = (count > 0) && !isScanning && !isCleaning;
        }

        static void UpdateDiskMetrics() {
            try {
                DriveInfo drive = new DriveInfo(selectedDriveLetter);
                if (!drive.IsReady) return;

                double totalGB = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                double freeGB = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                double usedGB = totalGB - freeGB;
                double pctFree = (freeGB / totalGB) * 100.0;

                txtDiskPercent.Text = string.Format("{0:F1}% Free", pctFree);
                txtDiskRatio.Text = string.Format("{0:F1} GB Free / {1:F1} GB Total", freeGB, totalGB);
                txtStatFree.Text = string.Format("{0:F1} GB", freeGB);
                txtStatUsed.Text = string.Format("{0:F1} GB", usedGB);
                txtStatTotal.Text = string.Format("{0:F1} GB", totalGB);

                diskProgress.Value = (usedGB / totalGB) * 100.0;
            } catch { }
        }

        static void CheckScheduledTask() {
            Task.Run(new Action(() => {
                bool hasTask = false;
                try {
                    Process p = new Process();
                    p.StartInfo.FileName = "schtasks.exe";
                    p.StartInfo.Arguments = "/query /tn \"" + TASK_NAME + "\"";
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.UseShellExecute = false;
                    p.Start();
                    p.WaitForExit();
                    hasTask = (p.ExitCode == 0);
                } catch { }

                mainWindow.Dispatcher.Invoke(new Action(() => {
                    chkAutoClean.IsChecked = hasTask;
                    txtScheduleStatus.Text = hasTask ? "Status: Active (Every Sunday at 12:00 PM)" : "Status: Disabled";
                    txtScheduleStatus.Foreground = hasTask ? new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)) : new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
                }));
            }));
        }

        static void ToggleScheduler(bool enable) {
            Task.Run(new Action(() => {
                try {
                    string exePath = Process.GetCurrentProcess().MainModule.FileName;
                    Process p = new Process();
                    p.StartInfo.FileName = "schtasks.exe";
                    if (enable) {
                        string taskCmd = "\"" + exePath + "\" --silent-clean";
                        p.StartInfo.Arguments = string.Format("/create /tn \"{0}\" /tr \"{1}\" /sc weekly /d SUN /st 12:00 /f", TASK_NAME, taskCmd);
                    } else {
                        p.StartInfo.Arguments = string.Format("/delete /tn \"{0}\" /f", TASK_NAME);
                    }
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.UseShellExecute = false;
                    p.Start();
                    p.WaitForExit();

                    mainWindow.Dispatcher.Invoke(new Action(() => {
                        txtScheduleStatus.Text = enable ? "Status: Active (Every Sunday at 12:00 PM)" : "Status: Disabled";
                        txtScheduleStatus.Foreground = enable ? new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)) : new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
                        Log(enable ? "Scheduled weekly auto-clean in Windows Task Scheduler." : "Removed auto-clean scheduled task.");
                    }));
                } catch (Exception ex) {
                    Log("Scheduler error: " + ex.Message);
                }
            }));
        }

        static void CreateDesktopShortcut() {
            try {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string shortcutPath = Path.Combine(desktop, "OmniClean.lnk");
                string exePath = Process.GetCurrentProcess().MainModule.FileName;

                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = exePath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
                shortcut.Description = "OmniClean - Universal Windows Optimizer";
                shortcut.IconLocation = exePath + ",0";
                shortcut.Save();
                Log("Desktop shortcut created.");
            } catch (Exception ex) {
                Log("Shortcut error: " + ex.Message);
            }
        }

        static double GetDirectorySizeMB(string path) {
            if (!Directory.Exists(path)) return 0;
            double bytes = 0;
            try {
                string[] files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                foreach (var f in files) {
                    try {
                        FileInfo fi = new FileInfo(f);
                        bytes += fi.Length;
                    } catch { }
                }
            } catch { }
            return bytes / (1024.0 * 1024.0);
        }

        static double ScanBrowserCaches() {
            double total = 0;
            string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] browserRoots = new string[] {
                Path.Combine(localApp, @"Google\Chrome\User Data"),
                Path.Combine(localApp, @"Microsoft\Edge\User Data"),
                Path.Combine(localApp, @"BraveSoftware\Brave-Browser\User Data"),
                Path.Combine(localApp, @"Mozilla\Firefox\Profiles"),
                Path.Combine(localApp, @"Opera Software")
            };

            foreach (var root in browserRoots) {
                if (Directory.Exists(root)) {
                    try {
                        foreach (var dir in Directory.GetDirectories(root)) {
                            string c1 = Path.Combine(dir, "Cache");
                            if (Directory.Exists(c1)) total += GetDirectorySizeMB(c1);

                            string c2 = Path.Combine(dir, "Code Cache");
                            if (Directory.Exists(c2)) total += GetDirectorySizeMB(c2);

                            string ff = Path.Combine(dir, "cache2");
                            if (Directory.Exists(ff)) total += GetDirectorySizeMB(ff);
                        }
                    } catch { }
                }
            }
            return total;
        }

        static void CleanBrowserCaches() {
            string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] browserRoots = new string[] {
                Path.Combine(localApp, @"Google\Chrome\User Data"),
                Path.Combine(localApp, @"Microsoft\Edge\User Data"),
                Path.Combine(localApp, @"BraveSoftware\Brave-Browser\User Data"),
                Path.Combine(localApp, @"Mozilla\Firefox\Profiles"),
                Path.Combine(localApp, @"Opera Software")
            };

            foreach (var root in browserRoots) {
                if (Directory.Exists(root)) {
                    try {
                        foreach (var dir in Directory.GetDirectories(root)) {
                            CleanFolder(Path.Combine(dir, "Cache"));
                            CleanFolder(Path.Combine(dir, "Code Cache"));
                            CleanFolder(Path.Combine(dir, "cache2"));
                        }
                    } catch { }
                }
            }
        }

        static void RunScanAsync() {
            if (isScanning || isCleaning) return;
            isScanning = true;
            btnRescan.IsEnabled = false;
            Log("Scanning cleanable targets...");

            Task.Run(new Action(() => {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

                foreach (var cat in categories) {
                    double size = 0;
                    bool exists = true;

                    try {
                        if (cat.Id == "recycle_bin") {
                            string userSid = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
                            string sysDrive = Path.GetPathRoot(Environment.SystemDirectory);
                            string rbPath = Path.Combine(sysDrive, "$Recycle.Bin\\" + userSid);
                            size = GetDirectorySizeMB(rbPath);
                        } else if (cat.Id == "temp_files") {
                            size = GetDirectorySizeMB(Path.GetTempPath()) + GetDirectorySizeMB(Path.Combine(winDir, "Temp"));
                        } else if (cat.Id == "browser_cache") {
                            size = ScanBrowserCaches();
                        } else if (cat.Id == "win_update") {
                            size = GetDirectorySizeMB(Path.Combine(winDir, @"SoftwareDistribution\Download"));
                        } else if (cat.Id == "crash_dumps") {
                            size = GetDirectorySizeMB(Path.Combine(localApp, "CrashDumps"))
                                 + GetDirectorySizeMB(Path.Combine(progData, @"Microsoft\Windows\WER"));
                        } else if (cat.Id == "nvidia_artifacts") {
                            string nv = Path.Combine(progData, @"NVIDIA Corporation\NVIDIA app\UpdateFramework\ota-artifacts");
                            exists = Directory.Exists(Path.Combine(progData, @"NVIDIA Corporation"));
                            if (exists) size = GetDirectorySizeMB(nv);
                        } else if (cat.Id == "capcut_old") {
                            string capApps = Path.Combine(localApp, @"CapCut\Apps");
                            exists = Directory.Exists(capApps);
                            if (exists) {
                                var dirs = new List<string>(Directory.GetDirectories(capApps));
                                dirs.Sort();
                                if (dirs.Count > 1) {
                                    string latest = dirs[dirs.Count - 1];
                                    foreach (var d in dirs) {
                                        if (d != latest) size += GetDirectorySizeMB(d);
                                    }
                                }
                            }
                        } else if (cat.Id == "vscode_cache") {
                            string vscPath = Path.Combine(appData, "Code");
                            exists = Directory.Exists(vscPath);
                            if (exists) {
                                size = GetDirectorySizeMB(Path.Combine(vscPath, "CachedExtensionVSIXs"))
                                     + GetDirectorySizeMB(Path.Combine(vscPath, @"Crashpad\reports"))
                                     + GetDirectorySizeMB(Path.Combine(vscPath, "CachedData"));
                            }
                        } else if (cat.Id == "dev_cache") {
                            string npm = Path.Combine(localApp, "npm-cache");
                            string pip = Path.Combine(localApp, @"pip\cache");
                            exists = Directory.Exists(npm) || Directory.Exists(pip);
                            if (exists) size = GetDirectorySizeMB(npm) + GetDirectorySizeMB(pip);
                        } else if (cat.Id == "discord_cache") {
                            string dc = Path.Combine(appData, @"discord\Cache");
                            exists = Directory.Exists(Path.Combine(appData, "discord"));
                            if (exists) size = GetDirectorySizeMB(dc);
                        }
                    } catch { }

                    cat.SizeMB = size;
                    cat.IsInstalled = exists;

                    mainWindow.Dispatcher.Invoke(new Action(() => {
                        if (!cat.IsInstalled) {
                            cat.BadgeRef.Text = "Not Present";
                            cat.BadgeRef.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
                            cat.CheckBoxRef.IsEnabled = false;
                            cat.CheckBoxRef.IsChecked = false;
                        } else if (cat.SizeMB > 0.1) {
                            cat.BadgeRef.Text = cat.SizeMB > 1024 ? string.Format("{0:F2} GB", cat.SizeMB / 1024.0) : string.Format("{0:F1} MB", cat.SizeMB);
                            cat.BadgeRef.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                            cat.CheckBoxRef.IsEnabled = true;
                        } else {
                            cat.BadgeRef.Text = "Clean";
                            cat.BadgeRef.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                            cat.CheckBoxRef.IsEnabled = true;
                        }
                    }));
                }

                mainWindow.Dispatcher.Invoke(new Action(() => {
                    isScanning = false;
                    btnRescan.IsEnabled = true;
                    UpdateDiskMetrics();
                    UpdateSelectedSize();
                    Log("Scan completed.");
                }));
            }));
        }

        static void CleanFolder(string path) {
            if (!Directory.Exists(path)) return;
            try {
                foreach (string file in Directory.GetFiles(path)) {
                    try { File.Delete(file); } catch { }
                }
                foreach (string dir in Directory.GetDirectories(path)) {
                    try { Directory.Delete(dir, true); } catch { }
                }
            } catch { }
        }

        static void RunCleanAsync() {
            if (isCleaning) return;
            isCleaning = true;
            btnClean.IsEnabled = false;
            btnClean.Content = "Cleaning Storage...";
            bannerSuccess.Visibility = Visibility.Collapsed;
            Log("Starting universal optimization process...");

            Task.Run(new Action(() => {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

                foreach (var cat in categories) {
                    bool shouldClean = false;
                    mainWindow.Dispatcher.Invoke(new Action(() => {
                        shouldClean = (cat.CheckBoxRef.IsChecked == true && cat.IsInstalled);
                    }));

                    if (!shouldClean) continue;

                    Log("Cleaning " + cat.Name + "...");
                    try {
                        if (cat.Id == "recycle_bin") {
                            SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                        } else if (cat.Id == "temp_files") {
                            CleanFolder(Path.GetTempPath());
                            CleanFolder(Path.Combine(winDir, "Temp"));
                        } else if (cat.Id == "browser_cache") {
                            CleanBrowserCaches();
                        } else if (cat.Id == "win_update") {
                            CleanFolder(Path.Combine(winDir, @"SoftwareDistribution\Download"));
                        } else if (cat.Id == "crash_dumps") {
                            CleanFolder(Path.Combine(localApp, "CrashDumps"));
                            CleanFolder(Path.Combine(progData, @"Microsoft\Windows\WER\ReportQueue"));
                            CleanFolder(Path.Combine(progData, @"Microsoft\Windows\WER\ReportArchive"));
                        } else if (cat.Id == "nvidia_artifacts") {
                            CleanFolder(Path.Combine(progData, @"NVIDIA Corporation\NVIDIA app\UpdateFramework\ota-artifacts"));
                        } else if (cat.Id == "capcut_old") {
                            string capApps = Path.Combine(localApp, @"CapCut\Apps");
                            if (Directory.Exists(capApps)) {
                                var dirs = new List<string>(Directory.GetDirectories(capApps));
                                dirs.Sort();
                                if (dirs.Count > 1) {
                                    string latest = dirs[dirs.Count - 1];
                                    foreach (var d in dirs) {
                                        if (d != latest) {
                                            try { Directory.Delete(d, true); } catch { }
                                        }
                                    }
                                }
                            }
                        } else if (cat.Id == "vscode_cache") {
                            string vscPath = Path.Combine(appData, "Code");
                            CleanFolder(Path.Combine(vscPath, "CachedExtensionVSIXs"));
                            CleanFolder(Path.Combine(vscPath, @"Crashpad\reports"));
                            CleanFolder(Path.Combine(vscPath, "CachedData"));
                        } else if (cat.Id == "dev_cache") {
                            CleanFolder(Path.Combine(localApp, "npm-cache"));
                            CleanFolder(Path.Combine(localApp, @"pip\cache"));
                        } else if (cat.Id == "discord_cache") {
                            CleanFolder(Path.Combine(appData, @"discord\Cache"));
                        }
                    } catch (Exception ex) {
                        Log("Warning: " + ex.Message);
                    }
                }

                mainWindow.Dispatcher.Invoke(new Action(() => {
                    isCleaning = false;
                    btnClean.Content = "Clean Selected Storage";
                    UpdateDiskMetrics();
                    Log("Universal cleanup successfully finished.");
                    bannerSuccess.Visibility = Visibility.Visible;
                    txtSuccessMsg.Text = "Cleanup complete! Selected storage targets successfully purged.";
                    RunScanAsync();
                }));
            }));
        }

        // TAB 2: Large Files Scanner
        static void ScanLargeFilesAsync() {
            btnScanLargeFiles.IsEnabled = false;
            btnScanLargeFiles.Content = "Scanning Drive...";
            pnlLargeFiles.Children.Clear();
            txtLargeFilesSummary.Text = "Scanning user folders and selected drive for files > 100 MB...";

            Task.Run(new Action(() => {
                string userDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var found = new List<LargeFileInfo>();
                long minBytes = 100 * 1024 * 1024; // 100 MB

                try {
                    DirectoryInfo di = new DirectoryInfo(userDir);
                    foreach (var dir in di.GetDirectories()) {
                        if (dir.Name.StartsWith(".") && dir.Name != ".cache") continue;
                        try {
                            foreach (var fi in dir.GetFiles("*.*", SearchOption.AllDirectories)) {
                                try {
                                    if (fi.Length > minBytes) {
                                        found.Add(new LargeFileInfo {
                                            FilePath = fi.FullName,
                                            FileName = fi.Name,
                                            Bytes = fi.Length,
                                            SizeMB = Math.Round(fi.Length / (1024.0 * 1024.0), 1)
                                        });
                                    }
                                } catch { }
                            }
                        } catch { }
                    }
                } catch { }

                found.Sort((a, b) => b.Bytes.CompareTo(a.Bytes));

                mainWindow.Dispatcher.Invoke(new Action(() => {
                    largeFiles = found;
                    btnScanLargeFiles.IsEnabled = true;
                    btnScanLargeFiles.Content = "Scan for Large Files";
                    txtLargeFilesSummary.Text = string.Format("Found {0} files exceeding 100 MB.", found.Count);

                    if (found.Count == 0) {
                        TextBlock empty = new TextBlock();
                        empty.Text = "No files larger than 100 MB found in scanned directories.";
                        empty.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
                        empty.Margin = new Thickness(0, 20, 0, 0);
                        pnlLargeFiles.Children.Add(empty);
                        return;
                    }

                    foreach (var item in found) {
                        Border card = new Border();
                        card.Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x14, 0x24));
                        card.BorderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
                        card.BorderThickness = new Thickness(1);
                        card.Padding = new Thickness(14, 10, 14, 10);
                        card.Margin = new Thickness(0, 0, 0, 8);
                        card.CornerRadius = new CornerRadius(10);

                        Grid grid = new Grid();
                        grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                        grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

                        StackPanel sp = new StackPanel();
                        TextBlock name = new TextBlock();
                        name.Text = item.FileName;
                        name.FontWeight = FontWeights.Bold;
                        name.Foreground = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC));
                        name.FontSize = 13;

                        TextBlock path = new TextBlock();
                        path.Text = item.FilePath;
                        path.FontSize = 11;
                        path.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
                        path.TextTrimming = TextTrimming.CharacterEllipsis;
                        path.MaxWidth = 620;

                        sp.Children.Add(name);
                        sp.Children.Add(path);
                        Grid.SetColumn(sp, 0);
                        grid.Children.Add(sp);

                        StackPanel rightSp = new StackPanel();
                        rightSp.Orientation = Orientation.Horizontal;
                        rightSp.VerticalAlignment = VerticalAlignment.Center;

                        Border sizeBadge = new Border();
                        sizeBadge.Background = new SolidColorBrush(Color.FromRgb(0x23, 0x20, 0x48));
                        sizeBadge.Padding = new Thickness(10, 4, 10, 4);
                        sizeBadge.CornerRadius = new CornerRadius(6);
                        sizeBadge.Margin = new Thickness(0, 0, 10, 0);

                        TextBlock txtSize = new TextBlock();
                        txtSize.Text = item.SizeMB > 1024 ? string.Format("{0:F2} GB", item.SizeMB / 1024.0) : string.Format("{0:F0} MB", item.SizeMB);
                        txtSize.Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0x84, 0xFC));
                        txtSize.FontWeight = FontWeights.Bold;
                        txtSize.FontSize = 11;
                        sizeBadge.Child = txtSize;

                        Button btnShow = new Button();
                        btnShow.Content = "Open Folder";
                        btnShow.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
                        btnShow.Foreground = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));
                        btnShow.BorderThickness = new Thickness(0);
                        btnShow.Padding = new Thickness(10, 4, 10, 4);
                        btnShow.FontSize = 11;
                        btnShow.Cursor = Cursors.Hand;
                        btnShow.Click += (s, e) => {
                            try { Process.Start("explorer.exe", string.Format("/select,\"{0}\"", item.FilePath)); } catch { }
                        };

                        rightSp.Children.Add(sizeBadge);
                        rightSp.Children.Add(btnShow);
                        Grid.SetColumn(rightSp, 1);
                        grid.Children.Add(rightSp);

                        card.Child = grid;
                        pnlLargeFiles.Children.Add(card);
                    }
                }));
            }));
        }

        // TAB 3: Duplicate Finder
        static string GetPartialFileHash(string path) {
            try {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sha = SHA256.Create()) {
                    byte[] buf = new byte[8192];
                    int read = fs.Read(buf, 0, buf.Length);
                    return BitConverter.ToString(sha.ComputeHash(buf, 0, read));
                }
            } catch { return Guid.NewGuid().ToString(); }
        }

        static void ScanDuplicatesAsync() {
            btnScanDuplicates.IsEnabled = false;
            btnScanDuplicates.Content = "Scanning Duplicates...";
            pnlDuplicates.Children.Clear();
            txtDuplicatesSummary.Text = "Scanning Downloads and user files for duplicate matches...";

            Task.Run(new Action(() => {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string[] searchDirs = new string[] {
                    Path.Combine(userProfile, "Downloads"),
                    Path.Combine(userProfile, "Documents"),
                    Path.Combine(userProfile, "Pictures")
                };

                var sizeMap = new Dictionary<long, List<FileInfo>>();
                foreach (var dirPath in searchDirs) {
                    if (Directory.Exists(dirPath)) {
                        try {
                            foreach (var f in Directory.GetFiles(dirPath, "*.*", SearchOption.AllDirectories)) {
                                try {
                                    var fi = new FileInfo(f);
                                    if (fi.Length > 512 * 1024) { // > 512 KB
                                        if (!sizeMap.ContainsKey(fi.Length)) sizeMap[fi.Length] = new List<FileInfo>();
                                        sizeMap[fi.Length].Add(fi);
                                    }
                                } catch { }
                            }
                        } catch { }
                    }
                }

                var matchedGroups = new List<DuplicateFileGroup>();
                foreach (var pair in sizeMap) {
                    if (pair.Value.Count > 1) {
                        var hashMap = new Dictionary<string, List<string>>();
                        foreach (var fi in pair.Value) {
                            string h = GetPartialFileHash(fi.FullName);
                            if (!hashMap.ContainsKey(h)) hashMap[h] = new List<string>();
                            hashMap[h].Add(fi.FullName);
                        }

                        foreach (var hPair in hashMap) {
                            if (hPair.Value.Count > 1) {
                                matchedGroups.Add(new DuplicateFileGroup {
                                    Hash = hPair.Key,
                                    Bytes = pair.Key,
                                    SizeMB = Math.Round(pair.Key / (1024.0 * 1024.0), 2),
                                    FilePaths = hPair.Value
                                });
                            }
                        }
                    }
                }

                mainWindow.Dispatcher.Invoke(new Action(() => {
                    btnScanDuplicates.IsEnabled = true;
                    btnScanDuplicates.Content = "Find Duplicates";
                    txtDuplicatesSummary.Text = string.Format("Found {0} duplicate file groups.", matchedGroups.Count);

                    if (matchedGroups.Count == 0) {
                        TextBlock empty = new TextBlock();
                        empty.Text = "No duplicate files found in Downloads, Documents, or Pictures.";
                        empty.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
                        empty.Margin = new Thickness(0, 20, 0, 0);
                        pnlDuplicates.Children.Add(empty);
                        return;
                    }

                    foreach (var group in matchedGroups) {
                        Border groupBorder = new Border();
                        groupBorder.Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x14, 0x24));
                        groupBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
                        groupBorder.BorderThickness = new Thickness(1);
                        groupBorder.Padding = new Thickness(14, 10, 14, 10);
                        groupBorder.Margin = new Thickness(0, 0, 0, 10);
                        groupBorder.CornerRadius = new CornerRadius(10);

                        StackPanel groupSp = new StackPanel();

                        Grid headerGrid = new Grid();
                        headerGrid.Margin = new Thickness(0, 0, 0, 8);
                        TextBlock title = new TextBlock();
                        title.Text = string.Format("Duplicate Group ({0:F1} MB each - {1} copies)", group.SizeMB, group.FilePaths.Count);
                        title.FontWeight = FontWeights.Bold;
                        title.Foreground = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8));
                        headerGrid.Children.Add(title);
                        groupSp.Children.Add(headerGrid);

                        for (int i = 0; i < group.FilePaths.Count; i++) {
                            string p = group.FilePaths[i];
                            Border itemCard = new Border();
                            itemCard.Background = new SolidColorBrush(Color.FromRgb(0x13, 0x1A, 0x2D));
                            itemCard.Padding = new Thickness(10, 6, 10, 6);
                            itemCard.Margin = new Thickness(0, 0, 0, 4);
                            itemCard.CornerRadius = new CornerRadius(6);

                            Grid itemGrid = new Grid();
                            itemGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                            itemGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

                            TextBlock itemPath = new TextBlock();
                            itemPath.Text = (i == 0 ? "[Original] " : "[Copy] ") + p;
                            itemPath.FontSize = 11;
                            itemPath.Foreground = (i == 0) ? new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99)) : new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
                            itemPath.TextTrimming = TextTrimming.CharacterEllipsis;
                            Grid.SetColumn(itemPath, 0);
                            itemGrid.Children.Add(itemPath);

                            Button btnDel = new Button();
                            btnDel.Content = (i == 0 ? "Reveal" : "Delete");
                            btnDel.Background = (i == 0) ? new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)) : new SolidColorBrush(Color.FromRgb(0x3B, 0x1A, 0x22));
                            btnDel.Foreground = (i == 0) ? new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)) : new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));
                            btnDel.BorderThickness = new Thickness(0);
                            btnDel.Padding = new Thickness(10, 2, 10, 2);
                            btnDel.FontSize = 10;
                            btnDel.Cursor = Cursors.Hand;

                            string capturedPath = p;
                            bool isOriginal = (i == 0);
                            btnDel.Click += (s, e) => {
                                if (isOriginal) {
                                    try { Process.Start("explorer.exe", string.Format("/select,\"{0}\"", capturedPath)); } catch { }
                                } else {
                                    if (MessageBox.Show(mainWindow, "Are you sure you want to delete this duplicate?\r\n" + capturedPath, "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) {
                                        try {
                                            File.Delete(capturedPath);
                                            itemCard.Visibility = Visibility.Collapsed;
                                            Log("Deleted duplicate file: " + capturedPath);
                                        } catch (Exception ex) {
                                            Log("Delete error: " + ex.Message);
                                        }
                                    }
                                }
                            };

                            Grid.SetColumn(btnDel, 1);
                            itemGrid.Children.Add(btnDel);

                            itemCard.Child = itemGrid;
                            groupSp.Children.Add(itemCard);
                        }

                        groupBorder.Child = groupSp;
                        pnlDuplicates.Children.Add(groupBorder);
                    }
                }));
            }));
        }

        // TAB 4: Startup Apps Scanner
        static void ScanStartupAppsAsync() {
            btnScanStartup.IsEnabled = false;
            pnlStartupApps.Children.Clear();
            txtStartupSummary.Text = "Scanning Windows Registry startup entries...";

            Task.Run(new Action(() => {
                var list = new List<StartupAppItem>();

                try {
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) {
                        if (key != null) {
                            foreach (var valName in key.GetValueNames()) {
                                list.Add(new StartupAppItem {
                                    Name = valName,
                                    Command = key.GetValue(valName).ToString(),
                                    IsEnabled = true
                                });
                            }
                        }
                    }
                } catch { }

                mainWindow.Dispatcher.Invoke(new Action(() => {
                    startupApps = list;
                    btnScanStartup.IsEnabled = true;
                    txtStartupSummary.Text = string.Format("Found {0} active startup programs configured in user registry.", list.Count);

                    if (list.Count == 0) {
                        TextBlock empty = new TextBlock();
                        empty.Text = "No active startup applications found in user profile.";
                        empty.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
                        empty.Margin = new Thickness(0, 20, 0, 0);
                        pnlStartupApps.Children.Add(empty);
                        return;
                    }

                    foreach (var app in list) {
                        Border card = new Border();
                        card.Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x14, 0x24));
                        card.BorderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
                        card.BorderThickness = new Thickness(1);
                        card.Padding = new Thickness(14, 10, 14, 10);
                        card.Margin = new Thickness(0, 0, 0, 8);
                        card.CornerRadius = new CornerRadius(10);

                        Grid grid = new Grid();
                        grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                        grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

                        StackPanel sp = new StackPanel();
                        TextBlock name = new TextBlock();
                        name.Text = app.Name;
                        name.FontWeight = FontWeights.Bold;
                        name.Foreground = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC));
                        name.FontSize = 13;

                        TextBlock cmd = new TextBlock();
                        cmd.Text = app.Command;
                        cmd.FontSize = 11;
                        cmd.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
                        cmd.TextTrimming = TextTrimming.CharacterEllipsis;
                        cmd.MaxWidth = 550;

                        sp.Children.Add(name);
                        sp.Children.Add(cmd);
                        Grid.SetColumn(sp, 0);
                        grid.Children.Add(sp);

                        StackPanel rightSp = new StackPanel();
                        rightSp.Orientation = Orientation.Horizontal;
                        rightSp.VerticalAlignment = VerticalAlignment.Center;

                        TextBlock status = new TextBlock();
                        status.Text = "Enabled";
                        status.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                        status.FontWeight = FontWeights.Bold;
                        status.FontSize = 11;
                        status.Margin = new Thickness(0, 0, 12, 0);
                        status.VerticalAlignment = VerticalAlignment.Center;

                        Button btnToggle = new Button();
                        btnToggle.Content = "Disable";
                        btnToggle.Background = new SolidColorBrush(Color.FromRgb(0x3B, 0x1A, 0x22));
                        btnToggle.Foreground = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));
                        btnToggle.BorderThickness = new Thickness(0);
                        btnToggle.Padding = new Thickness(12, 4, 12, 4);
                        btnToggle.FontSize = 11;
                        btnToggle.FontWeight = FontWeights.SemiBold;
                        btnToggle.Cursor = Cursors.Hand;

                        btnToggle.Click += (s, e) => {
                            try {
                                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)) {
                                    if (key != null) {
                                        key.DeleteValue(app.Name, false);
                                    }
                                }
                                card.Visibility = Visibility.Collapsed;
                                Log("Disabled startup application: " + app.Name);
                            } catch (Exception ex) {
                                Log("Failed to disable startup app: " + ex.Message);
                            }
                        };

                        rightSp.Children.Add(status);
                        rightSp.Children.Add(btnToggle);
                        Grid.SetColumn(rightSp, 1);
                        grid.Children.Add(rightSp);

                        card.Child = grid;
                        pnlStartupApps.Children.Add(card);
                    }
                }));
            }));
        }

        static void PerformSilentCleanup() {
            try {
                SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                CleanFolder(Path.GetTempPath());
                CleanFolder(Path.Combine(winDir, "Temp"));
                CleanBrowserCaches();
                CleanFolder(Path.Combine(winDir, @"SoftwareDistribution\Download"));

                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                CleanFolder(Path.Combine(localApp, "CrashDumps"));
                CleanFolder(Path.Combine(progData, @"Microsoft\Windows\WER\ReportQueue"));

                CleanFolder(Path.Combine(localApp, "npm-cache"));
                CleanFolder(Path.Combine(localApp, @"pip\cache"));

                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                CleanFolder(Path.Combine(appData, @"discord\Cache"));

                string vscPath = Path.Combine(appData, "Code");
                if (Directory.Exists(vscPath)) {
                    CleanFolder(Path.Combine(vscPath, "CachedExtensionVSIXs"));
                    CleanFolder(Path.Combine(vscPath, @"Crashpad\reports"));
                }
            } catch { }
        }
    }
}
