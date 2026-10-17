using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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

    class Program {
        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        static extern uint SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);
        const uint SHERB_NOCONFIRMATION = 0x00000001;
        const uint SHERB_NOPROGRESSUI   = 0x00000002;
        const uint SHERB_NOSOUND        = 0x00000004;

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

        const string TASK_NAME = "OmniCleanUniversalAutoClean";

        static Window mainWindow;
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
        static Button btnSelectAll;
        static Button btnClearAll;
        static CheckBox chkAutoClean;
        static TextBlock txtScheduleStatus;
        static TextBox txtConsole;
        static StackPanel pnlCategories;

        static List<CategoryItem> categories = new List<CategoryItem>();
        static bool isScanning = false;
        static bool isCleaning = false;
        static string selectedDriveLetter = "C";

        [STAThread]
        static void Main(string[] args) {
            // If executed by Task Scheduler with --silent-clean, run silent cleanup and exit
            if (args != null && args.Length > 0 && args[0].Equals("--silent-clean", StringComparison.OrdinalIgnoreCase)) {
                PerformSilentCleanup();
                return;
            }

            try {
                Application app = new Application();

                string xaml = @"
<Window xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
        xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
        Title='OmniClean - Universal Windows Storage Optimizer'
        Width='1080' Height='740'
        MinWidth='960' MinHeight='660'
        WindowStartupLocation='CenterScreen'
        Background='#0A0E1A'
        Foreground='#F8FAFC'
        FontFamily='Segoe UI'>
    <Window.Resources>
        <Style TargetType='Border'>
            <Setter Property='CornerRadius' Value='16'/>
        </Style>
        <Style TargetType='CheckBox'>
            <Setter Property='Foreground' Value='#F1F5F9'/>
            <Setter Property='FontSize' Value='14'/>
            <Setter Property='VerticalContentAlignment' Value='Center'/>
            <Setter Property='Cursor' Value='Hand'/>
        </Style>
    </Window.Resources>

    <Grid Margin='24'>
        <Grid.RowDefinitions>
            <RowDefinition Height='Auto'/>
            <RowDefinition Height='*'/>
        </Grid.RowDefinitions>

        <!-- Header -->
        <Border Grid.Row='0' Background='#141B2D' BorderBrush='#232D47' BorderThickness='1' Padding='18,14' Margin='0,0,0,20'>
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width='Auto'/>
                    <ColumnDefinition Width='*'/>
                    <ColumnDefinition Width='Auto'/>
                </Grid.ColumnDefinitions>

                <StackPanel Grid.Column='0' Orientation='Horizontal'>
                    <Border Width='42' Height='42' Background='#6366F1' CornerRadius='10' Margin='0,0,14,0'>
                        <TextBlock Text='⚡' FontSize='22' Foreground='White' HorizontalAlignment='Center' VerticalAlignment='Center'/>
                    </Border>
                    <StackPanel VerticalAlignment='Center'>
                        <StackPanel Orientation='Horizontal'>
                            <TextBlock Text='OmniClean' FontSize='21' FontWeight='Bold' Foreground='#FFFFFF'/>
                            <Border Background='#262054' BorderBrush='#6366F1' BorderThickness='1' CornerRadius='8' Padding='8,2' Margin='10,0,0,0'>
                                <TextBlock Text='UNIVERSAL PRO' FontSize='10' FontWeight='Bold' Foreground='#A5B4FC'/>
                            </Border>
                        </StackPanel>
                        <TextBlock Text='Works on any Windows PC • Dynamic Hardware &amp; Storage Engine' FontSize='12' Foreground='#94A3B8'/>
                    </StackPanel>
                </StackPanel>

                <Border Grid.Column='1' HorizontalAlignment='Center' Background='#0E1424' BorderBrush='#1E293B' BorderThickness='1' CornerRadius='20' Padding='16,6'>
                    <StackPanel Orientation='Horizontal'>
                        <Ellipse Width='8' Height='8' Fill='#10B981' Margin='0,0,8,0'/>
                        <TextBlock Name='txtHardwarePill' Text='Detecting Hardware...' FontSize='12' Foreground='#94A3B8'/>
                    </StackPanel>
                </Border>

                <StackPanel Grid.Column='2' Orientation='Horizontal'>
                    <Button Name='btnRescan' Content='🔄 Rescan Storage' Background='#1E293B' Foreground='#E2E8F0' BorderBrush='#334155' BorderThickness='1' Padding='16,8' FontWeight='SemiBold' Cursor='Hand'/>
                </StackPanel>
            </Grid>
        </Border>

        <!-- Content Grid -->
        <Grid Grid.Row='1'>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width='380'/>
                <ColumnDefinition Width='*'/>
            </Grid.ColumnDefinitions>

            <!-- Left Panel -->
            <StackPanel Grid.Column='0' Margin='0,0,20,0'>
                <!-- Drive Capacity Card -->
                <Border Background='#141B2D' BorderBrush='#232D47' BorderThickness='1' Padding='20' Margin='0,0,0,16'>
                    <StackPanel>
                        <Grid Margin='0,0,0,14'>
                            <StackPanel Orientation='Horizontal'>
                                <TextBlock Text='Drive:' FontSize='15' FontWeight='Bold' Foreground='White' VerticalAlignment='Center' Margin='0,0,8,0'/>
                                <ComboBox Name='cmbDrive' Width='110' Height='28' Background='#0E1424' Foreground='#F8FAFC' BorderBrush='#334155'/>
                            </StackPanel>
                            <Border HorizontalAlignment='Right' Background='#0E2A36' BorderBrush='#06B6D4' BorderThickness='1' CornerRadius='6' Padding='6,2'>
                                <TextBlock Text='System Drive' FontSize='10' FontWeight='Bold' Foreground='#22D3EE'/>
                            </Border>
                        </Grid>

                        <TextBlock Name='txtDiskPercent' Text='--% Free' FontSize='32' FontWeight='Bold' Foreground='#10B981' Margin='0,0,0,4'/>
                        <TextBlock Name='txtDiskRatio' Text='-- GB Free / -- GB' FontSize='13' Foreground='#94A3B8' Margin='0,0,0,12'/>

                        <ProgressBar Name='diskProgress' Height='14' Value='30' Maximum='100' Foreground='#10B981' Background='#0A0F1D' BorderBrush='#232D47' Margin='0,0,0,16'/>

                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width='*'/>
                                <ColumnDefinition Width='*'/>
                                <ColumnDefinition Width='*'/>
                            </Grid.ColumnDefinitions>
                            <Border Grid.Column='0' Background='#0E1424' Padding='8' Margin='0,0,4,0' CornerRadius='8'>
                                <StackPanel HorizontalAlignment='Center'>
                                    <TextBlock Text='Free' FontSize='11' Foreground='#64748B'/>
                                    <TextBlock Name='txtStatFree' Text='-- GB' FontSize='13' FontWeight='Bold' Foreground='#10B981'/>
                                </StackPanel>
                            </Border>
                            <Border Grid.Column='1' Background='#0E1424' Padding='8' Margin='2,0,2,0' CornerRadius='8'>
                                <StackPanel HorizontalAlignment='Center'>
                                    <TextBlock Text='Used' FontSize='11' Foreground='#64748B'/>
                                    <TextBlock Name='txtStatUsed' Text='-- GB' FontSize='13' FontWeight='Bold' Foreground='#818CF8'/>
                                </StackPanel>
                            </Border>
                            <Border Grid.Column='2' Background='#0E1424' Padding='8' Margin='4,0,0,0' CornerRadius='8'>
                                <StackPanel HorizontalAlignment='Center'>
                                    <TextBlock Text='Total' FontSize='11' Foreground='#64748B'/>
                                    <TextBlock Name='txtStatTotal' Text='-- GB' FontSize='13' FontWeight='Bold' Foreground='#38BDF8'/>
                                </StackPanel>
                            </Border>
                        </Grid>
                    </StackPanel>
                </Border>

                <!-- Automatic Scheduler Card -->
                <Border Background='#141B2D' BorderBrush='#232D47' BorderThickness='1' Padding='20'>
                    <StackPanel>
                        <Grid Margin='0,0,0,8'>
                            <StackPanel Orientation='Horizontal'>
                                <TextBlock Text='⏰' FontSize='16' Margin='0,0,8,0'/>
                                <TextBlock Text='Auto-Clean Schedule' FontSize='15' FontWeight='Bold' Foreground='White'/>
                            </StackPanel>
                        </Grid>
                        <TextBlock Text='Runs silently in the background every Sunday at 12:00 PM via Windows Task Scheduler. Cleans Recycle Bin, temp files, and caches.' FontSize='12' Foreground='#94A3B8' TextWrapping='Wrap' Margin='0,0,0,12'/>
                        <CheckBox Name='chkAutoClean' Content='Enable Weekly Silent Auto-Clean' Margin='0,0,0,8'/>
                        <Border Background='#0A0E1A' Padding='8,6' CornerRadius='6'>
                            <TextBlock Name='txtScheduleStatus' Text='Status: Checking...' FontSize='11' Foreground='#64748B'/>
                        </Border>
                    </StackPanel>
                </Border>
            </StackPanel>

            <!-- Right Panel -->
            <Grid Grid.Column='1'>
                <Grid.RowDefinitions>
                    <RowDefinition Height='*'/>
                    <RowDefinition Height='160'/>
                </Grid.RowDefinitions>

                <!-- Target Categories Card -->
                <Border Grid.Row='0' Background='#141B2D' BorderBrush='#232D47' BorderThickness='1' Padding='20' Margin='0,0,0,16'>
                    <Grid>
                        <Grid.RowDefinitions>
                            <RowDefinition Height='Auto'/>
                            <RowDefinition Height='*'/>
                            <RowDefinition Height='Auto'/>
                        </Grid.RowDefinitions>

                        <!-- Category Header -->
                        <Grid Grid.Row='0' Margin='0,0,0,12'>
                            <StackPanel>
                                <TextBlock Text='Cleanable Targets' FontSize='17' FontWeight='Bold' Foreground='White'/>
                                <TextBlock Text='Safely detected on this PC' FontSize='12' Foreground='#94A3B8'/>
                            </StackPanel>
                            <StackPanel Orientation='Horizontal' HorizontalAlignment='Right'>
                                <Button Name='btnSelectAll' Content='Select All' Background='#1E293B' Foreground='#CBD5E1' BorderBrush='#334155' BorderThickness='1' Padding='10,4' FontSize='11' Margin='0,0,6,0' Cursor='Hand'/>
                                <Button Name='btnClearAll' Content='Clear' Background='#1E293B' Foreground='#CBD5E1' BorderBrush='#334155' BorderThickness='1' Padding='10,4' FontSize='11' Cursor='Hand'/>
                            </StackPanel>
                        </Grid>

                        <!-- Scrollable Category List -->
                        <ScrollViewer Grid.Row='1' VerticalScrollBarVisibility='Auto' Margin='0,0,0,12'>
                            <StackPanel Name='pnlCategories'/>
                        </ScrollViewer>

                        <!-- Action Bar -->
                        <Border Grid.Row='2' BorderBrush='#232D47' BorderThickness='0,1,0,0' Padding='0,14,0,0'>
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

                <!-- Live Log Console -->
                <Border Grid.Row='1' Background='#0E1424' BorderBrush='#1E293B' BorderThickness='1' Padding='14'>
                    <Grid>
                        <Grid.RowDefinitions>
                            <RowDefinition Height='Auto'/>
                            <RowDefinition Height='*'/>
                        </Grid.RowDefinitions>
                        <StackPanel Grid.Row='0' Orientation='Horizontal' Margin='0,0,0,6'>
                            <Ellipse Width='7' Height='7' Fill='#06B6D4' Margin='0,0,6,0'/>
                            <TextBlock Text='Activity Log' FontSize='11' FontWeight='SemiBold' Foreground='#94A3B8'/>
                        </StackPanel>
                        <TextBox Grid.Row='1' Name='txtConsole' IsReadOnly='True' Background='Transparent' Foreground='#CBD5E1' BorderThickness='0' FontFamily='Consolas' FontSize='11' VerticalScrollBarVisibility='Auto' TextWrapping='Wrap'/>
                    </Grid>
                </Border>
            </Grid>
        </Grid>
    </Grid>
</Window>";

                mainWindow = (Window)XamlReader.Parse(xaml);

                // Bind UI Elements
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
                btnSelectAll = (Button)mainWindow.FindName("btnSelectAll");
                btnClearAll = (Button)mainWindow.FindName("btnClearAll");
                chkAutoClean = (CheckBox)mainWindow.FindName("chkAutoClean");
                txtScheduleStatus = (TextBlock)mainWindow.FindName("txtScheduleStatus");
                txtConsole = (TextBox)mainWindow.FindName("txtConsole");
                pnlCategories = (StackPanel)mainWindow.FindName("pnlCategories");

                // Initialize Hardware Info dynamically
                DetectHardware();

                // Initialize Drive List dynamically
                PopulateDrives();

                // Initialize Categories
                InitCategoryDefinitions();

                // Attach Events
                btnRescan.Click += (s, e) => { RunScanAsync(); };
                btnSelectAll.Click += (s, e) => { SetAllCheckboxes(true); };
                btnClearAll.Click += (s, e) => { SetAllCheckboxes(false); };
                btnClean.Click += (s, e) => { RunCleanAsync(); };

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

                // Initial Startup
                Log("OmniClean Universal Engine initialized.");
                UpdateDiskMetrics();
                CheckScheduledTask();
                RunScanAsync();

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
                            // Simplify common CPU names
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

            // Universal Categories (Available on all Windows PCs)
            AddCategory("recycle_bin", "🗑️ Windows Recycle Bin", "All files and deleted items sent to the Recycle Bin");
            AddCategory("temp_files", "🧹 System & User Temporary Files", "Windows and application temporary cache in %TEMP% and Windows\\Temp");
            AddCategory("browser_cache", "🌐 Web Browser Caches", "Chrome, Edge, Brave, Firefox, and Opera internet cache and shader data");
            AddCategory("win_update", "🪟 Windows Update Download Cache", "Leftover update packages in SoftwareDistribution\\Download");
            AddCategory("crash_dumps", "⚠️ Error Reports & Crash Dumps", "Windows Error Reporting (WER) and memory dump logs");

            // Developer / Gamer / Productivity Caches (Dynamically enabled if installed)
            AddCategory("vscode_cache", "💻 VS Code Caches & Crash Dumps", "Visual Studio Code installer packages and crash logs");
            AddCategory("dev_cache", "📦 npm & pip Package Caches", "Node.js and Python package manager cache folders");
            AddCategory("discord_cache", "💬 Discord Client Cache", "Discord media, audio, and GPU shader caches");
            AddCategory("capcut_old", "🎬 Outdated CapCut App Versions", "Older builds leftover in AppData\\Local\\CapCut\\Apps");
            AddCategory("nvidia_artifacts", "🎮 NVIDIA Driver Update Packages", "Downloaded GPU driver installers in NVIDIA App UpdateFramework");
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
                            // Chromium Cache
                            string c1 = Path.Combine(dir, "Cache");
                            if (Directory.Exists(c1)) total += GetDirectorySizeMB(c1);

                            string c2 = Path.Combine(dir, "Code Cache");
                            if (Directory.Exists(c2)) total += GetDirectorySizeMB(c2);

                            // Firefox cache2
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
            Log("Scanning universal cleanable targets...");

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
                            // Recycle bin measure
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
            btnClean.Content = "⏳ Cleaning Storage...";
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
                    btnClean.Content = "⚡ Clean Selected Storage";
                    UpdateDiskMetrics();
                    Log("Universal cleanup successfully finished!");
                    MessageBox.Show(mainWindow, "Cleanup completed successfully!\r\nStorage has been optimized on this PC.", "OmniClean Universal", MessageBoxButton.OK, MessageBoxImage.Information);
                    RunScanAsync();
                }));
            }));
        }

        static void PerformSilentCleanup() {
            try {
                // 1. Recycle Bin
                SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);

                // 2. Temp
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                CleanFolder(Path.GetTempPath());
                CleanFolder(Path.Combine(winDir, "Temp"));

                // 3. Browser Cache
                CleanBrowserCaches();

                // 4. Windows Update Cache
                CleanFolder(Path.Combine(winDir, @"SoftwareDistribution\Download"));

                // 5. Crash Dumps
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                CleanFolder(Path.Combine(localApp, "CrashDumps"));
                CleanFolder(Path.Combine(progData, @"Microsoft\Windows\WER\ReportQueue"));

                // 6. Dev Caches
                CleanFolder(Path.Combine(localApp, "npm-cache"));
                CleanFolder(Path.Combine(localApp, @"pip\cache"));

                // 7. Discord Cache
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                CleanFolder(Path.Combine(appData, @"discord\Cache"));

                // 8. VS Code Cache
                string vscPath = Path.Combine(appData, "Code");
                if (Directory.Exists(vscPath)) {
                    CleanFolder(Path.Combine(vscPath, "CachedExtensionVSIXs"));
                    CleanFolder(Path.Combine(vscPath, @"Crashpad\reports"));
                }
            } catch { }
        }
    }
}
