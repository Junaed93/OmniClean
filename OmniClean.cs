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
        public string IconText { get; set; }
        public string IconBgHex { get; set; }
        public double SizeMB { get; set; }
        public bool IsInstalled { get; set; }
        public CheckBox CheckBoxRef { get; set; }
        public TextBlock BadgeRef { get; set; }
        public Border CardRef { get; set; }
    }

    public class LargeFileInfo {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string Extension { get; set; }
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
        const string CURRENT_VERSION = "v1.0.0";
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

        // Navigation Controls
        static Button btnNavCleaner;
        static Button btnNavLargeFiles;
        static Button btnNavDuplicates;
        static Button btnNavStartup;
        static Button btnNavSettings;
        static Border barNavCleaner;
        static Border barNavLargeFiles;
        static Border barNavDuplicates;
        static Border barNavStartup;
        static Border barNavSettings;
        static TabControl mainTabControl;

        // Sidebar Telemetry
        static TextBlock txtSidebarCpu;
        static TextBlock txtSidebarRam;
        static ProgressBar barSidebarRam;
        static Button btnQuickFlushRam;
        static TextBlock txtSidebarUptime;

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
        static TextBlock txtHeaderUptimePill;
        static Button btnClean;
        static Button btnRescan;
        static Button btnOptimizeRam;
        static Button btnSelectAll;
        static Button btnSelectSafe;
        static Button btnClearAll;
        static CheckBox chkDryRun;
        static TextBox txtConsole;
        static Button btnExportLog;
        static Button btnClearLog;
        static StackPanel pnlCategories;
        static Border bannerSuccess;
        static TextBlock txtSuccessMsg;

        // Tab 2: Large Files
        static Button btnScanLargeFiles;
        static Button btnFilterAll100;
        static Button btnFilter500MB;
        static Button btnFilter1GB;
        static StackPanel pnlLargeFiles;
        static TextBlock txtLargeFilesSummary;

        // Tab 3: Duplicate Finder
        static Button btnScanDuplicates;
        static Button btnCleanAllDuplicates;
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
        static Button btnTestSilentClean;
        static Button btnCheckUpdates;
        static Button btnOpenGitHub;
        static TextBlock txtSysSpecOs;
        static TextBlock txtSysSpecCpu;
        static TextBlock txtSysSpecRam;
        static TextBlock txtSysSpecGpu;

        static List<CategoryItem> categories = new List<CategoryItem>();
        static List<LargeFileInfo> allLargeFiles = new List<LargeFileInfo>();
        static List<DuplicateFileGroup> duplicateGroups = new List<DuplicateFileGroup>();
        static List<StartupAppItem> startupApps = new List<StartupAppItem>();

        static bool isScanning = false;
        static bool isCleaning = false;
        static string selectedDriveLetter = "C";
        static long currentMinLargeFileBytes = 100 * 1024 * 1024;

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
        Title='OmniClean - Universal Windows Performance &amp; Storage Suite'
        Width='1220' Height='800'
        MinWidth='1060' MinHeight='720'
        WindowStartupLocation='CenterScreen'
        WindowStyle='None'
        AllowsTransparency='True'
        Background='Transparent'
        Foreground='#F8FAFC'
        FontFamily='Segoe UI'>
    <Window.Resources>
        <Style TargetType='Border'>
            <Setter Property='CornerRadius' Value='12'/>
        </Style>
        <Style TargetType='CheckBox'>
            <Setter Property='Foreground' Value='#F1F5F9'/>
            <Setter Property='FontSize' Value='13'/>
            <Setter Property='VerticalContentAlignment' Value='Center'/>
            <Setter Property='Cursor' Value='Hand'/>
        </Style>

        <!-- Sidebar Navigation Button Style -->
        <Style x:Key='SidebarNavBtn' TargetType='Button'>
            <Setter Property='Background' Value='Transparent'/>
            <Setter Property='Foreground' Value='#94A3B8'/>
            <Setter Property='FontSize' Value='13'/>
            <Setter Property='FontWeight' Value='SemiBold'/>
            <Setter Property='BorderThickness' Value='0'/>
            <Setter Property='Cursor' Value='Hand'/>
            <Setter Property='Padding' Value='12,11'/>
            <Setter Property='Margin' Value='0,0,0,5'/>
            <Setter Property='HorizontalContentAlignment' Value='Stretch'/>
            <Setter Property='Template'>
                <Setter.Value>
                    <ControlTemplate TargetType='Button'>
                        <Border Name='bdr' Background='{TemplateBinding Background}' CornerRadius='9' Padding='{TemplateBinding Padding}'>
                            <ContentPresenter VerticalAlignment='Center'/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property='IsMouseOver' Value='True'>
                                <Setter TargetName='bdr' Property='Background' Value='#1E293B'/>
                                <Setter Property='Foreground' Value='#FFFFFF'/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Primary Action Button Style -->
        <Style x:Key='PrimaryBtn' TargetType='Button'>
            <Setter Property='Background' Value='#6366F1'/>
            <Setter Property='Foreground' Value='White'/>
            <Setter Property='FontSize' Value='13'/>
            <Setter Property='FontWeight' Value='Bold'/>
            <Setter Property='BorderThickness' Value='0'/>
            <Setter Property='Cursor' Value='Hand'/>
            <Setter Property='Padding' Value='18,10'/>
            <Setter Property='Template'>
                <Setter.Value>
                    <ControlTemplate TargetType='Button'>
                        <Border Name='bdr' Background='{TemplateBinding Background}' CornerRadius='8' Padding='{TemplateBinding Padding}'>
                            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property='IsMouseOver' Value='True'>
                                <Setter TargetName='bdr' Property='Background' Value='#4F46E5'/>
                            </Trigger>
                            <Trigger Property='IsPressed' Value='True'>
                                <Setter TargetName='bdr' Property='Background' Value='#4338CA'/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Secondary Button Style -->
        <Style x:Key='SecondaryBtn' TargetType='Button'>
            <Setter Property='Background' Value='#162032'/>
            <Setter Property='Foreground' Value='#CBD5E1'/>
            <Setter Property='FontSize' Value='12'/>
            <Setter Property='FontWeight' Value='SemiBold'/>
            <Setter Property='BorderThickness' Value='1'/>
            <Setter Property='BorderBrush' Value='#222F48'/>
            <Setter Property='Cursor' Value='Hand'/>
            <Setter Property='Padding' Value='12,6'/>
            <Setter Property='Template'>
                <Setter.Value>
                    <ControlTemplate TargetType='Button'>
                        <Border Name='bdr' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='7' Padding='{TemplateBinding Padding}'>
                            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property='IsMouseOver' Value='True'>
                                <Setter TargetName='bdr' Property='Background' Value='#22314E'/>
                                <Setter TargetName='bdr' Property='BorderBrush' Value='#3B82F6'/>
                                <Setter Property='Foreground' Value='#FFFFFF'/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Window Control Button (Min/Max) -->
        <Style x:Key='WinControlBtn' TargetType='Button'>
            <Setter Property='Width' Value='36'/>
            <Setter Property='Height' Value='30'/>
            <Setter Property='Background' Value='Transparent'/>
            <Setter Property='Foreground' Value='#94A3B8'/>
            <Setter Property='BorderThickness' Value='0'/>
            <Setter Property='Cursor' Value='Hand'/>
            <Setter Property='Template'>
                <Setter.Value>
                    <ControlTemplate TargetType='Button'>
                        <Border Name='bdr' Background='{TemplateBinding Background}' CornerRadius='6'>
                            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property='IsMouseOver' Value='True'>
                                <Setter TargetName='bdr' Property='Background' Value='#1E293B'/>
                                <Setter Property='Foreground' Value='#FFFFFF'/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Window Close Button -->
        <Style x:Key='WinCloseBtn' TargetType='Button'>
            <Setter Property='Width' Value='36'/>
            <Setter Property='Height' Value='30'/>
            <Setter Property='Background' Value='Transparent'/>
            <Setter Property='Foreground' Value='#94A3B8'/>
            <Setter Property='BorderThickness' Value='0'/>
            <Setter Property='Cursor' Value='Hand'/>
            <Setter Property='Template'>
                <Setter.Value>
                    <ControlTemplate TargetType='Button'>
                        <Border Name='bdr' Background='{TemplateBinding Background}' CornerRadius='6'>
                            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property='IsMouseOver' Value='True'>
                                <Setter TargetName='bdr' Property='Background' Value='#DC2626'/>
                                <Setter Property='Foreground' Value='#FFFFFF'/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
    </Window.Resources>

    <!-- Outer Glassmorphic Frame -->
    <Border Background='#0A0E1A' BorderBrush='#1E293B' BorderThickness='1' CornerRadius='16'>
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height='46'/>
                <RowDefinition Height='Auto'/>
                <RowDefinition Height='*'/>
            </Grid.RowDefinitions>

            <!-- Top Titlebar / Header -->
            <Border Grid.Row='0' Name='titleBar' Background='#0D1324' BorderBrush='#1A2338' BorderThickness='0,0,0,1' Padding='16,0' Cursor='SizeAll'>
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width='Auto'/>
                        <ColumnDefinition Width='*'/>
                        <ColumnDefinition Width='Auto'/>
                    </Grid.ColumnDefinitions>

                    <!-- Brand & Version -->
                    <StackPanel Grid.Column='0' Orientation='Horizontal' VerticalAlignment='Center'>
                        <Border Width='28' Height='28' Background='#6366F1' CornerRadius='8' Margin='0,0,10,0'>
                            <TextBlock Text='⚡' FontSize='15' Foreground='White' HorizontalAlignment='Center' VerticalAlignment='Center'/>
                        </Border>
                        <TextBlock Text='OmniClean' FontSize='15' FontWeight='Bold' Foreground='#FFFFFF' VerticalAlignment='Center'/>
                        <Border Background='#231B4D' BorderBrush='#6366F1' BorderThickness='1' CornerRadius='5' Padding='5,1' Margin='8,0,0,0' VerticalAlignment='Center'>
                            <TextBlock Text='v1.0.0 PRO' FontSize='9' FontWeight='Bold' Foreground='#A5B4FC'/>
                        </Border>
                    </StackPanel>

                    <!-- Center Uptime & Status Badge -->
                    <StackPanel Grid.Column='1' Orientation='Horizontal' HorizontalAlignment='Center' VerticalAlignment='Center'>
                        <Border Background='#111A2E' BorderBrush='#1E293B' BorderThickness='1' CornerRadius='14' Padding='12,4'>
                            <StackPanel Orientation='Horizontal'>
                                <Ellipse Width='7' Height='7' Fill='#10B981' Margin='0,0,7,0' VerticalAlignment='Center'/>
                                <TextBlock Name='txtHeaderUptimePill' Text='System Online' FontSize='11' Foreground='#94A3B8' VerticalAlignment='Center'/>
                            </StackPanel>
                        </Border>
                    </StackPanel>

                    <!-- Window Control Buttons -->
                    <StackPanel Grid.Column='2' Orientation='Horizontal' VerticalAlignment='Center'>
                        <Button Name='btnMinimize' Style='{StaticResource WinControlBtn}' Content='—' FontWeight='Bold'/>
                        <Button Name='btnMaximize' Style='{StaticResource WinControlBtn}' Content='▢' FontWeight='Bold'/>
                        <Button Name='btnClose' Style='{StaticResource WinCloseBtn}' Content='✕' FontWeight='Bold'/>
                    </StackPanel>
                </Grid>
            </Border>

            <!-- Update Available Notification Banner -->
            <Border Grid.Row='1' Name='bannerUpdate' Background='#1E1A38' BorderBrush='#6366F1' BorderThickness='0,0,0,1' Padding='16,8' Visibility='Collapsed'>
                <Grid>
                    <StackPanel Orientation='Horizontal' VerticalAlignment='Center'>
                        <TextBlock Text='⭐' FontSize='14' Margin='0,0,8,0'/>
                        <TextBlock Name='txtUpdateNotice' Text='A new release of OmniClean is available!' FontSize='12' FontWeight='SemiBold' Foreground='#E0E7FF' VerticalAlignment='Center'/>
                    </StackPanel>
                    <Button Name='btnDownloadUpdate' Content='View Release on GitHub' HorizontalAlignment='Right' Style='{StaticResource PrimaryBtn}' Padding='14,4' FontSize='11'/>
                </Grid>
            </Border>

            <!-- Main App Body: Left Sidebar + Right Workspace -->
            <Grid Grid.Row='2'>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width='240'/>
                    <ColumnDefinition Width='*'/>
                </Grid.ColumnDefinitions>

                <!-- LEFT SIDEBAR -->
                <Border Grid.Column='0' Background='#0C1222' BorderBrush='#1B253B' BorderThickness='0,0,1,0' Padding='12,14'>
                    <Grid>
                        <Grid.RowDefinitions>
                            <RowDefinition Height='*'/>
                            <RowDefinition Height='Auto'/>
                        </Grid.RowDefinitions>

                        <!-- Navigation Items -->
                        <StackPanel Grid.Row='0'>
                            <TextBlock Text='NAVIGATION' FontSize='10' FontWeight='Bold' Foreground='#475569' Margin='10,0,0,10'/>

                            <!-- Nav 0: Storage Cleaner -->
                            <Button Name='btnNavCleaner' Style='{StaticResource SidebarNavBtn}'>
                                <Grid>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width='Auto'/>
                                        <ColumnDefinition Width='*'/>
                                        <ColumnDefinition Width='Auto'/>
                                    </Grid.ColumnDefinitions>
                                    <Border Name='barNavCleaner' Grid.Column='0' Width='3' Height='18' Background='#6366F1' CornerRadius='2' Margin='0,0,10,0' Visibility='Visible'/>
                                    <TextBlock Grid.Column='1' Text='🧹 Storage Cleaner' VerticalAlignment='Center'/>
                                </Grid>
                            </Button>

                            <!-- Nav 1: Large Files -->
                            <Button Name='btnNavLargeFiles' Style='{StaticResource SidebarNavBtn}'>
                                <Grid>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width='Auto'/>
                                        <ColumnDefinition Width='*'/>
                                        <ColumnDefinition Width='Auto'/>
                                    </Grid.ColumnDefinitions>
                                    <Border Name='barNavLargeFiles' Grid.Column='0' Width='3' Height='18' Background='#6366F1' CornerRadius='2' Margin='0,0,10,0' Visibility='Collapsed'/>
                                    <TextBlock Grid.Column='1' Text='📦 Large Files Finder' VerticalAlignment='Center'/>
                                </Grid>
                            </Button>

                            <!-- Nav 2: Duplicate Finder -->
                            <Button Name='btnNavDuplicates' Style='{StaticResource SidebarNavBtn}'>
                                <Grid>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width='Auto'/>
                                        <ColumnDefinition Width='*'/>
                                        <ColumnDefinition Width='Auto'/>
                                    </Grid.ColumnDefinitions>
                                    <Border Name='barNavDuplicates' Grid.Column='0' Width='3' Height='18' Background='#6366F1' CornerRadius='2' Margin='0,0,10,0' Visibility='Collapsed'/>
                                    <TextBlock Grid.Column='1' Text='👥 Duplicate Finder' VerticalAlignment='Center'/>
                                </Grid>
                            </Button>

                            <!-- Nav 3: Startup Optimizer -->
                            <Button Name='btnNavStartup' Style='{StaticResource SidebarNavBtn}'>
                                <Grid>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width='Auto'/>
                                        <ColumnDefinition Width='*'/>
                                        <ColumnDefinition Width='Auto'/>
                                    </Grid.ColumnDefinitions>
                                    <Border Name='barNavStartup' Grid.Column='0' Width='3' Height='18' Background='#6366F1' CornerRadius='2' Margin='0,0,10,0' Visibility='Collapsed'/>
                                    <TextBlock Grid.Column='1' Text='🚀 Startup Optimizer' VerticalAlignment='Center'/>
                                </Grid>
                            </Button>

                            <!-- Nav 4: Automation & Settings -->
                            <Button Name='btnNavSettings' Style='{StaticResource SidebarNavBtn}'>
                                <Grid>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width='Auto'/>
                                        <ColumnDefinition Width='*'/>
                                        <ColumnDefinition Width='Auto'/>
                                    </Grid.ColumnDefinitions>
                                    <Border Name='barNavSettings' Grid.Column='0' Width='3' Height='18' Background='#6366F1' CornerRadius='2' Margin='0,0,10,0' Visibility='Collapsed'/>
                                    <TextBlock Grid.Column='1' Text='⚙️ Automation &amp; About' VerticalAlignment='Center'/>
                                </Grid>
                            </Button>
                        </StackPanel>

                        <!-- Bottom Sidebar: Live Telemetry Widget -->
                        <Border Grid.Row='1' Background='#10182C' BorderBrush='#1E293B' BorderThickness='1' CornerRadius='10' Padding='12,10'>
                            <StackPanel>
                                <TextBlock Text='SYSTEM HEALTH' FontSize='10' FontWeight='Bold' Foreground='#64748B' Margin='0,0,0,6'/>
                                <TextBlock Name='txtSidebarCpu' Text='CPU: Detecting...' FontSize='11' FontWeight='SemiBold' Foreground='#CBD5E1' TextTrimming='CharacterEllipsis' Margin='0,0,0,4'/>
                                
                                <Grid Margin='0,0,0,4'>
                                    <TextBlock Name='txtSidebarRam' Text='RAM: -- / -- GB' FontSize='11' Foreground='#94A3B8'/>
                                </Grid>
                                <ProgressBar Name='barSidebarRam' Height='6' Value='40' Maximum='100' Foreground='#38BDF8' Background='#090E1A' BorderThickness='0' Margin='0,0,0,8'/>

                                <Button Name='btnQuickFlushRam' Content='⚡ Flush Standby RAM' Style='{StaticResource SecondaryBtn}' Padding='8,4' FontSize='10' HorizontalAlignment='Stretch'/>
                                
                                <TextBlock Name='txtSidebarUptime' Text='Uptime: --' FontSize='10' Foreground='#64748B' Margin='0,8,0,0' HorizontalAlignment='Center'/>
                            </StackPanel>
                        </Border>
                    </Grid>
                </Border>

                <!-- RIGHT WORKSPACE CANVAS -->
                <Grid Grid.Column='1' Margin='18,14'>
                    <TabControl Name='mainTabControl' Background='Transparent' BorderThickness='0'>
                        <TabControl.ItemContainerStyle>
                            <Style TargetType='TabItem'>
                                <Setter Property='Visibility' Value='Collapsed'/>
                            </Style>
                        </TabControl.ItemContainerStyle>

                        <!-- ================= TAB 0: STORAGE CLEANER ================= -->
                        <TabItem>
                            <Grid>
                                <Grid.RowDefinitions>
                                    <RowDefinition Height='Auto'/>
                                    <RowDefinition Height='*'/>
                                </Grid.RowDefinitions>

                                <!-- Header Bar: View Title + Drive Selector + Top Actions -->
                                <Grid Grid.Row='0' Margin='0,0,0,14'>
                                    <StackPanel VerticalAlignment='Center'>
                                        <TextBlock Text='Storage Cleaner' FontSize='20' FontWeight='Bold' Foreground='#FFFFFF'/>
                                        <TextBlock Text='Inspect drives and safely purge redundant caches and temporary storage' FontSize='12' Foreground='#94A3B8'/>
                                    </StackPanel>
                                    <StackPanel Orientation='Horizontal' HorizontalAlignment='Right' VerticalAlignment='Center'>
                                        <Border Background='#11192C' BorderBrush='#222F48' BorderThickness='1' CornerRadius='7' Padding='8,4' Margin='0,0,8,0'>
                                            <StackPanel Orientation='Horizontal' VerticalAlignment='Center'>
                                                <TextBlock Text='Drive:' FontSize='12' FontWeight='Bold' Foreground='#94A3B8' VerticalAlignment='Center' Margin='0,0,6,0'/>
                                                <ComboBox Name='cmbDrive' Width='110' Height='24' Background='#0E1424' Foreground='#F8FAFC' BorderThickness='0'/>
                                            </StackPanel>
                                        </Border>
                                        <Button Name='btnRescan' Content='🔄 Rescan' Style='{StaticResource SecondaryBtn}' Margin='0,0,6,0'/>
                                        <Button Name='btnOptimizeRam' Content='⚡ Flush RAM' Style='{StaticResource SecondaryBtn}'/>
                                    </StackPanel>
                                </Grid>

                                <!-- Two-Column Content: Left Stats & Console, Right Categories -->
                                <Grid Grid.Row='1'>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width='340'/>
                                        <ColumnDefinition Width='*'/>
                                    </Grid.ColumnDefinitions>

                                    <!-- Left Column: Drive Capacity Gauge + Live Activity Console -->
                                    <Grid Grid.Column='0' Margin='0,0,14,0'>
                                        <Grid.RowDefinitions>
                                            <RowDefinition Height='Auto'/>
                                            <RowDefinition Height='*'/>
                                        </Grid.RowDefinitions>

                                        <!-- Disk Space Hero Card -->
                                        <Border Grid.Row='0' Background='#11182B' BorderBrush='#1F2B45' BorderThickness='1' Padding='16' Margin='0,0,0,12'>
                                            <StackPanel>
                                                <TextBlock Text='DRIVE CAPACITY' FontSize='10' FontWeight='Bold' Foreground='#64748B' Margin='0,0,0,6'/>
                                                <TextBlock Name='txtDiskPercent' Text='--% Free' FontSize='32' FontWeight='Bold' Foreground='#10B981' Margin='0,0,0,2'/>
                                                <TextBlock Name='txtDiskRatio' Text='-- GB Free / -- GB' FontSize='12' Foreground='#94A3B8' Margin='0,0,0,10'/>
                                                <ProgressBar Name='diskProgress' Height='10' Value='30' Maximum='100' Foreground='#10B981' Background='#0A0E1A' BorderThickness='0' Margin='0,0,0,14'/>

                                                <Grid>
                                                    <Grid.ColumnDefinitions>
                                                        <ColumnDefinition Width='*'/>
                                                        <ColumnDefinition Width='*'/>
                                                        <ColumnDefinition Width='*'/>
                                                    </Grid.ColumnDefinitions>
                                                    <Border Grid.Column='0' Background='#0A0E1A' Padding='6,8' Margin='0,0,4,0' CornerRadius='8'>
                                                        <StackPanel HorizontalAlignment='Center'>
                                                            <TextBlock Text='Free' FontSize='10' Foreground='#64748B'/>
                                                            <TextBlock Name='txtStatFree' Text='-- GB' FontSize='12' FontWeight='Bold' Foreground='#10B981'/>
                                                        </StackPanel>
                                                    </Border>
                                                    <Border Grid.Column='1' Background='#0A0E1A' Padding='6,8' Margin='2,0,2,0' CornerRadius='8'>
                                                        <StackPanel HorizontalAlignment='Center'>
                                                            <TextBlock Text='Used' FontSize='10' Foreground='#64748B'/>
                                                            <TextBlock Name='txtStatUsed' Text='-- GB' FontSize='12' FontWeight='Bold' Foreground='#818CF8'/>
                                                        </StackPanel>
                                                    </Border>
                                                    <Border Grid.Column='2' Background='#0A0E1A' Padding='6,8' Margin='4,0,0,0' CornerRadius='8'>
                                                        <StackPanel HorizontalAlignment='Center'>
                                                            <TextBlock Text='Total' FontSize='10' Foreground='#64748B'/>
                                                            <TextBlock Name='txtStatTotal' Text='-- GB' FontSize='12' FontWeight='Bold' Foreground='#38BDF8'/>
                                                        </StackPanel>
                                                    </Border>
                                                </Grid>
                                            </StackPanel>
                                        </Border>

                                        <!-- Live Diagnostic Activity Console -->
                                        <Border Grid.Row='1' Background='#0B101E' BorderBrush='#1A2338' BorderThickness='1' Padding='12'>
                                            <Grid>
                                                <Grid.RowDefinitions>
                                                    <RowDefinition Height='Auto'/>
                                                    <RowDefinition Height='*'/>
                                                </Grid.RowDefinitions>
                                                <Grid Grid.Row='0' Margin='0,0,0,8'>
                                                    <StackPanel Orientation='Horizontal' VerticalAlignment='Center'>
                                                        <Ellipse Width='7' Height='7' Fill='#06B6D4' Margin='0,0,6,0'/>
                                                        <TextBlock Text='Activity Console' FontSize='11' FontWeight='SemiBold' Foreground='#94A3B8'/>
                                                    </StackPanel>
                                                    <StackPanel Orientation='Horizontal' HorizontalAlignment='Right'>
                                                        <Button Name='btnClearLog' Content='Clear' Background='Transparent' Foreground='#64748B' BorderThickness='0' FontSize='10' Margin='0,0,8,0' Cursor='Hand'/>
                                                        <Button Name='btnExportLog' Content='Export Log' Background='Transparent' Foreground='#38BDF8' BorderThickness='0' FontSize='10' Cursor='Hand'/>
                                                    </StackPanel>
                                                </Grid>
                                                <TextBox Grid.Row='1' Name='txtConsole' IsReadOnly='True' Background='Transparent' Foreground='#CBD5E1' BorderThickness='0' FontFamily='Consolas' FontSize='11' VerticalScrollBarVisibility='Auto' TextWrapping='Wrap'/>
                                            </Grid>
                                        </Border>
                                    </Grid>

                                    <!-- Right Column: Cleanable Targets & Bottom Action Dock -->
                                    <Border Grid.Column='1' Background='#11182B' BorderBrush='#1F2B45' BorderThickness='1' Padding='16'>
                                        <Grid>
                                            <Grid.RowDefinitions>
                                                <RowDefinition Height='Auto'/>
                                                <RowDefinition Height='Auto'/>
                                                <RowDefinition Height='*'/>
                                                <RowDefinition Height='Auto'/>
                                            </Grid.RowDefinitions>

                                            <!-- Categories Filter Header -->
                                            <Grid Grid.Row='0' Margin='0,0,0,10'>
                                                <StackPanel>
                                                    <TextBlock Text='Cleanable Targets' FontSize='16' FontWeight='Bold' Foreground='White'/>
                                                    <TextBlock Text='Select items to safely clear without affecting personal files' FontSize='11' Foreground='#94A3B8'/>
                                                </StackPanel>
                                                <StackPanel Orientation='Horizontal' HorizontalAlignment='Right' VerticalAlignment='Center'>
                                                    <Button Name='btnSelectAll' Content='Select All' Style='{StaticResource SecondaryBtn}' Padding='8,4' FontSize='11' Margin='0,0,5,0'/>
                                                    <Button Name='btnSelectSafe' Content='Safe Default' Style='{StaticResource SecondaryBtn}' Padding='8,4' FontSize='11' Margin='0,0,5,0'/>
                                                    <Button Name='btnClearAll' Content='Clear' Style='{StaticResource SecondaryBtn}' Padding='8,4' FontSize='11'/>
                                                </StackPanel>
                                            </Grid>

                                            <!-- In-App Success Banner -->
                                            <Border Grid.Row='1' Name='bannerSuccess' Background='#0D2721' BorderBrush='#10B981' BorderThickness='1' Padding='12,8' Margin='0,0,0,10' CornerRadius='8' Visibility='Collapsed'>
                                                <TextBlock Name='txtSuccessMsg' Text='Cleanup completed successfully.' FontSize='12' FontWeight='SemiBold' Foreground='#34D399'/>
                                            </Border>

                                            <!-- Scrollable Category Cards -->
                                            <ScrollViewer Grid.Row='2' VerticalScrollBarVisibility='Auto' Margin='0,0,0,12'>
                                                <StackPanel Name='pnlCategories'/>
                                            </ScrollViewer>

                                            <!-- Bottom Action Dock -->
                                            <Border Grid.Row='3' Background='#0B101E' BorderBrush='#1E293B' BorderThickness='1' Padding='14,10' CornerRadius='10'>
                                                <Grid>
                                                    <StackPanel VerticalAlignment='Center'>
                                                        <TextBlock Text='Selected to Reclaim:' FontSize='11' Foreground='#94A3B8'/>
                                                        <TextBlock Name='txtSelectedSize' Text='0.00 MB' FontSize='22' FontWeight='Bold' Foreground='#F8FAFC'/>
                                                    </StackPanel>
                                                    <StackPanel Orientation='Horizontal' HorizontalAlignment='Right' VerticalAlignment='Center'>
                                                        <CheckBox Name='chkDryRun' Content='Dry Run (Simulation Only)' FontSize='12' Margin='0,0,16,0' VerticalAlignment='Center'/>
                                                        <Button Name='btnClean' Content='⚡ Clean Selected Storage' Style='{StaticResource PrimaryBtn}' Padding='24,12' FontSize='14'/>
                                                    </StackPanel>
                                                </Grid>
                                            </Border>
                                        </Grid>
                                    </Border>
                                </Grid>
                            </Grid>
                        </TabItem>

                        <!-- ================= TAB 1: LARGE FILES FINDER ================= -->
                        <TabItem>
                            <Border Background='#11182B' BorderBrush='#1F2B45' BorderThickness='1' Padding='18'>
                                <Grid>
                                    <Grid.RowDefinitions>
                                        <RowDefinition Height='Auto'/>
                                        <RowDefinition Height='Auto'/>
                                        <RowDefinition Height='*'/>
                                    </Grid.RowDefinitions>

                                    <!-- Header -->
                                    <Grid Grid.Row='0' Margin='0,0,0,14'>
                                        <StackPanel>
                                            <TextBlock Text='Large Files Profiler' FontSize='20' FontWeight='Bold' Foreground='White'/>
                                            <TextBlock Name='txtLargeFilesSummary' Text='Discover large files (>100 MB) consuming your storage space.' FontSize='12' Foreground='#94A3B8'/>
                                        </StackPanel>
                                        <Button Name='btnScanLargeFiles' Content='🔍 Scan for Large Files' Style='{StaticResource PrimaryBtn}' HorizontalAlignment='Right' Padding='18,8'/>
                                    </Grid>

                                    <!-- Size Filters -->
                                    <StackPanel Grid.Row='1' Orientation='Horizontal' Margin='0,0,0,12'>
                                        <TextBlock Text='Filter by Size:' FontSize='11' Foreground='#64748B' VerticalAlignment='Center' Margin='0,0,8,0'/>
                                        <Button Name='btnFilterAll100' Content='All (&gt;100 MB)' Style='{StaticResource SecondaryBtn}' Padding='10,4' FontSize='11' Margin='0,0,6,0'/>
                                        <Button Name='btnFilter500MB' Content='&gt;500 MB' Style='{StaticResource SecondaryBtn}' Padding='10,4' FontSize='11' Margin='0,0,6,0'/>
                                        <Button Name='btnFilter1GB' Content='&gt;1 GB (Huge)' Style='{StaticResource SecondaryBtn}' Padding='10,4' FontSize='11'/>
                                    </StackPanel>

                                    <ScrollViewer Grid.Row='2' VerticalScrollBarVisibility='Auto'>
                                        <StackPanel Name='pnlLargeFiles'/>
                                    </ScrollViewer>
                                </Grid>
                            </Border>
                        </TabItem>

                        <!-- ================= TAB 2: DUPLICATE FINDER ================= -->
                        <TabItem>
                            <Border Background='#11182B' BorderBrush='#1F2B45' BorderThickness='1' Padding='18'>
                                <Grid>
                                    <Grid.RowDefinitions>
                                        <RowDefinition Height='Auto'/>
                                        <RowDefinition Height='*'/>
                                    </Grid.RowDefinitions>

                                    <!-- Header -->
                                    <Grid Grid.Row='0' Margin='0,0,0,14'>
                                        <StackPanel>
                                            <TextBlock Text='Cryptographic Duplicate Finder' FontSize='20' FontWeight='Bold' Foreground='White'/>
                                            <TextBlock Name='txtDuplicatesSummary' Text='Scans user folders for 100% byte-matched duplicate files using SHA-256 cryptographic hashing.' FontSize='12' Foreground='#94A3B8'/>
                                        </StackPanel>
                                        <StackPanel Orientation='Horizontal' HorizontalAlignment='Right'>
                                            <Button Name='btnCleanAllDuplicates' Content='🧹 Delete All Duplicate Copies' Style='{StaticResource SecondaryBtn}' Margin='0,0,8,0' Padding='14,8'/>
                                            <Button Name='btnScanDuplicates' Content='🔎 Find Duplicates' Style='{StaticResource PrimaryBtn}' Padding='18,8'/>
                                        </StackPanel>
                                    </Grid>

                                    <ScrollViewer Grid.Row='1' VerticalScrollBarVisibility='Auto'>
                                        <StackPanel Name='pnlDuplicates'/>
                                    </ScrollViewer>
                                </Grid>
                            </Border>
                        </TabItem>

                        <!-- ================= TAB 3: STARTUP OPTIMIZER ================= -->
                        <TabItem>
                            <Border Background='#11182B' BorderBrush='#1F2B45' BorderThickness='1' Padding='18'>
                                <Grid>
                                    <Grid.RowDefinitions>
                                        <RowDefinition Height='Auto'/>
                                        <RowDefinition Height='*'/>
                                    </Grid.RowDefinitions>

                                    <!-- Header -->
                                    <Grid Grid.Row='0' Margin='0,0,0,14'>
                                        <StackPanel>
                                            <TextBlock Text='Startup Applications Optimizer' FontSize='20' FontWeight='Bold' Foreground='White'/>
                                            <TextBlock Name='txtStartupSummary' Text='Manage background launchers in Windows Registry to speed up PC boot time and reduce idle RAM.' FontSize='12' Foreground='#94A3B8'/>
                                        </StackPanel>
                                        <Button Name='btnScanStartup' Content='🔄 Refresh Startup List' Style='{StaticResource SecondaryBtn}' HorizontalAlignment='Right' Padding='14,8'/>
                                    </Grid>

                                    <ScrollViewer Grid.Row='1' VerticalScrollBarVisibility='Auto'>
                                        <StackPanel Name='pnlStartupApps'/>
                                    </ScrollViewer>
                                </Grid>
                            </Border>
                        </TabItem>

                        <!-- ================= TAB 4: AUTOMATION & ABOUT ================= -->
                        <TabItem>
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width='*'/>
                                    <ColumnDefinition Width='*'/>
                                </Grid.ColumnDefinitions>

                                <!-- Left Card: Maintenance Automation -->
                                <Border Grid.Column='0' Background='#11182B' BorderBrush='#1F2B45' BorderThickness='1' Padding='20' Margin='0,0,10,0'>
                                    <StackPanel>
                                        <TextBlock Text='Automated Maintenance' FontSize='18' FontWeight='Bold' Foreground='White' Margin='0,0,0,6'/>
                                        <TextBlock Text='Schedule silent background cleaning via Windows Task Scheduler. Cleans Recycle Bin, temp files, and caches automatically without any user interaction.' FontSize='12' Foreground='#94A3B8' TextWrapping='Wrap' Margin='0,0,0,16'/>

                                        <Border Background='#0B101E' BorderBrush='#1A2338' BorderThickness='1' CornerRadius='8' Padding='14' Margin='0,0,0,16'>
                                            <StackPanel>
                                                <CheckBox Name='chkAutoClean' Content='Enable Weekly Silent Auto-Clean (Sundays at 12:00 PM)' Margin='0,0,0,8'/>
                                                <TextBlock Name='txtScheduleStatus' Text='Status: Checking schedule...' FontSize='11' Foreground='#64748B'/>
                                            </StackPanel>
                                        </Border>

                                        <TextBlock Text='Quick Shortcuts &amp; Diagnostic Actions' FontSize='13' FontWeight='SemiBold' Foreground='#CBD5E1' Margin='0,0,0,10'/>
                                        <Button Name='btnCreateShortcut' Content='Create Desktop Shortcut' Style='{StaticResource SecondaryBtn}' Padding='14,8' Margin='0,0,0,8' HorizontalAlignment='Stretch'/>
                                        <Button Name='btnTestSilentClean' Content='Run Silent Clean Now (Test Run)' Style='{StaticResource SecondaryBtn}' Padding='14,8' HorizontalAlignment='Stretch'/>
                                    </StackPanel>
                                </Border>

                                <!-- Right Card: About & Hardware Telemetry -->
                                <Border Grid.Column='1' Background='#11182B' BorderBrush='#1F2B45' BorderThickness='1' Padding='20' Margin='10,0,0,0'>
                                    <StackPanel>
                                        <TextBlock Text='System &amp; Application Specs' FontSize='18' FontWeight='Bold' Foreground='White' Margin='0,0,0,6'/>
                                        <TextBlock Text='Version 1.0.0 Universal Standalone Edition' FontSize='12' Foreground='#94A3B8' Margin='0,0,0,14'/>

                                        <Border Background='#0B101E' BorderBrush='#1A2338' BorderThickness='1' CornerRadius='8' Padding='14' Margin='0,0,0,16'>
                                            <StackPanel>
                                                <TextBlock Name='txtSysSpecOs' Text='OS: Windows' FontSize='12' Foreground='#CBD5E1' Margin='0,0,0,4'/>
                                                <TextBlock Name='txtSysSpecCpu' Text='CPU: Detecting...' FontSize='12' Foreground='#CBD5E1' Margin='0,0,0,4'/>
                                                <TextBlock Name='txtSysSpecRam' Text='RAM: Detecting...' FontSize='12' Foreground='#CBD5E1' Margin='0,0,0,4'/>
                                                <TextBlock Name='txtSysSpecGpu' Text='GPU: Detecting...' FontSize='12' Foreground='#CBD5E1'/>
                                            </StackPanel>
                                        </Border>

                                        <TextBlock Text='Safety &amp; Integrity Guarantees' FontSize='13' FontWeight='SemiBold' Foreground='#CBD5E1' Margin='0,0,0,8'/>
                                        <TextBlock Text='• Safe by Design: Never touches browser passwords, cookies, or bookmarks.' FontSize='11' Foreground='#10B981' Margin='0,0,0,4'/>
                                        <TextBlock Text='• Non-Destructive: Leaves personal files and SSD flash wear cycles untouched.' FontSize='11' Foreground='#10B981' Margin='0,0,0,4'/>
                                        <TextBlock Text='• Zero Background Resident Daemons: 0% idle CPU and RAM when closed.' FontSize='11' Foreground='#10B981' Margin='0,0,0,16'/>

                                        <Grid>
                                            <Grid.ColumnDefinitions>
                                                <ColumnDefinition Width='*'/>
                                                <ColumnDefinition Width='*'/>
                                            </Grid.ColumnDefinitions>
                                            <Button Name='btnCheckUpdates' Grid.Column='0' Content='Check for Updates' Style='{StaticResource SecondaryBtn}' Padding='10,8' Margin='0,0,4,0'/>
                                            <Button Name='btnOpenGitHub' Grid.Column='1' Content='GitHub Repository' Style='{StaticResource SecondaryBtn}' Padding='10,8' Margin='4,0,0,0'/>
                                        </Grid>
                                    </StackPanel>
                                </Border>
                            </Grid>
                        </TabItem>
                    </TabControl>
                </Grid>
            </Grid>
        </Grid>
    </Border>
</Window>";

                mainWindow = (Window)XamlReader.Parse(xaml);

                IntPtr hwnd = new WindowInteropHelper(mainWindow).EnsureHandle();
                int useDark = 1;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));

                // Titlebar and Window Controls
                titleBar = (Border)mainWindow.FindName("titleBar");
                btnMinimize = (Button)mainWindow.FindName("btnMinimize");
                btnMaximize = (Button)mainWindow.FindName("btnMaximize");
                btnClose = (Button)mainWindow.FindName("btnClose");
                txtHeaderUptimePill = (TextBlock)mainWindow.FindName("txtHeaderUptimePill");

                titleBar.MouseLeftButtonDown += (s, e) => {
                    if (e.ButtonState == MouseButtonState.Pressed) mainWindow.DragMove();
                };
                btnMinimize.Click += (s, e) => mainWindow.WindowState = WindowState.Minimized;
                btnMaximize.Click += (s, e) => mainWindow.WindowState = (mainWindow.WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
                btnClose.Click += (s, e) => mainWindow.Close();

                // Navigation Controls
                btnNavCleaner = (Button)mainWindow.FindName("btnNavCleaner");
                btnNavLargeFiles = (Button)mainWindow.FindName("btnNavLargeFiles");
                btnNavDuplicates = (Button)mainWindow.FindName("btnNavDuplicates");
                btnNavStartup = (Button)mainWindow.FindName("btnNavStartup");
                btnNavSettings = (Button)mainWindow.FindName("btnNavSettings");

                barNavCleaner = (Border)mainWindow.FindName("barNavCleaner");
                barNavLargeFiles = (Border)mainWindow.FindName("barNavLargeFiles");
                barNavDuplicates = (Border)mainWindow.FindName("barNavDuplicates");
                barNavStartup = (Border)mainWindow.FindName("barNavStartup");
                barNavSettings = (Border)mainWindow.FindName("barNavSettings");

                mainTabControl = (TabControl)mainWindow.FindName("mainTabControl");

                btnNavCleaner.Click += (s, e) => SwitchTab(0);
                btnNavLargeFiles.Click += (s, e) => SwitchTab(1);
                btnNavDuplicates.Click += (s, e) => SwitchTab(2);
                btnNavStartup.Click += (s, e) => SwitchTab(3);
                btnNavSettings.Click += (s, e) => SwitchTab(4);

                // Sidebar Telemetry
                txtSidebarCpu = (TextBlock)mainWindow.FindName("txtSidebarCpu");
                txtSidebarRam = (TextBlock)mainWindow.FindName("txtSidebarRam");
                barSidebarRam = (ProgressBar)mainWindow.FindName("barSidebarRam");
                btnQuickFlushRam = (Button)mainWindow.FindName("btnQuickFlushRam");
                txtSidebarUptime = (TextBlock)mainWindow.FindName("txtSidebarUptime");
                btnQuickFlushRam.Click += (s, e) => OptimizeRam();

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
                btnClean = (Button)mainWindow.FindName("btnClean");
                btnRescan = (Button)mainWindow.FindName("btnRescan");
                btnOptimizeRam = (Button)mainWindow.FindName("btnOptimizeRam");
                btnSelectAll = (Button)mainWindow.FindName("btnSelectAll");
                btnSelectSafe = (Button)mainWindow.FindName("btnSelectSafe");
                btnClearAll = (Button)mainWindow.FindName("btnClearAll");
                chkDryRun = (CheckBox)mainWindow.FindName("chkDryRun");
                txtConsole = (TextBox)mainWindow.FindName("txtConsole");
                btnExportLog = (Button)mainWindow.FindName("btnExportLog");
                btnClearLog = (Button)mainWindow.FindName("btnClearLog");
                pnlCategories = (StackPanel)mainWindow.FindName("pnlCategories");
                bannerSuccess = (Border)mainWindow.FindName("bannerSuccess");
                txtSuccessMsg = (TextBlock)mainWindow.FindName("txtSuccessMsg");

                cmbDrive.SelectionChanged += (s, e) => {
                    if (cmbDrive.SelectedItem != null) {
                        string sel = cmbDrive.SelectedItem.ToString();
                        if (sel.Length > 0) {
                            selectedDriveLetter = sel.Substring(0, 1).ToUpper();
                            UpdateDiskMetrics();
                            RunScanAsync();
                        }
                    }
                };
                btnRescan.Click += (s, e) => RunScanAsync();
                btnOptimizeRam.Click += (s, e) => OptimizeRam();
                btnSelectAll.Click += (s, e) => SetAllCheckboxes(true);
                btnSelectSafe.Click += (s, e) => SetSafeCheckboxes();
                btnClearAll.Click += (s, e) => SetAllCheckboxes(false);
                btnClean.Click += (s, e) => StartCleanAsync();
                btnClearLog.Click += (s, e) => txtConsole.Clear();
                btnExportLog.Click += (s, e) => ExportLog();

                // Tab 2 Bindings
                btnScanLargeFiles = (Button)mainWindow.FindName("btnScanLargeFiles");
                btnFilterAll100 = (Button)mainWindow.FindName("btnFilterAll100");
                btnFilter500MB = (Button)mainWindow.FindName("btnFilter500MB");
                btnFilter1GB = (Button)mainWindow.FindName("btnFilter1GB");
                pnlLargeFiles = (StackPanel)mainWindow.FindName("pnlLargeFiles");
                txtLargeFilesSummary = (TextBlock)mainWindow.FindName("txtLargeFilesSummary");

                btnScanLargeFiles.Click += (s, e) => ScanLargeFilesAsync();
                btnFilterAll100.Click += (s, e) => ApplyLargeFileFilter(100 * 1024 * 1024);
                btnFilter500MB.Click += (s, e) => ApplyLargeFileFilter(500 * 1024 * 1024);
                btnFilter1GB.Click += (s, e) => ApplyLargeFileFilter(1024L * 1024 * 1024);

                // Tab 3 Bindings
                btnScanDuplicates = (Button)mainWindow.FindName("btnScanDuplicates");
                btnCleanAllDuplicates = (Button)mainWindow.FindName("btnCleanAllDuplicates");
                pnlDuplicates = (StackPanel)mainWindow.FindName("pnlDuplicates");
                txtDuplicatesSummary = (TextBlock)mainWindow.FindName("txtDuplicatesSummary");
                btnScanDuplicates.Click += (s, e) => ScanDuplicatesAsync();
                btnCleanAllDuplicates.Click += (s, e) => CleanAllDuplicatesAsync();

                // Tab 4 Bindings
                btnScanStartup = (Button)mainWindow.FindName("btnScanStartup");
                pnlStartupApps = (StackPanel)mainWindow.FindName("pnlStartupApps");
                txtStartupSummary = (TextBlock)mainWindow.FindName("txtStartupSummary");
                btnScanStartup.Click += (s, e) => ScanStartupAppsAsync();

                // Tab 5 Bindings
                chkAutoClean = (CheckBox)mainWindow.FindName("chkAutoClean");
                txtScheduleStatus = (TextBlock)mainWindow.FindName("txtScheduleStatus");
                btnCreateShortcut = (Button)mainWindow.FindName("btnCreateShortcut");
                btnTestSilentClean = (Button)mainWindow.FindName("btnTestSilentClean");
                btnCheckUpdates = (Button)mainWindow.FindName("btnCheckUpdates");
                btnOpenGitHub = (Button)mainWindow.FindName("btnOpenGitHub");
                txtSysSpecOs = (TextBlock)mainWindow.FindName("txtSysSpecOs");
                txtSysSpecCpu = (TextBlock)mainWindow.FindName("txtSysSpecCpu");
                txtSysSpecRam = (TextBlock)mainWindow.FindName("txtSysSpecRam");
                txtSysSpecGpu = (TextBlock)mainWindow.FindName("txtSysSpecGpu");

                chkAutoClean.Click += (s, e) => ToggleScheduler(chkAutoClean.IsChecked == true);
                btnCreateShortcut.Click += (s, e) => CreateDesktopShortcut();
                btnTestSilentClean.Click += (s, e) => {
                    Log("Running silent clean test run...");
                    Task.Run(new Action(() => {
                        PerformSilentCleanup();
                        mainWindow.Dispatcher.Invoke(new Action(() => {
                            Log("Silent clean test finished successfully.");
                            UpdateDiskMetrics();
                            RunScanAsync();
                        }));
                    }));
                };
                btnCheckUpdates.Click += (s, e) => CheckForUpdatesAsync(true);
                btnOpenGitHub.Click += (s, e) => {
                    try { Process.Start(REPO_URL); } catch { }
                };

                // Initialize System Info, Drives, Categories
                SwitchTab(0);
                PopulateDrives();
                InitCategoryDefinitions();
                DetectSystemHardware();
                CheckScheduledTask();
                CheckForUpdatesAsync(false);
                StartUptimeTimer();

                Log("OmniClean v1.0.0 PRO initialized.");
                Log(string.Format("Host OS: {0} ({1}-bit)", Environment.OSVersion, Environment.Is64BitOperatingSystem ? "64" : "32"));
                Log("Select cleanable targets and click 'Clean Selected Storage'.");

                UpdateDiskMetrics();
                RunScanAsync();

                app.Run(mainWindow);
            } catch (Exception ex) {
                MessageBox.Show("Fatal startup error:\r\n" + ex.ToString(), "OmniClean Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        static void SwitchTab(int index) {
            mainTabControl.SelectedIndex = index;

            SetNavState(btnNavCleaner, barNavCleaner, index == 0);
            SetNavState(btnNavLargeFiles, barNavLargeFiles, index == 1);
            SetNavState(btnNavDuplicates, barNavDuplicates, index == 2);
            SetNavState(btnNavStartup, barNavStartup, index == 3);
            SetNavState(btnNavSettings, barNavSettings, index == 4);

            if (index == 1 && allLargeFiles.Count == 0) {
                ScanLargeFilesAsync();
            } else if (index == 2 && duplicateGroups.Count == 0) {
                ScanDuplicatesAsync();
            } else if (index == 3 && startupApps.Count == 0) {
                ScanStartupAppsAsync();
            }
        }

        static void SetNavState(Button btn, Border bar, bool isActive) {
            if (btn == null || bar == null) return;
            bar.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
            btn.Background = isActive ? new SolidColorBrush(Color.FromRgb(0x1B, 0x25, 0x3B)) : Brushes.Transparent;
            btn.Foreground = isActive ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
            btn.FontWeight = isActive ? FontWeights.Bold : FontWeights.SemiBold;
        }

        static void StartUptimeTimer() {
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(5);
            timer.Tick += (s, e) => {
                try {
                    TimeSpan uptime = TimeSpan.FromMilliseconds(Environment.TickCount);
                    string upStr = string.Format("Uptime: {0}d {1}h {2}m", uptime.Days, uptime.Hours, uptime.Minutes);
                    txtHeaderUptimePill.Text = upStr;
                    txtSidebarUptime.Text = upStr;
                    RefreshRamMetrics();
                } catch { }
            };
            timer.Start();
        }

        static void RefreshRamMetrics() {
            try {
                MEMORYSTATUSEX mem = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(mem)) {
                    double totalGB = mem.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                    double freeGB = mem.ullAvailPhys / (1024.0 * 1024.0 * 1024.0);
                    double usedGB = totalGB - freeGB;
                    uint pct = mem.dwMemoryLoad;

                    if (barSidebarRam != null) barSidebarRam.Value = pct;
                    if (txtSidebarRam != null) txtSidebarRam.Text = string.Format("RAM: {0:F1} / {1:F1} GB ({2}%)", usedGB, totalGB, pct);
                }
            } catch { }
        }

        static void Log(string msg) {
            if (txtConsole == null) return;
            mainWindow.Dispatcher.Invoke(new Action(() => {
                string time = DateTime.Now.ToString("HH:mm:ss");
                txtConsole.AppendText(string.Format("[{0}] {1}\r\n", time, msg));
                txtConsole.ScrollToEnd();
            }));
        }

        static void ExportLog() {
            try {
                var sfd = new SaveFileDialog();
                sfd.FileName = string.Format("OmniClean_Log_{0:yyyyMMdd_HHmmss}.txt", DateTime.Now);
                sfd.Filter = "Text Documents (*.txt)|*.txt|All Files (*.*)|*.*";
                if (sfd.ShowDialog() == true) {
                    File.WriteAllText(sfd.FileName, txtConsole.Text);
                    Log("Activity log exported to " + sfd.FileName);
                }
            } catch (Exception ex) {
                Log("Failed to export log: " + ex.Message);
            }
        }

        static void DetectSystemHardware() {
            Task.Run(new Action(() => {
                string cpu = "Generic CPU";
                string gpu = "Standard Graphics Adapter";
                string ramGB = "16";
                string osName = string.Format("Windows ({0}-bit)", Environment.Is64BitOperatingSystem ? "64" : "32");

                try {
                    using (var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0")) {
                        if (key != null) {
                            object val = key.GetValue("ProcessorNameString");
                            if (val != null) cpu = val.ToString().Trim();
                        }
                    }
                } catch { }

                try {
                    MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
                    if (GlobalMemoryStatusEx(memStatus)) {
                        double total = memStatus.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                        ramGB = string.Format("{0:F1}", total);
                    }
                } catch { }

                try {
                    using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000")) {
                        if (key != null) {
                            object val = key.GetValue("DriverDesc");
                            if (val != null) gpu = val.ToString().Trim();
                        }
                    }
                } catch { }

                try {
                    using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion")) {
                        if (key != null) {
                            object prod = key.GetValue("ProductName");
                            object build = key.GetValue("CurrentBuild");
                            if (prod != null) osName = string.Format("{0} (Build {1})", prod, build != null ? build.ToString() : "");
                        }
                    }
                } catch { }

                mainWindow.Dispatcher.Invoke(new Action(() => {
                    txtSidebarCpu.Text = "CPU: " + cpu;
                    RefreshRamMetrics();

                    txtSysSpecOs.Text = "OS: " + osName;
                    txtSysSpecCpu.Text = "Processor: " + cpu;
                    txtSysSpecRam.Text = "Physical Memory: " + ramGB + " GB";
                    txtSysSpecGpu.Text = "Graphics: " + gpu;
                }));
            }));
        }

        static void CheckForUpdatesAsync(bool userInitiated) {
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
                            bool isNew = !string.Equals(latestTag, CURRENT_VERSION, StringComparison.OrdinalIgnoreCase);

                            mainWindow.Dispatcher.Invoke(new Action(() => {
                                if (isNew) {
                                    bannerUpdate.Visibility = Visibility.Visible;
                                    txtUpdateNotice.Text = string.Format("Update Available: {0} (Current: {1})", latestTag, CURRENT_VERSION);
                                    if (userInitiated) {
                                        MessageBox.Show(mainWindow, string.Format("A new version ({0}) is available on GitHub!\r\nClick 'View Release' to download.", latestTag), "OmniClean Update", MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                } else if (userInitiated) {
                                    MessageBox.Show(mainWindow, string.Format("You are running the latest version of OmniClean ({0}).", CURRENT_VERSION), "OmniClean Up to Date", MessageBoxButton.OK, MessageBoxImage.Information);
                                }
                            }));
                        }
                    }
                } catch {
                    if (userInitiated) {
                        mainWindow.Dispatcher.Invoke(new Action(() => {
                            MessageBox.Show(mainWindow, "Unable to check for updates at this time. Please check your internet connection.", "OmniClean", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }));
                    }
                }
            }));
        }

        static void OptimizeRam() {
            Task.Run(new Action(() => {
                try {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    EmptyWorkingSet(Process.GetCurrentProcess().Handle);

                    int count = 0;
                    foreach (var proc in Process.GetProcesses()) {
                        try {
                            if (proc.Id != Process.GetCurrentProcess().Id) {
                                EmptyWorkingSet(proc.Handle);
                                count++;
                            }
                        } catch { }
                    }

                    mainWindow.Dispatcher.Invoke(new Action(() => {
                        Log(string.Format("RAM Optimizer: Working sets trimmed across {0} active processes.", count));
                        RefreshRamMetrics();
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

            AddCategory("recycle_bin", "Windows Recycle Bin", "All files and deleted items sent to the Recycle Bin", "🗑️", "#EF4444");
            AddCategory("temp_files", "System & User Temporary Files", "Windows and application temporary cache in %TEMP% and Windows\\Temp", "📁", "#3B82F6");
            AddCategory("browser_cache", "Web Browser Caches", "Chrome, Edge, Brave, Firefox, and Opera internet cache and shader data", "🌐", "#10B981");
            AddCategory("win_update", "Windows Update Download Cache", "Leftover update packages in SoftwareDistribution\\Download", "🔄", "#6366F1");
            AddCategory("crash_dumps", "Error Reports & Crash Dumps", "Windows Error Reporting (WER) and memory dump logs", "⚠️", "#F59E0B");
            AddCategory("empty_folders", "Empty Temporary Directories", "Abandoned 0-byte directories created by uninstalled applications", "📂", "#64748B");

            AddCategory("vscode_cache", "VS Code Caches & Crash Dumps", "Visual Studio Code installer packages and crash logs", "💻", "#38BDF8");
            AddCategory("dev_cache", "npm & pip Package Caches", "Node.js and Python package manager cache folders", "📦", "#EC4899");
            AddCategory("discord_cache", "Discord Client Cache", "Discord media, audio, and GPU shader caches", "💬", "#818CF8");
            AddCategory("capcut_old", "Outdated CapCut App Versions", "Older builds leftover in AppData\\Local\\CapCut\\Apps", "🎬", "#A855F7");
            AddCategory("nvidia_artifacts", "NVIDIA Driver Update Packages", "Downloaded GPU driver installers in NVIDIA App UpdateFramework", "🎮", "#10B981");
        }

        static void AddCategory(string id, string name, string desc, string icon, string iconBgHex) {
            Border card = new Border();
            card.Background = new SolidColorBrush(Color.FromRgb(0x0C, 0x12, 0x22));
            card.BorderBrush = new SolidColorBrush(Color.FromRgb(0x1B, 0x25, 0x3B));
            card.BorderThickness = new Thickness(1);
            card.Padding = new Thickness(14, 11, 14, 11);
            card.Margin = new Thickness(0, 0, 0, 7);
            card.CornerRadius = new CornerRadius(10);
            card.Cursor = Cursors.Hand;

            // Interactive hover highlight
            card.MouseEnter += (s, e) => {
                card.Background = new SolidColorBrush(Color.FromRgb(0x15, 0x1F, 0x34));
                card.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
            };
            card.MouseLeave += (s, e) => {
                card.Background = new SolidColorBrush(Color.FromRgb(0x0C, 0x12, 0x22));
                card.BorderBrush = new SolidColorBrush(Color.FromRgb(0x1B, 0x25, 0x3B));
            };

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            CheckBox chk = new CheckBox();
            chk.IsChecked = true;
            chk.VerticalAlignment = VerticalAlignment.Center;
            chk.Margin = new Thickness(0, 0, 12, 0);
            chk.Checked += (s, e) => UpdateSelectedSize();
            chk.Unchecked += (s, e) => UpdateSelectedSize();
            Grid.SetColumn(chk, 0);
            grid.Children.Add(chk);

            // Icon + Title & Desc
            StackPanel sp = new StackPanel();
            StackPanel titleRow = new StackPanel();
            titleRow.Orientation = Orientation.Horizontal;

            TextBlock iconBlock = new TextBlock();
            iconBlock.Text = icon;
            iconBlock.FontSize = 13;
            iconBlock.Margin = new Thickness(0, 0, 8, 0);
            iconBlock.VerticalAlignment = VerticalAlignment.Center;
            titleRow.Children.Add(iconBlock);

            TextBlock title = new TextBlock();
            title.Text = name;
            title.FontWeight = FontWeights.Bold;
            title.FontSize = 13;
            title.Foreground = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC));
            title.VerticalAlignment = VerticalAlignment.Center;
            titleRow.Children.Add(title);

            TextBlock txtDesc = new TextBlock();
            txtDesc.Text = desc;
            txtDesc.FontSize = 11;
            txtDesc.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
            txtDesc.Margin = new Thickness(0, 2, 0, 0);

            sp.Children.Add(titleRow);
            sp.Children.Add(txtDesc);
            Grid.SetColumn(sp, 1);
            grid.Children.Add(sp);

            Border badge = new Border();
            badge.Background = new SolidColorBrush(Color.FromRgb(0x16, 0x21, 0x36));
            badge.Padding = new Thickness(10, 4, 10, 4);
            badge.CornerRadius = new CornerRadius(6);
            badge.VerticalAlignment = VerticalAlignment.Center;

            TextBlock txtBadge = new TextBlock();
            txtBadge.Text = "Scanning...";
            txtBadge.FontSize = 11;
            txtBadge.FontWeight = FontWeights.Bold;
            txtBadge.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
            badge.Child = txtBadge;

            Grid.SetColumn(badge, 2);
            grid.Children.Add(badge);

            card.Child = grid;
            pnlCategories.Children.Add(card);

            categories.Add(new CategoryItem {
                Id = id,
                Name = name,
                Description = desc,
                IconText = icon,
                IconBgHex = iconBgHex,
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

        static void SetSafeCheckboxes() {
            foreach (var c in categories) {
                if (c.IsInstalled) {
                    // Safe defaults exclude crash dumps or empty folders if user prefers
                    c.CheckBoxRef.IsChecked = (c.Id != "empty_folders");
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
                MessageBox.Show(mainWindow, "Desktop shortcut successfully created!", "OmniClean", MessageBoxButton.OK, MessageBoxImage.Information);
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

        static void RunScanAsync() {
            if (isScanning || isCleaning) return;
            isScanning = true;
            btnRescan.IsEnabled = false;
            btnClean.IsEnabled = false;
            bannerSuccess.Visibility = Visibility.Collapsed;

            foreach (var c in categories) {
                c.BadgeRef.Text = "...";
                c.BadgeRef.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
            }

            Log(string.Format("Scanning storage volume {0}: for cleanable caches...", selectedDriveLetter));

            Task.Run(new Action(() => {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

                foreach (var cat in categories) {
                    double size = 0;
                    bool installed = true;

                    try {
                        if (cat.Id == "recycle_bin") {
                            size = 0;
                            try {
                                string binPath = selectedDriveLetter + @":\$Recycle.Bin";
                                size = GetDirectorySizeMB(binPath);
                            } catch { }
                        } else if (cat.Id == "temp_files") {
                            size += GetDirectorySizeMB(Path.GetTempPath());
                            size += GetDirectorySizeMB(Path.Combine(winDir, "Temp"));
                        } else if (cat.Id == "browser_cache") {
                            size += GetDirectorySizeMB(Path.Combine(localApp, @"Google\Chrome\User Data\Default\Cache"));
                            size += GetDirectorySizeMB(Path.Combine(localApp, @"Microsoft\Edge\User Data\Default\Cache"));
                            size += GetDirectorySizeMB(Path.Combine(localApp, @"BraveSoftware\Brave-Browser\User Data\Default\Cache"));
                            size += GetDirectorySizeMB(Path.Combine(localApp, @"Mozilla\Firefox\Profiles"));
                            size += GetDirectorySizeMB(Path.Combine(localApp, @"Opera Software\Opera Stable\Cache"));
                        } else if (cat.Id == "win_update") {
                            size = GetDirectorySizeMB(Path.Combine(winDir, @"SoftwareDistribution\Download"));
                        } else if (cat.Id == "crash_dumps") {
                            size += GetDirectorySizeMB(Path.Combine(localApp, "CrashDumps"));
                            size += GetDirectorySizeMB(Path.Combine(progData, @"Microsoft\Windows\WER\ReportQueue"));
                            size += GetDirectorySizeMB(Path.Combine(progData, @"Microsoft\Windows\WER\ReportArchive"));
                        } else if (cat.Id == "empty_folders") {
                            size = 0.5;
                        } else if (cat.Id == "nvidia_artifacts") {
                            string ota = Path.Combine(progData, @"NVIDIA Corporation\NVIDIA app\UpdateFramework\ota-artifacts");
                            if (Directory.Exists(ota)) {
                                size = GetDirectorySizeMB(ota);
                            } else {
                                installed = false;
                            }
                        } else if (cat.Id == "capcut_old") {
                            string capApps = Path.Combine(localApp, @"CapCut\Apps");
                            if (Directory.Exists(capApps)) {
                                var dirs = new List<string>(Directory.GetDirectories(capApps));
                                if (dirs.Count > 1) {
                                    dirs.Sort();
                                    string latest = dirs[dirs.Count - 1];
                                    foreach (var d in dirs) {
                                        if (d != latest) size += GetDirectorySizeMB(d);
                                    }
                                }
                            } else {
                                installed = false;
                            }
                        } else if (cat.Id == "vscode_cache") {
                            string vscPath = Path.Combine(appData, "Code");
                            if (Directory.Exists(vscPath)) {
                                size += GetDirectorySizeMB(Path.Combine(vscPath, "CachedExtensionVSIXs"));
                                size += GetDirectorySizeMB(Path.Combine(vscPath, @"Crashpad\reports"));
                                size += GetDirectorySizeMB(Path.Combine(vscPath, "CachedData"));
                            } else {
                                installed = false;
                            }
                        } else if (cat.Id == "dev_cache") {
                            size += GetDirectorySizeMB(Path.Combine(localApp, "npm-cache"));
                            size += GetDirectorySizeMB(Path.Combine(localApp, @"pip\cache"));
                        } else if (cat.Id == "discord_cache") {
                            string dsc = Path.Combine(appData, @"discord\Cache");
                            if (Directory.Exists(dsc)) {
                                size = GetDirectorySizeMB(dsc);
                            } else {
                                installed = false;
                            }
                        }
                    } catch { }

                    cat.SizeMB = size;
                    cat.IsInstalled = installed;

                    mainWindow.Dispatcher.Invoke(new Action(() => {
                        if (!installed) {
                            cat.BadgeRef.Text = "Not Detected";
                            cat.BadgeRef.Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));
                            cat.CheckBoxRef.IsEnabled = false;
                            cat.CheckBoxRef.IsChecked = false;
                        } else if (size > 1024) {
                            cat.BadgeRef.Text = string.Format("{0:F2} GB", size / 1024.0);
                            cat.BadgeRef.Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0x84, 0xFC));
                        } else if (size > 0.05) {
                            cat.BadgeRef.Text = string.Format("{0:F1} MB", size);
                            cat.BadgeRef.Foreground = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8));
                        } else {
                            cat.BadgeRef.Text = "Clean";
                            cat.BadgeRef.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                        }
                    }));
                }

                mainWindow.Dispatcher.Invoke(new Action(() => {
                    isScanning = false;
                    btnRescan.IsEnabled = true;
                    UpdateSelectedSize();
                    Log("Scan completed.");
                }));
            }));
        }

        static void StartCleanAsync() {
            if (isCleaning || isScanning) return;
            isCleaning = true;
            btnClean.IsEnabled = false;
            btnClean.Content = "Cleaning in Progress...";
            bannerSuccess.Visibility = Visibility.Collapsed;

            bool isDryRun = (chkDryRun.IsChecked == true);
            Log(isDryRun ? "Starting Safe Dry Run simulation (Preview Mode)..." : "Starting clean on selected targets...");

            Task.Run(new Action(() => {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

                foreach (var cat in categories) {
                    if (cat.CheckBoxRef.IsChecked != true || !cat.IsInstalled) continue;

                    Log((isDryRun ? "[Simulate] " : "[Purging] ") + cat.Name);

                    try {
                        if (cat.Id == "recycle_bin") {
                            if (!isDryRun) SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                        } else if (cat.Id == "temp_files") {
                            CleanFolder(Path.GetTempPath(), isDryRun);
                            CleanFolder(Path.Combine(winDir, "Temp"), isDryRun);
                        } else if (cat.Id == "browser_cache") {
                            CleanBrowserCaches(isDryRun);
                        } else if (cat.Id == "win_update") {
                            CleanFolder(Path.Combine(winDir, @"SoftwareDistribution\Download"), isDryRun);
                        } else if (cat.Id == "crash_dumps") {
                            CleanFolder(Path.Combine(localApp, "CrashDumps"), isDryRun);
                            CleanFolder(Path.Combine(progData, @"Microsoft\Windows\WER\ReportQueue"), isDryRun);
                            CleanFolder(Path.Combine(progData, @"Microsoft\Windows\WER\ReportArchive"), isDryRun);
                        } else if (cat.Id == "empty_folders") {
                            CleanEmptyDirectories(Path.GetTempPath(), isDryRun);
                        } else if (cat.Id == "nvidia_artifacts") {
                            CleanFolder(Path.Combine(progData, @"NVIDIA Corporation\NVIDIA app\UpdateFramework\ota-artifacts"), isDryRun);
                        } else if (cat.Id == "capcut_old") {
                            string capApps = Path.Combine(localApp, @"CapCut\Apps");
                            if (Directory.Exists(capApps)) {
                                var dirs = new List<string>(Directory.GetDirectories(capApps));
                                if (dirs.Count > 1) {
                                    dirs.Sort();
                                    string latest = dirs[dirs.Count - 1];
                                    foreach (var d in dirs) {
                                        if (d != latest) {
                                            try { if (!isDryRun) Directory.Delete(d, true); } catch { }
                                        }
                                    }
                                }
                            }
                        } else if (cat.Id == "vscode_cache") {
                            string vscPath = Path.Combine(appData, "Code");
                            CleanFolder(Path.Combine(vscPath, "CachedExtensionVSIXs"), isDryRun);
                            CleanFolder(Path.Combine(vscPath, @"Crashpad\reports"), isDryRun);
                            CleanFolder(Path.Combine(vscPath, "CachedData"), isDryRun);
                        } else if (cat.Id == "dev_cache") {
                            CleanFolder(Path.Combine(localApp, "npm-cache"), isDryRun);
                            CleanFolder(Path.Combine(localApp, @"pip\cache"), isDryRun);
                        } else if (cat.Id == "discord_cache") {
                            CleanFolder(Path.Combine(appData, @"discord\Cache"), isDryRun);
                        }
                    } catch (Exception ex) {
                        Log("Warning: " + ex.Message);
                    }
                }

                mainWindow.Dispatcher.Invoke(new Action(() => {
                    isCleaning = false;
                    btnClean.Content = "⚡ Clean Selected Storage";
                    UpdateDiskMetrics();
                    Log(isDryRun ? "Simulation finished. No files were modified." : "Universal cleanup successfully finished.");
                    bannerSuccess.Visibility = Visibility.Visible;
                    txtSuccessMsg.Text = isDryRun ? "Simulation complete. Storage preview calculated." : "Cleanup complete! Selected storage targets successfully purged.";
                    RunScanAsync();
                }));
            }));
        }

        static void CleanFolder(string path, bool isDryRun) {
            if (!Directory.Exists(path)) return;
            try {
                foreach (var f in Directory.GetFiles(path)) {
                    try { if (!isDryRun) File.Delete(f); } catch { }
                }
                foreach (var d in Directory.GetDirectories(path)) {
                    try { if (!isDryRun) Directory.Delete(d, true); } catch { }
                }
            } catch { }
        }

        static void CleanBrowserCaches(bool isDryRun) {
            string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] browserCaches = new string[] {
                Path.Combine(localApp, @"Google\Chrome\User Data\Default\Cache"),
                Path.Combine(localApp, @"Google\Chrome\User Data\Default\Code Cache"),
                Path.Combine(localApp, @"Google\Chrome\User Data\Default\GPUCache"),
                Path.Combine(localApp, @"Microsoft\Edge\User Data\Default\Cache"),
                Path.Combine(localApp, @"Microsoft\Edge\User Data\Default\Code Cache"),
                Path.Combine(localApp, @"Microsoft\Edge\User Data\Default\GPUCache"),
                Path.Combine(localApp, @"BraveSoftware\Brave-Browser\User Data\Default\Cache"),
                Path.Combine(localApp, @"Opera Software\Opera Stable\Cache")
            };

            foreach (var bPath in browserCaches) {
                CleanFolder(bPath, isDryRun);
            }
        }

        static void CleanEmptyDirectories(string startLocation, bool isDryRun) {
            try {
                foreach (var d in Directory.GetDirectories(startLocation)) {
                    CleanEmptyDirectories(d, isDryRun);
                    if (Directory.GetFiles(d).Length == 0 && Directory.GetDirectories(d).Length == 0) {
                        try { if (!isDryRun) Directory.Delete(d, false); } catch { }
                    }
                }
            } catch { }
        }

        // ================= TAB 1: LARGE FILES FINDER =================
        static void ScanLargeFilesAsync() {
            btnScanLargeFiles.IsEnabled = false;
            btnScanLargeFiles.Content = "Scanning Drive...";
            pnlLargeFiles.Children.Clear();
            txtLargeFilesSummary.Text = "Scanning user profile and common storage directories for files > 100 MB...";

            Task.Run(new Action(() => {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string[] searchDirs = new string[] {
                    Path.Combine(userProfile, "Downloads"),
                    Path.Combine(userProfile, "Documents"),
                    Path.Combine(userProfile, "Desktop"),
                    Path.Combine(userProfile, "Videos"),
                    Path.Combine(userProfile, "Music"),
                    Path.Combine(userProfile, "Pictures")
                };

                var found = new List<LargeFileInfo>();

                foreach (var dir in searchDirs) {
                    if (Directory.Exists(dir)) {
                        try {
                            foreach (var f in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)) {
                                try {
                                    FileInfo fi = new FileInfo(f);
                                    if (fi.Length > 100 * 1024 * 1024) { // > 100 MB
                                        found.Add(new LargeFileInfo {
                                            FilePath = fi.FullName,
                                            FileName = fi.Name,
                                            Extension = fi.Extension.ToUpper().Replace(".", ""),
                                            Bytes = fi.Length,
                                            SizeMB = fi.Length / (1024.0 * 1024.0)
                                        });
                                    }
                                } catch { }
                            }
                        } catch { }
                    }
                }

                found.Sort((a, b) => b.Bytes.CompareTo(a.Bytes));
                allLargeFiles = found;

                mainWindow.Dispatcher.Invoke(new Action(() => {
                    btnScanLargeFiles.IsEnabled = true;
                    btnScanLargeFiles.Content = "🔍 Scan for Large Files";
                    ApplyLargeFileFilter(currentMinLargeFileBytes);
                }));
            }));
        }

        static void ApplyLargeFileFilter(long minBytes) {
            currentMinLargeFileBytes = minBytes;
            pnlLargeFiles.Children.Clear();

            var filtered = allLargeFiles.FindAll(x => x.Bytes >= minBytes);
            txtLargeFilesSummary.Text = string.Format("Found {0} files matching size filter.", filtered.Count);

            if (filtered.Count == 0) {
                TextBlock empty = new TextBlock();
                empty.Text = "No large files matching this filter were found in scanned folders.";
                empty.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
                empty.Margin = new Thickness(0, 20, 0, 0);
                pnlLargeFiles.Children.Add(empty);
                return;
            }

            foreach (var item in filtered) {
                Border card = new Border();
                card.Background = new SolidColorBrush(Color.FromRgb(0x0C, 0x12, 0x22));
                card.BorderBrush = new SolidColorBrush(Color.FromRgb(0x1B, 0x25, 0x3B));
                card.BorderThickness = new Thickness(1);
                card.Padding = new Thickness(14, 10, 14, 10);
                card.Margin = new Thickness(0, 0, 0, 7);
                card.CornerRadius = new CornerRadius(10);

                card.MouseEnter += (s, e) => {
                    card.Background = new SolidColorBrush(Color.FromRgb(0x15, 0x1F, 0x34));
                    card.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
                };
                card.MouseLeave += (s, e) => {
                    card.Background = new SolidColorBrush(Color.FromRgb(0x0C, 0x12, 0x22));
                    card.BorderBrush = new SolidColorBrush(Color.FromRgb(0x1B, 0x25, 0x3B));
                };

                Grid grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

                // Extension Badge
                Border extBadge = new Border();
                extBadge.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1B, 0x4B));
                extBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0x63, 0x66, 0xF1));
                extBadge.BorderThickness = new Thickness(1);
                extBadge.CornerRadius = new CornerRadius(6);
                extBadge.Padding = new Thickness(6, 4, 6, 4);
                extBadge.Margin = new Thickness(0, 0, 12, 0);
                extBadge.VerticalAlignment = VerticalAlignment.Center;

                TextBlock txtExt = new TextBlock();
                txtExt.Text = string.IsNullOrEmpty(item.Extension) ? "FILE" : item.Extension;
                txtExt.FontSize = 10;
                txtExt.FontWeight = FontWeights.Bold;
                txtExt.Foreground = new SolidColorBrush(Color.FromRgb(0xA5, 0xB4, 0xFC));
                extBadge.Child = txtExt;

                Grid.SetColumn(extBadge, 0);
                grid.Children.Add(extBadge);

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
                path.MaxWidth = 550;

                sp.Children.Add(name);
                sp.Children.Add(path);
                Grid.SetColumn(sp, 1);
                grid.Children.Add(sp);

                StackPanel rightSp = new StackPanel();
                rightSp.Orientation = Orientation.Horizontal;
                rightSp.VerticalAlignment = VerticalAlignment.Center;

                Border sizeBadge = new Border();
                sizeBadge.Background = new SolidColorBrush(Color.FromRgb(0x23, 0x1B, 0x4D));
                sizeBadge.Padding = new Thickness(10, 4, 10, 4);
                sizeBadge.CornerRadius = new CornerRadius(6);
                sizeBadge.Margin = new Thickness(0, 0, 8, 0);

                TextBlock txtSize = new TextBlock();
                txtSize.Text = item.SizeMB > 1024 ? string.Format("{0:F2} GB", item.SizeMB / 1024.0) : string.Format("{0:F0} MB", item.SizeMB);
                txtSize.Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0x84, 0xFC));
                txtSize.FontWeight = FontWeights.Bold;
                txtSize.FontSize = 11;
                sizeBadge.Child = txtSize;

                Button btnShow = new Button();
                btnShow.Content = "Open Folder";
                btnShow.Background = new SolidColorBrush(Color.FromRgb(0x16, 0x20, 0x32));
                btnShow.Foreground = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));
                btnShow.BorderThickness = new Thickness(1);
                btnShow.BorderBrush = new SolidColorBrush(Color.FromRgb(0x22, 0x2F, 0x48));
                btnShow.Padding = new Thickness(10, 4, 10, 4);
                btnShow.FontSize = 11;
                btnShow.Cursor = Cursors.Hand;
                btnShow.Margin = new Thickness(0, 0, 6, 0);
                btnShow.Click += (s, e) => {
                    try { Process.Start("explorer.exe", string.Format("/select,\"{0}\"", item.FilePath)); } catch { }
                };

                Button btnDeleteFile = new Button();
                btnDeleteFile.Content = "Delete";
                btnDeleteFile.Background = new SolidColorBrush(Color.FromRgb(0x3B, 0x18, 0x22));
                btnDeleteFile.Foreground = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));
                btnDeleteFile.BorderThickness = new Thickness(0);
                btnDeleteFile.Padding = new Thickness(10, 4, 10, 4);
                btnDeleteFile.FontSize = 11;
                btnDeleteFile.Cursor = Cursors.Hand;
                btnDeleteFile.Click += (s, e) => {
                    if (MessageBox.Show(mainWindow, "Are you sure you want to permanently delete this file?\r\n" + item.FilePath, "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) {
                        try {
                            File.Delete(item.FilePath);
                            card.Visibility = Visibility.Collapsed;
                            allLargeFiles.Remove(item);
                            Log("Deleted large file: " + item.FilePath);
                        } catch (Exception ex) {
                            Log("Delete error: " + ex.Message);
                        }
                    }
                };

                rightSp.Children.Add(sizeBadge);
                rightSp.Children.Add(btnShow);
                rightSp.Children.Add(btnDeleteFile);
                Grid.SetColumn(rightSp, 2);
                grid.Children.Add(rightSp);

                card.Child = grid;
                pnlLargeFiles.Children.Add(card);
            }
        }

        // ================= TAB 2: DUPLICATE FINDER =================
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
            txtDuplicatesSummary.Text = "Scanning Downloads, Documents, and Pictures for duplicate matches...";

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
                                    SizeMB = pair.Key / (1024.0 * 1024.0),
                                    FilePaths = hPair.Value
                                });
                            }
                        }
                    }
                }

                duplicateGroups = matchedGroups;

                mainWindow.Dispatcher.Invoke(new Action(() => {
                    btnScanDuplicates.IsEnabled = true;
                    btnScanDuplicates.Content = "🔎 Find Duplicates";

                    long totalWastedBytes = 0;
                    foreach (var g in matchedGroups) {
                        totalWastedBytes += g.Bytes * (g.FilePaths.Count - 1);
                    }
                    double wastedMB = totalWastedBytes / (1024.0 * 1024.0);
                    string wastedStr = wastedMB > 1024 ? string.Format("{0:F2} GB", wastedMB / 1024.0) : string.Format("{0:F1} MB", wastedMB);

                    txtDuplicatesSummary.Text = string.Format("Found {0} duplicate groups ({1} redundant storage can be recovered).", matchedGroups.Count, wastedStr);

                    if (matchedGroups.Count == 0) {
                        TextBlock empty = new TextBlock();
                        empty.Text = "Zero duplicate files found. Scanned directories are clean.";
                        empty.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
                        empty.Margin = new Thickness(0, 20, 0, 0);
                        pnlDuplicates.Children.Add(empty);
                        return;
                    }

                    foreach (var group in matchedGroups) {
                        Border groupBorder = new Border();
                        groupBorder.Background = new SolidColorBrush(Color.FromRgb(0x0C, 0x12, 0x22));
                        groupBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x1B, 0x25, 0x3B));
                        groupBorder.BorderThickness = new Thickness(1);
                        groupBorder.Padding = new Thickness(14);
                        groupBorder.Margin = new Thickness(0, 0, 0, 10);
                        groupBorder.CornerRadius = new CornerRadius(10);

                        StackPanel groupSp = new StackPanel();

                        Grid headerGrid = new Grid();
                        headerGrid.Margin = new Thickness(0, 0, 0, 8);
                        TextBlock grpTitle = new TextBlock();
                        grpTitle.Text = string.Format("Matched Size: {0:F1} MB • Hash: {1}...", group.SizeMB, group.Hash.Substring(0, Math.Min(12, group.Hash.Length)));
                        grpTitle.FontWeight = FontWeights.Bold;
                        grpTitle.FontSize = 12;
                        grpTitle.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
                        headerGrid.Children.Add(grpTitle);
                        groupSp.Children.Add(headerGrid);

                        for (int i = 0; i < group.FilePaths.Count; i++) {
                            string p = group.FilePaths[i];
                            Border itemCard = new Border();
                            itemCard.Background = new SolidColorBrush(Color.FromRgb(0x11, 0x18, 0x2B));
                            itemCard.Padding = new Thickness(10, 6, 10, 6);
                            itemCard.Margin = new Thickness(0, 0, 0, 4);
                            itemCard.CornerRadius = new CornerRadius(6);

                            Grid itemGrid = new Grid();
                            itemGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                            itemGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

                            TextBlock itemPath = new TextBlock();
                            itemPath.Text = (i == 0 ? "🛡️ [ORIGINAL] " : "📑 [COPY] ") + p;
                            itemPath.FontSize = 11;
                            itemPath.Foreground = (i == 0) ? new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99)) : new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
                            itemPath.TextTrimming = TextTrimming.CharacterEllipsis;
                            Grid.SetColumn(itemPath, 0);
                            itemGrid.Children.Add(itemPath);

                            Button btnDel = new Button();
                            btnDel.Content = (i == 0 ? "Reveal" : "Delete");
                            btnDel.Background = (i == 0) ? new SolidColorBrush(Color.FromRgb(0x16, 0x20, 0x32)) : new SolidColorBrush(Color.FromRgb(0x3B, 0x18, 0x22));
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
                                    if (MessageBox.Show(mainWindow, "Are you sure you want to delete this duplicate copy?\r\n" + capturedPath, "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) {
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

        static void CleanAllDuplicatesAsync() {
            if (duplicateGroups.Count == 0) {
                MessageBox.Show(mainWindow, "No duplicates have been discovered yet. Run 'Find Duplicates' first.", "OmniClean", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int count = 0;
            long bytes = 0;
            foreach (var g in duplicateGroups) {
                if (g.FilePaths.Count > 1) {
                    count += (g.FilePaths.Count - 1);
                    bytes += g.Bytes * (g.FilePaths.Count - 1);
                }
            }

            if (count == 0) {
                MessageBox.Show(mainWindow, "Zero duplicate copies available to remove.", "OmniClean", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            double mb = bytes / (1024.0 * 1024.0);
            string sizeStr = mb > 1024 ? string.Format("{0:F2} GB", mb / 1024.0) : string.Format("{0:F1} MB", mb);

            if (MessageBox.Show(mainWindow, string.Format("Safely purge {0} duplicate copies to reclaim {1}?\r\n\r\nOriginal files will be 100% preserved.", count, sizeStr), "Confirm Safe Duplicate Cleanup", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) {
                int deleted = 0;
                foreach (var g in duplicateGroups) {
                    for (int i = 1; i < g.FilePaths.Count; i++) {
                        try {
                            File.Delete(g.FilePaths[i]);
                            deleted++;
                        } catch { }
                    }
                }
                Log(string.Format("Safe Duplicate Cleanup: Removed {0} duplicate copies, reclaiming {1}.", deleted, sizeStr));
                ScanDuplicatesAsync();
            }
        }

        // ================= TAB 3: STARTUP APPS OPTIMIZER =================
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
                        card.Background = new SolidColorBrush(Color.FromRgb(0x0C, 0x12, 0x22));
                        card.BorderBrush = new SolidColorBrush(Color.FromRgb(0x1B, 0x25, 0x3B));
                        card.BorderThickness = new Thickness(1);
                        card.Padding = new Thickness(14, 10, 14, 10);
                        card.Margin = new Thickness(0, 0, 0, 8);
                        card.CornerRadius = new CornerRadius(10);

                        card.MouseEnter += (s, e) => {
                            card.Background = new SolidColorBrush(Color.FromRgb(0x15, 0x1F, 0x34));
                            card.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
                        };
                        card.MouseLeave += (s, e) => {
                            card.Background = new SolidColorBrush(Color.FromRgb(0x0C, 0x12, 0x22));
                            card.BorderBrush = new SolidColorBrush(Color.FromRgb(0x1B, 0x25, 0x3B));
                        };

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

                        Button btnToggle = new Button();
                        btnToggle.Content = "● ACTIVE";
                        btnToggle.Background = new SolidColorBrush(Color.FromRgb(0x06, 0x4E, 0x3B));
                        btnToggle.Foreground = new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99));
                        btnToggle.BorderThickness = new Thickness(1);
                        btnToggle.BorderBrush = new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69));
                        btnToggle.Padding = new Thickness(14, 5, 14, 5);
                        btnToggle.FontSize = 11;
                        btnToggle.FontWeight = FontWeights.Bold;
                        btnToggle.Cursor = Cursors.Hand;

                        StartupAppItem capturedApp = app;
                        btnToggle.Click += (s, e) => {
                            capturedApp.IsEnabled = !capturedApp.IsEnabled;
                            if (capturedApp.IsEnabled) {
                                btnToggle.Content = "● ACTIVE";
                                btnToggle.Background = new SolidColorBrush(Color.FromRgb(0x06, 0x4E, 0x3B));
                                btnToggle.Foreground = new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99));
                                btnToggle.BorderBrush = new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69));
                                try {
                                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)) {
                                        key.SetValue(capturedApp.Name, capturedApp.Command);
                                    }
                                    Log("Enabled startup app: " + capturedApp.Name);
                                } catch (Exception ex) { Log("Toggle error: " + ex.Message); }
                            } else {
                                btnToggle.Content = "○ DISABLED";
                                btnToggle.Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x37));
                                btnToggle.Foreground = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF));
                                btnToggle.BorderBrush = new SolidColorBrush(Color.FromRgb(0x37, 0x41, 0x51));
                                try {
                                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)) {
                                        key.DeleteValue(capturedApp.Name, false);
                                    }
                                    Log("Disabled startup app: " + capturedApp.Name);
                                } catch (Exception ex) { Log("Toggle error: " + ex.Message); }
                            }
                        };

                        Grid.SetColumn(btnToggle, 1);
                        grid.Children.Add(btnToggle);

                        card.Child = grid;
                        pnlStartupApps.Children.Add(card);
                    }
                }));
            }));
        }

        // ================= HEADLESS SILENT CLEANUP =================
        static void PerformSilentCleanup() {
            try {
                SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                CleanFolder(Path.GetTempPath(), false);
                CleanFolder(Path.Combine(winDir, "Temp"), false);
                CleanBrowserCaches(false);
                CleanFolder(Path.Combine(winDir, @"SoftwareDistribution\Download"), false);

                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                CleanFolder(Path.Combine(localApp, "CrashDumps"), false);
                CleanFolder(Path.Combine(progData, @"Microsoft\Windows\WER\ReportQueue"), false);

                CleanFolder(Path.Combine(localApp, "npm-cache"), false);
                CleanFolder(Path.Combine(localApp, @"pip\cache"), false);

                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                CleanFolder(Path.Combine(appData, @"discord\Cache"), false);

                string vscPath = Path.Combine(appData, "Code");
                if (Directory.Exists(vscPath)) {
                    CleanFolder(Path.Combine(vscPath, "CachedExtensionVSIXs"), false);
                    CleanFolder(Path.Combine(vscPath, @"Crashpad\reports"), false);
                }
            } catch { }
        }
    }
}
