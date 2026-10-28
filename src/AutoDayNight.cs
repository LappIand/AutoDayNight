// AutoDayNight - Offline sunrise/sunset Windows theme switching for your own location
// 托盘常驻 / 开机自启 / 本地天文计算(不联网) / 极低资源占用
// 编译: csc /target:winexe /optimize+ /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AutoDayNight
{
    // ---------------- 程序入口(单实例) ----------------
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // 命令行模式在抢互斥体之前执行, 保证常驻运行时也能从外部切换主题
            if (args != null && args.Length > 0)
            {
                try { Cli.Run(args); }
                catch (Exception ex)
                {
                    string text = "Error: " + ex.Message;
                    try { Console.WriteLine(text); File.WriteAllText(Path.Combine(Path.GetTempPath(), "AutoDayNight.out"), text); } catch { }
                    Environment.ExitCode = 1;
                }
                return;
            }

            // 崩溃也留痕, 避免"后台悄悄没了"查无对证
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e)
            {
                Config.Log(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " UI线程异常: " + e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                Config.Log(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " 致命异常: " + e.ExceptionObject);
            };

            bool createdNew;
            using (System.Threading.Mutex mutex = new System.Threading.Mutex(true, "Local\\AutoDayNight_SingleInstance", out createdNew))
            {
                if (!createdNew)
                    return; // 已有实例在运行

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp());
                GC.KeepAlive(mutex);
            }
        }
    }

    // Configuration and solar scheduling are in AppConfig.cs and SolarSchedule.cs.

    // ---------------- 主题切换 ----------------
    internal static class ThemeSwitcher
    {
        private const string PersonalizePath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        public static bool IsCurrentlyDark()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(PersonalizePath, false))
                {
                    if (k == null) return false;
                    object v = k.GetValue("AppsUseLightTheme");
                    if (v == null) return false;
                    return Convert.ToInt32(v) == 0;
                }
            }
            catch { return false; }
        }

        public static void Apply(bool dark)
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(PersonalizePath))
            {
                if (k != null)
                {
                    k.SetValue("AppsUseLightTheme", dark ? 0 : 1, RegistryValueKind.DWord);
                    k.SetValue("SystemUsesLightTheme", dark ? 0 : 1, RegistryValueKind.DWord);
                }
            }
            Broadcast();
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, UIntPtr wParam,
            string lParam, uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);

        private static void Broadcast()
        {
            // 通知所有顶层窗口主题已变化(WM_SETTINGCHANGE / "ImmersiveColorSet"),
            // 任务栏/资源管理器/大部分应用会立即刷新, 无需重启 explorer
            UIntPtr result;
            SendMessageTimeout(new IntPtr(0xFFFF), 0x001A, UIntPtr.Zero,
                "ImmersiveColorSet", 0x0002 /*SMTO_ABORTIFHUNG*/, 5000, out result);
        }
    }

    // ---------------- 电池状态 ----------------
    internal static class Battery
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Status
        {
            public byte ACLineStatus;       // 0=用电池 1=接电源 255=未知
            public byte BatteryFlag;
            public byte BatteryLifePercent; // 0-100, 255=无电池
            public byte SystemStatusFlag;
            public int BatteryLifeTime;
            public int BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll")]
        private static extern bool GetSystemPowerStatus(out Status sps);

        /// <summary>返回电量百分比; 无电池(台式机)返回 -1。</summary>
        public static int Percent(out bool onAc)
        {
            Status s;
            onAc = false;
            if (!GetSystemPowerStatus(out s)) return -1;
            onAc = s.ACLineStatus == 1;
            if (s.BatteryLifePercent > 100 || s.ACLineStatus == 255 || (s.BatteryFlag & 128) != 0) return -1;
            return s.BatteryLifePercent;
        }

        public static string Describe()
        {
            bool onAc;
            int pct = Percent(out onAc);
            if (pct < 0) return Texts.Get("无电池或电源状态未知", "No battery or unknown power status");
            return Texts.Get("电池 ", "Battery ") + pct + "% · " + (onAc ? Texts.Get("已接电源", "AC connected") : Texts.Get("放电中", "On battery"));
        }
    }

    // ---------------- 开机自启(HKCU Run 键, 无需管理员) ----------------
    internal static class AutoStart
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "AutoDayNight";

        public static bool Enabled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                    return k != null && k.GetValue(ValueName) != null;
            }
            catch { return false; }
        }

        public static void Set(bool enable)
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                if (k == null) return;
                if (enable)
                    k.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"");
                else
                    k.DeleteValue(ValueName, false);
            }
        }
    }

    // ---------------- 命令行(测试/调试用) ----------------
    internal static class Cli
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        public static void Run(string[] args)
        {
            AttachConsole(-1); // 尽量把输出打到父控制台
            StringBuilder sb = new StringBuilder();
            string cmd = args[0].ToLowerInvariant();
            Config.Load();

            if (cmd == "--test-calc" || cmd == "--status" || cmd == "--calc")
            {
                bool custom = cmd == "--calc";
                TimeZoneInfo zone = Config.Zone;
                DateTime date = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).Date;
                double lat = Config.Lat, lon = Config.Lon;
                if (custom)
                {
                    if (args.Length != 5) throw new ArgumentException("Usage: --calc yyyy-MM-dd LAT LON WINDOWS_TIME_ZONE_ID");
                    date = DateTime.ParseExact(args[1], "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    lat = double.Parse(args[2], CultureInfo.InvariantCulture); lon = double.Parse(args[3], CultureInfo.InvariantCulture);
                    zone = TimeZoneInfo.FindSystemTimeZoneById(args[4]);
                }
                if (!custom && !Config.LocationConfigured) sb.AppendLine("Location not configured. Use tray Location settings or --location.");
                else
                {
                    SolarDay day = SolarSchedule.GetDay(date, lat, lon, zone);
                    sb.AppendLine("地点 / Location: " + (custom ? "Custom" : Config.City) + " (" + lat.ToString(CultureInfo.InvariantCulture) + ", " + lon.ToString(CultureInfo.InvariantCulture) + ")");
                    sb.AppendLine("时区 / Time zone: " + zone.Id);
                    sb.AppendLine("日期 / Date: " + date.ToString("yyyy-MM-dd"));
                    sb.AppendLine("日出 / Sunrise: " + (day.Sunrise.HasValue ? day.Sunrise.Value.ToString("HH:mm:ss") : "—"));
                    sb.AppendLine("日落 / Sunset: " + (day.Sunset.HasValue ? day.Sunset.Value.ToString("HH:mm:ss") : "—"));
                    sb.AppendLine("日照类型 / Day: " + day.Kind);
                    if (cmd == "--status")
                    {
                        bool desired = SolarSchedule.IsDark(DateTimeOffset.UtcNow, lat, lon, zone, Config.SunriseOffset, Config.SunsetOffset);
                        sb.AppendLine("当前主题: " + (ThemeSwitcher.IsCurrentlyDark() ? "深色 / Dark" : "浅色 / Light"));
                        sb.AppendLine("应有主题: " + (desired ? "深色 / Dark" : "浅色 / Light"));
                        DateTimeOffset nt; bool nd;
                        bool hasNext = SolarSchedule.NextTransition(DateTimeOffset.UtcNow, lat, lon, zone, Config.SunriseOffset, Config.SunsetOffset, out nt, out nd);
                        sb.AppendLine("下次切换: " + (hasNext ? TimeZoneInfo.ConvertTime(nt, zone).ToString("yyyy-MM-dd HH:mm") + (nd ? " -> Dark" : " -> Light") : "No event within 370 days"));
                        sb.AppendLine("自动切换: " + (Config.AutoRun ? "开 / On" : "关 / Off"));
                        sb.AppendLine("开机自启: " + (AutoStart.Enabled() ? "开 / On" : "关 / Off"));
                        sb.AppendLine("电源: " + Battery.Describe());
                        sb.AppendLine("低电量提醒: " + (Config.LowBattAlert ? "开 / On" : "关 / Off") + " (" + Config.LowBattPercent + "%, " + Config.LowBattGraceSec + "s)");
                    }
                }
            }
            else if (cmd == "--location")
            {
                Application.EnableVisualStyles();
                using (LocationForm form = new LocationForm()) form.ShowDialog();
                sb.AppendLine("Location settings closed. Restart the resident app if settings were changed through CLI.");
            }
            else if (cmd == "--set" && args.Length >= 2)
            {
                string m = args[1].ToLowerInvariant();
                bool d;
                if (m == "dark") d = true;
                else if (m == "light") d = false;
                else { sb.AppendLine("用法: --set dark|light"); d = ThemeSwitcher.IsCurrentlyDark(); m = null; }
                if (m != null)
                {
                    ThemeSwitcher.Apply(d);
                    Config.Log(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " 命令行 -> " + (d ? "深色" : "浅色"));
                    sb.AppendLine("已设置为: " + (d ? "深色" : "浅色"));
                    sb.AppendLine("回读验证: " + (ThemeSwitcher.IsCurrentlyDark() ? "深色" : "浅色"));
                }
            }
            else if (cmd == "--test-lowbatt")
            {
                // 测试模式: 弹出全屏低电量提醒, 但永不真正关机
                int pct = 15;
                int secs = Config.LowBattGraceSec;
                if (args.Length >= 2) { int t; if (int.TryParse(args[1], out t) && t > 0 && t <= 100) pct = t; }
                if (args.Length >= 3) { int t; if (int.TryParse(args[2], out t) && t > 0) secs = t; }
                sb.AppendLine("测试模式: 低电量弹窗 pct=" + pct + " 倒计时=" + secs + "秒 (不会真正关机)");
                try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "AutoDayNight.out"), sb.ToString(), Encoding.UTF8); }
                catch { }
                Application.EnableVisualStyles();
                try
                {
                    LowBatteryForm testForm = new LowBatteryForm(pct, Config.LowBattPercent, secs, true, null);
                    Application.Run(testForm);
                    sb.AppendLine("弹窗已关闭");
                }
                catch (Exception ex)
                {
                    sb.AppendLine("弹窗失败: " + ex);
                }
            }
            else if (cmd == "--dump-icons")
            {
                string dir = args.Length >= 2 ? args[1] : Path.Combine(Path.GetTempPath(), "adn-icons");
                Directory.CreateDirectory(dir);
                foreach (int s in new int[] { 16, 24, 32, 48, 128 })
                {
                    foreach (string kind in new string[] { "sun", "moon" })
                    {
                        using (Bitmap bmp = new Bitmap(s, s, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                        {
                            using (Graphics g = Graphics.FromImage(bmp))
                            {
                                g.SmoothingMode = SmoothingMode.AntiAlias;
                                g.Clear(kind == "sun"
                                    ? Color.FromArgb(255, 243, 243, 243)   // 模拟浅色任务栏
                                    : Color.FromArgb(255, 32, 32, 32));    // 模拟深色任务栏
                                if (kind == "sun") Icons.DrawSun(g, s); else Icons.DrawMoon(g, s);
                            }
                            bmp.Save(Path.Combine(dir, kind + "_" + s + ".png"), ImageFormat.Png);
                        }
                    }
                }
                sb.AppendLine("图标已导出到: " + dir);
            }
            else
            {
                sb.AppendLine("用法: AutoDayNight.exe [--test-calc | --status | --set dark|light | --test-lowbatt [pct] [seconds] | --dump-icons [dir]]");
            }

            string text = sb.ToString();
            try { Console.Write(text); }
            catch { }
            try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "AutoDayNight.out"), text, Encoding.UTF8); }
            catch { }
        }
    }

    // ---------------- 托盘常驻主程序 ----------------
    internal class TrayApp : ApplicationContext
    {
        private NotifyIcon _tray;
        private Timer _timer;
        private ToolStripMenuItem _miStatus1, _miStatus2, _miStatus3, _miStatus4;
        private ToolStripMenuItem _miToggle, _miAuto, _miAutoStart, _miLowBatt;
        private bool _lowBattArmed = true;   // 低电量提示单次上锁: 触发一次后需充电/接电源才重新武装
        private bool _battPromptOpen;        // 弹窗进行中, 防止重复弹
        private Icon _iconSun, _iconMoon;

        private bool _lastBoundaryDark;   // 上一个"日出/日落边界"时的目标状态
        private bool _cacheValid;
        private DateTime _cacheDate;
        private DateTime _sunrise, _sunset;
        private SolarDay _day;
        private string _zoneKey;
        private DateTimeOffset _nextUtc;
        private bool _hasNext, _nextDark, _scheduledDark;
        private volatile bool _refreshRequested;
        private bool _locationOpen;

        public TrayApp()
        {
            Config.Load();
            BuildTray();

            _timer = new Timer();
            _timer.Interval = 30 * 1000; // 30秒检查一次, 单次开销为几次字符串/注册表操作, CPU占用≈0
            _timer.Tick += OnTick;

            try { Evaluate(true); }
            catch (Exception ex) { Config.Log("启动错误: " + ex.Message); }

            _timer.Start();
            SystemEvents.TimeChanged += OnClockChanged;
            SystemEvents.PowerModeChanged += OnPowerChanged;
            Config.Log(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " Started v1.0.0");
            if (!Config.LocationConfigured)
            {
                Timer setup = new Timer { Interval = 200 };
                setup.Tick += delegate { setup.Stop(); setup.Dispose(); OnLocationClick(null, null); };
                setup.Start();
            }
        }

        private void OnClockChanged(object sender, EventArgs e) { _refreshRequested = true; }
        private void OnPowerChanged(object sender, PowerModeChangedEventArgs e)
        { if (e.Mode == PowerModes.Resume) _refreshRequested = true; }

        private void BuildTray()
        {
            _iconSun = Icons.Sun();
            _iconMoon = Icons.Moon();

            ContextMenuStrip menu = new ContextMenuStrip();
            _miStatus1 = new ToolStripMenuItem("当前：--"); _miStatus1.Enabled = false;
            _miStatus2 = new ToolStripMenuItem("今日日出 --:-- ｜ 日落 --:--"); _miStatus2.Enabled = false;
            _miStatus3 = new ToolStripMenuItem("下次切换 --"); _miStatus3.Enabled = false;
            _miStatus4 = new ToolStripMenuItem("电源: --"); _miStatus4.Enabled = false;
            _miToggle = new ToolStripMenuItem("立即切换", null, OnToggleClick);
            _miAuto = new ToolStripMenuItem("自动切换（日出/日落）", null, OnAutoClick);
            _miAutoStart = new ToolStripMenuItem("开机自启动", null, OnAutoStartClick);
            _miLowBatt = new ToolStripMenuItem("低电量关机提醒", null, OnLowBattClick);
            ToolStripMenuItem miExit = new ToolStripMenuItem(Texts.Get("退出", "Exit"), null, OnExitClick);

            menu.Items.Add(_miStatus1);
            menu.Items.Add(_miStatus2);
            menu.Items.Add(_miStatus3);
            menu.Items.Add(_miStatus4);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem(Texts.Get("所在地设置…", "Location settings…"), null, OnLocationClick));
            menu.Items.Add(_miToggle);
            menu.Items.Add(_miAuto);
            menu.Items.Add(_miAutoStart);
            menu.Items.Add(_miLowBatt);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miExit);

            _tray = new NotifyIcon();
            _tray.Icon = ThemeSwitcher.IsCurrentlyDark() ? _iconMoon : _iconSun;
            _tray.ContextMenuStrip = menu;
            _tray.Text = "日出浅色 · 日落深色";
            _tray.Visible = true;
        }

        private void OnTick(object sender, EventArgs e)
        {
            try { Evaluate(false); }
            catch (Exception ex) { Config.Log("运行错误: " + ex.Message); }
            try { CheckBattery(); }
            catch (Exception ex) { _battPromptOpen = false; Config.Log("电量检查错误: " + ex.Message); }
        }

        private void GetToday(out DateTime sr, out DateTime ss)
        {
            TimeZoneInfo zone = Config.Zone;
            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateTime date = TimeZoneInfo.ConvertTime(now, zone).Date;
            string key = zone.Id + "|" + zone.GetUtcOffset(now).Ticks;
            if (!_cacheValid || _cacheDate != date || key != _zoneKey || (_hasNext && now >= _nextUtc))
            {
                _day = SolarSchedule.GetDay(date, Config.Lat, Config.Lon, zone);
                _sunrise = _day.Sunrise.HasValue ? _day.Sunrise.Value.AddMinutes(Config.SunriseOffset) : DateTime.MinValue;
                _sunset = _day.Sunset.HasValue ? _day.Sunset.Value.AddMinutes(Config.SunsetOffset) : DateTime.MinValue;
                _scheduledDark = SolarSchedule.IsDark(now, Config.Lat, Config.Lon, zone, Config.SunriseOffset, Config.SunsetOffset);
                _hasNext = SolarSchedule.NextTransition(now, Config.Lat, Config.Lon, zone, Config.SunriseOffset, Config.SunsetOffset, out _nextUtc, out _nextDark);
                _cacheDate = date; _zoneKey = key; _cacheValid = true;
            }
            sr = _sunrise; ss = _sunset;
        }

        private void Evaluate(bool startup)
        {
            if (_refreshRequested)
            {
                _refreshRequested = false; TimeZoneInfo.ClearCachedData(); _cacheValid = false;
                startup = true;
            }
            if (!Config.LocationConfigured) { UpdateUi(DateTime.MinValue, DateTime.MinValue); return; }
            bool crossed = _hasNext && DateTimeOffset.UtcNow >= _nextUtc;
            DateTime sr, ss; GetToday(out sr, out ss);
            bool desiredDark = _scheduledDark;
            if (Config.AutoRun && (startup || crossed || desiredDark != _lastBoundaryDark)
                && ThemeSwitcher.IsCurrentlyDark() != desiredDark)
            {
                ThemeSwitcher.Apply(desiredDark);
                Config.Log(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + (startup ? " 启动同步 -> " : " 自动 -> ") + (desiredDark ? "深色" : "浅色"));
            }
            _lastBoundaryDark = desiredDark;
            UpdateUi(sr, ss);
        }

        private void UpdateUi(DateTime sr, DateTime ss)
        {
            bool nowDark = ThemeSwitcher.IsCurrentlyDark();
            _miStatus1.Text = Texts.Get("当前：", "Current: ") + (nowDark ? Texts.Get("深色", "Dark") : Texts.Get("浅色", "Light"))
                + (Config.AutoRun ? "" : Texts.Get("（已暂停）", " (paused)"));
            string next = "—";
            if (!Config.LocationConfigured)
            {
                _miStatus2.Text = Texts.Get("请先设置所在地", "Please configure your location");
                _miStatus3.Text = Texts.Get("日出日落切换尚未启用", "Solar switching awaits location setup");
            }
            else
            {
                _miStatus2.Text = Config.City + " ｜ " + Texts.Get("日出 ", "Sunrise ") + (sr == DateTime.MinValue ? "—" : sr.ToString("HH:mm"))
                    + " ｜ " + Texts.Get("日落 ", "Sunset ") + (ss == DateTime.MinValue ? "—" : ss.ToString("HH:mm"));
                if (_day.Kind != SolarDayKind.Normal) _miStatus2.Text += _day.Kind == SolarDayKind.PolarDay ? Texts.Get("（极昼）", " (polar day)") : Texts.Get("（极夜）", " (polar night)");
                next = _hasNext ? TimeZoneInfo.ConvertTime(_nextUtc, Config.Zone).ToString("MM-dd HH:mm")
                    + " → " + (_nextDark ? Texts.Get("深色", "Dark") : Texts.Get("浅色", "Light")) : Texts.Get("未来370天无边界", "No boundary in 370 days");
                _miStatus3.Text = Texts.Get("下次切换 ", "Next: ") + next;
            }
            _miStatus4.Text = Texts.Get("电源: ", "Power: ") + Battery.Describe();
            _miToggle.Text = Texts.Get("立即切换为", "Switch to ") + (nowDark ? Texts.Get("浅色", "Light") : Texts.Get("深色", "Dark"));
            _miAuto.Text = Texts.Get("自动切换（日出/日落）", "Automatic sunrise/sunset switching");
            _miAuto.Checked = Config.AutoRun;
            _miAutoStart.Text = Texts.Get("开机自启动", "Start at sign-in"); _miAutoStart.Checked = AutoStart.Enabled();
            _miLowBatt.Text = Texts.Get("低电量关机提醒", "Low-battery shutdown prompt") + " (<" + Config.LowBattPercent + "%, " + Config.LowBattGraceSec + "s)";
            _miLowBatt.Checked = Config.LowBattAlert;
            Icon ic = nowDark ? _iconMoon : _iconSun;
            if (!object.ReferenceEquals(_tray.Icon, ic)) _tray.Icon = ic;
            string tip = "AutoDayNight | " + next;
            if (tip.Length > 63) tip = tip.Substring(0, 63);
            if (_tray.Text != tip) _tray.Text = tip;
        }

        private void OnLocationClick(object sender, EventArgs e)
        {
            if (_locationOpen) return;
            _locationOpen = true;
            try
            {
                using (LocationForm form = new LocationForm())
                    if (form.ShowDialog() == DialogResult.OK) { _cacheValid = false; Evaluate(true); }
            }
            finally { _locationOpen = false; }
        }

        /// <summary>电量低于阈值且未接电源时弹全屏关机提醒; 单次上锁防连弹。</summary>
        private void CheckBattery()
        {
            if (_battPromptOpen) return;
            bool onAc;
            int pct = Battery.Percent(out onAc);
            if (pct < 0) return; // 无电池(台式机), 功能静默

            if (pct >= Config.LowBattPercent || onAc)
            {
                _lowBattArmed = true; // 充够电或接上电源 -> 重新武装
                return;
            }
            if (!Config.LowBattAlert || !_lowBattArmed) return;

            // 先构造, 成功后再上锁, 避免构造失败导致功能永久卡死
            LowBatteryForm prompt = new LowBatteryForm(pct, Config.LowBattPercent, Config.LowBattGraceSec, false,
                delegate { _battPromptOpen = false; });
            _lowBattArmed = false;
            _battPromptOpen = true;
            Config.Log(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " 低电量 " + pct +
                "% (阈值 " + Config.LowBattPercent + "%), 弹出关机提醒 (超时 " + Config.LowBattGraceSec + " 秒)");
            prompt.Show();
        }

        private void OnLowBattClick(object sender, EventArgs e)
        {
            Config.LowBattAlert = !Config.LowBattAlert;
            Config.Save();
            Config.Log(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " 低电量关机提醒 -> " + (Config.LowBattAlert ? "开" : "关"));
            if (Config.LowBattAlert) _lowBattArmed = true; // 打开开关立即武装
            DateTime a, b; GetToday(out a, out b); UpdateUi(a, b);
        }

        private void OnToggleClick(object sender, EventArgs e)
        {
            try
            {
                bool dark = !ThemeSwitcher.IsCurrentlyDark();
                ThemeSwitcher.Apply(dark);
                Config.Log(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " 手动 -> " + (dark ? "深色" : "浅色"));
                DateTime sr, ss; GetToday(out sr, out ss); UpdateUi(sr, ss);
            }
            catch (Exception ex) { Config.Log("手动切换错误: " + ex.Message); }
        }

        private void OnAutoClick(object sender, EventArgs e)
        {
            try
            {
                Config.AutoRun = !Config.AutoRun;
                if (Config.AutoRun && !Config.LocationConfigured) OnLocationClick(null, null);
                Config.Save(); _cacheValid = false; Evaluate(Config.AutoRun);
            }
            catch (Exception ex) { Config.Log("Automatic switching setting error: " + ex.Message); }
        }

        private void OnAutoStartClick(object sender, EventArgs e)
        {
            try
            {
                bool enable = !_miAutoStart.Checked;
                AutoStart.Set(enable);
                _miAutoStart.Checked = AutoStart.Enabled();
                Config.Log(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " 开机自启 -> " + (enable ? "开" : "关"));
            }
            catch (Exception ex) { Config.Log("自启设置错误: " + ex.Message); }
        }

        private void OnExitClick(object sender, EventArgs e)
        {
            Config.Log(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " 托盘菜单手动退出");
            SystemEvents.TimeChanged -= OnClockChanged;
            SystemEvents.PowerModeChanged -= OnPowerChanged;
            if (_timer != null) { _timer.Stop(); _timer.Dispose(); }
            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
                _tray = null;
            }
            if (_iconSun != null) _iconSun.Dispose();
            if (_iconMoon != null) _iconMoon.Dispose();
            Application.ExitThread();
        }

    }

    // ---------------- 托盘图标(太阳/月亮: 多尺寸 ICO, 高 DPI 下按原生尺寸绘制不糊边) ----------------
    internal static class Icons
    {
        private static readonly int[] Sizes = new int[] { 16, 20, 24, 32, 40, 48, 64 };
        // Icon 会惰性读取底层流, 必须保持存活
        private static readonly List<Stream> KeepAlive = new List<Stream>();

        internal static Icon Sun() { return Build(DrawSun); }
        internal static Icon Moon() { return Build(DrawMoon); }

        /// <summary>
        /// 太阳(浅色模式用, 纯黑): 圆盘 + 8 道圆头光芒。
        /// 参数经 16/20/24/32px 像素级比对选定, 保证托盘小尺寸下光芒与圆盘不粘连。
        /// </summary>
        internal static void DrawSun(Graphics g, int size)
        {
            float u = size / 32f;
            float cx = 16f * u;
            using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 0, 0, 0)))
                g.FillEllipse(b, cx - 7.2f * u, cx - 7.2f * u, 14.4f * u, 14.4f * u);
            using (Pen p = new Pen(Color.FromArgb(255, 0, 0, 0), Math.Max(1f, 2.5f * u)))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4.0;
                    float x1 = cx + (float)(10.6 * u * Math.Cos(a));
                    float y1 = cx + (float)(10.6 * u * Math.Sin(a));
                    float x2 = cx + (float)(13.6 * u * Math.Cos(a));
                    float y2 = cx + (float)(13.6 * u * Math.Sin(a));
                    g.DrawLine(p, x1, y1, x2, y2);
                }
            }
        }

        /// <summary>
        /// 月牙(深色模式用, 近白): 由两段圆弧构造 —— 外弧沿大圆走腹部, 内弧沿偏移圆回到起点,
        /// 两弧在尖端自然相交成尖角, 厚度从腹部向两端平滑收敛。
        /// (旧版用两圆 Region 相减, 厚度均匀、切口是钝的, 小尺寸下像"钩子", 已弃用)
        /// </summary>
        internal static void DrawMoon(Graphics g, int size)
        {
            float u = size / 32f;
            const double R1 = 12.0;     // 外圆半径(32 单位制)
            const double ALPHA = 62.0;  // 尖端半张角(度)
            const double THICK = 7.0;   // 腹部厚度
            const double PHI = -45.0;   // 开口方向(度, y 向下坐标系 -> 朝右上)

            double r1 = R1 * u, T = THICK * u;
            double cx = 16.0 * u, cy = 16.0 * u;
            double a = ALPHA * Math.PI / 180.0;
            double phi = PHI * Math.PI / 180.0;
            double ca = Math.Cos(a);
            // 由 T = r1 + t - r2 与 r2^2 = r1^2 + t^2 - 2*r1*t*cos(alpha) 联立解出圆心偏移 t
            double t = (2 * r1 * T - T * T) / (2 * r1 * (1 + ca) - 2 * T);
            double r2 = r1 + t - T;
            if (t <= 0 || r2 <= 0) return;

            double c2x = cx + t * Math.Cos(phi);
            double c2y = cy + t * Math.Sin(phi);
            double aA = phi + a, aB = phi - a;
            double pAx = cx + r1 * Math.Cos(aA), pAy = cy + r1 * Math.Sin(aA);
            double pBx = cx + r1 * Math.Cos(aB), pBy = cy + r1 * Math.Sin(aB);

            using (GraphicsPath path = new GraphicsPath())
            {
                // 外弧: 从尖端 A 顺时针走长弧(经过腹部)到尖端 B
                path.AddArc((float)(cx - r1), (float)(cy - r1), (float)(2 * r1), (float)(2 * r1),
                            (float)(aA * 180.0 / Math.PI), (float)(360.0 - 2 * ALPHA));

                // 内弧: 从尖端 B 沿偏移圆回到尖端 A, 取经过腹部方向的那一侧
                double angA2 = Math.Atan2(pAy - c2y, pAx - c2x) * 180.0 / Math.PI;
                double angB2 = Math.Atan2(pBy - c2y, pBx - c2x) * 180.0 / Math.PI;
                double s1 = Norm180(angA2 - angB2);
                double s2 = s1 > 0 ? s1 - 360.0 : s1 + 360.0;
                double want = Norm180((PHI + 180.0));
                double m1 = Norm180(angB2 + s1 / 2.0);
                double m2 = Norm180(angB2 + s2 / 2.0);
                double sweep = Math.Abs(Norm180(m1 - want)) <= Math.Abs(Norm180(m2 - want)) ? s1 : s2;
                path.AddArc((float)(c2x - r2), (float)(c2y - r2), (float)(2 * r2), (float)(2 * r2),
                            (float)angB2, (float)sweep);
                path.CloseFigure();

                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 246, 248, 252)))
                    g.FillPath(b, path);
            }
        }

        private static double Norm180(double d)
        {
            while (d > 180.0) d -= 360.0;
            while (d < -180.0) d += 360.0;
            return d;
        }

        /// <summary>把同一图形按多个尺寸分别绘制后打包成 ICO(PNG 条目, Vista+ 原生支持)。</summary>
        private static Icon Build(Action<Graphics, int> draw)
        {
            List<byte[]> pngs = new List<byte[]>();
            foreach (int s in Sizes)
            {
                using (Bitmap bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.Clear(Color.Transparent);
                        draw(g, s);
                    }
                    using (MemoryStream one = new MemoryStream())
                    {
                        bmp.Save(one, ImageFormat.Png);
                        pngs.Add(one.ToArray());
                    }
                }
            }

            MemoryStream ico = new MemoryStream();
            BinaryWriter w = new BinaryWriter(ico);
            w.Write((short)0);              // reserved
            w.Write((short)1);              // type = icon
            w.Write((short)Sizes.Length);   // image count
            int offset = 6 + 16 * Sizes.Length;
            for (int i = 0; i < Sizes.Length; i++)
            {
                w.Write((byte)Sizes[i]);    // width
                w.Write((byte)Sizes[i]);    // height
                w.Write((byte)0);           // palette
                w.Write((byte)0);           // reserved
                w.Write((short)1);          // planes
                w.Write((short)32);         // bpp
                w.Write(pngs[i].Length);
                w.Write(offset);
                offset += pngs[i].Length;
            }
            foreach (byte[] png in pngs) w.Write(png);
            w.Flush();
            ico.Position = 0;
            KeepAlive.Add(ico);
            return new Icon(ico);
        }
    }

    // ---------------- 低电量全屏提醒弹窗 ----------------
    internal class LowBatteryForm : Form
    {
        private readonly Label _title, _subtitle, _countdown, _hint;
        private readonly Button _btnShutdown, _btnCancel;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly int _pct, _threshold, _grace;
        private readonly bool _test;
        private readonly Action _onClosed;
        private int _remaining;
        private bool _shutdownOrdered;
        private bool _cancelLogged;

        public LowBatteryForm(int pct, int threshold, int grace, bool test, Action onClosed)
        {
            _pct = pct;
            _threshold = threshold;
            _grace = grace;
            _test = test;
            _onClosed = onClosed;
            _remaining = grace;

            Text = Texts.Get("低电量关机提醒", "Low battery shutdown prompt");
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = SystemInformation.VirtualScreen; // 覆盖全部显示器
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(16, 18, 24);
            KeyPreview = true;

            _title = new Label();
            _title.Text = Texts.Get("电量不足：", "Low battery: ") + pct + "%";
            _title.Font = new Font("Segoe UI", 30f, FontStyle.Bold);
            _title.ForeColor = Color.FromArgb(255, 176, 32);
            _title.TextAlign = ContentAlignment.MiddleCenter;

            _subtitle = new Label();
            _subtitle.Text = Texts.Get("已低于 ", "Below ") + threshold + Texts.Get("%，是否立即关闭计算机？", "%. Shut down the computer?");
            _subtitle.Font = new Font("Segoe UI", 16f);
            _subtitle.ForeColor = Color.Gainsboro;
            _subtitle.TextAlign = ContentAlignment.MiddleCenter;

            _countdown = new Label();
            _countdown.Font = new Font("Segoe UI", 14f, FontStyle.Bold);
            _countdown.ForeColor = Color.OrangeRed;
            _countdown.TextAlign = ContentAlignment.MiddleCenter;

            _hint = new Label();
            _hint.Text = Texts.Get("按 Esc 或点击「取消，不关机」中止", "Press Esc or Cancel shutdown to dismiss") + (test ? Texts.Get("　［测试模式：不会真正关机］", " [TEST: no real shutdown]") : "");
            _hint.Font = new Font("Segoe UI", 10f);
            _hint.ForeColor = Color.Gray;
            _hint.TextAlign = ContentAlignment.MiddleCenter;

            _btnShutdown = new Button();
            _btnShutdown.Text = Texts.Get("立即关机", "Shut down now");
            _btnShutdown.Size = new Size(180, 48);
            _btnShutdown.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            _btnShutdown.FlatStyle = FlatStyle.Flat;
            _btnShutdown.BackColor = Color.FromArgb(198, 40, 40);
            _btnShutdown.ForeColor = Color.White;
            _btnShutdown.FlatAppearance.BorderSize = 0;
            _btnShutdown.Click += OnShutdownClick;

            _btnCancel = new Button();
            _btnCancel.Text = Texts.Get("取消，不关机", "Cancel shutdown");
            _btnCancel.Size = new Size(180, 48);
            _btnCancel.Font = new Font("Segoe UI", 12f);
            _btnCancel.FlatStyle = FlatStyle.Flat;
            _btnCancel.BackColor = Color.FromArgb(60, 64, 72);
            _btnCancel.ForeColor = Color.White;
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.Click += OnCancelClick;

            Controls.AddRange(new Control[] { _title, _subtitle, _countdown, _hint, _btnShutdown, _btnCancel });

            KeyDown += OnFormKeyDown;
            FormClosed += OnFormClosedInternal;

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 1000;
            _timer.Tick += OnTimerTick;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            LayoutControls();
            UpdateCountdown();
            _timer.Start();
            Activate();
            TopMost = true;
            try { System.Media.SystemSounds.Exclamation.Play(); }
            catch { }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutControls();
        }

        private void LayoutControls()
        {
            // 构造函数里 Bounds 赋值会先触发 OnResize, 此时子控件尚未创建
            if (_title == null || _subtitle == null || _countdown == null || _hint == null
                || _btnShutdown == null || _btnCancel == null) return;
            int cx = ClientSize.Width / 2;
            int cy = ClientSize.Height / 2;
            _title.SetBounds(cx - 500, cy - 190, 1000, 60);
            _subtitle.SetBounds(cx - 500, cy - 120, 1000, 40);
            _countdown.SetBounds(cx - 500, cy - 65, 1000, 36);
            _btnShutdown.Location = new Point(cx - 195, cy + 10);
            _btnCancel.Location = new Point(cx + 15, cy + 10);
            _hint.SetBounds(cx - 500, cy + 80, 1000, 30);
        }

        private void UpdateCountdown()
        {
            _countdown.Text = _test
                ? Texts.Get("［测试］" + _remaining + " 秒后关闭本弹窗（不关机）", "[TEST] Close prompt in " + _remaining + "s (no shutdown)")
                : Texts.Get(_remaining + " 秒内无任何操作，将自动关机", "No response: shutdown in " + _remaining + " seconds");
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            if (!_test)
            {
                bool onAc; int pct = Battery.Percent(out onAc);
                if (onAc || pct < 0 || pct >= Config.LowBattPercent || !Config.LowBattAlert)
                { CancelClose("Power restored or low-battery prompt disabled"); return; }
            }
            _remaining--;
            if (_remaining <= 0)
            {
                _timer.Stop();
                PerformShutdown(true);
            }
            else
            {
                UpdateCountdown();
            }
        }

        private void OnShutdownClick(object sender, EventArgs e)
        {
            PerformShutdown(false);
        }

        private void PerformShutdown(bool byTimeout)
        {
            if (_shutdownOrdered) return;
            _shutdownOrdered = true;

            if (_test)
            {
                Config.Log(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [测试] " + (byTimeout
                    ? ("倒计时 " + _grace + " 秒归零，模拟自动关机（电量 " + _pct + "%，未真正执行）")
                    : "点击立即关机（测试未执行）"));
            }
            else
            {
                Config.Log(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + (byTimeout
                    ? (" 低电量超时无响应，自动关机（电量 " + _pct + "%）")
                    : (" 用户点击立即关机（电量 " + _pct + "%）")));
                try
                {
                    System.Diagnostics.Process.Start(Path.Combine(Environment.SystemDirectory, "shutdown.exe"), "/s /t 0");
                }
                catch (Exception ex)
                {
                    Config.Log("关机命令失败: " + ex.Message);
                }
            }
            Close();
        }

        private void OnCancelClick(object sender, EventArgs e)
        {
            CancelClose("用户点击取消");
        }

        private void OnFormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                CancelClose("用户按 Esc 取消");
            }
        }

        private void CancelClose(string reason)
        {
            if (!_shutdownOrdered && !_cancelLogged)
            {
                _cancelLogged = true;
                Config.Log(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + (test_suffix(reason)));
            }
            Close();
        }

        private string test_suffix(string reason)
        {
            return (_test ? " [测试] " : " ") + reason + "（电量 " + _pct + "%）";
        }

        private void OnFormClosedInternal(object sender, FormClosedEventArgs e)
        {
            _timer.Stop();
            _timer.Dispose();
            // 未走到关机、也未记录过的关闭(Alt+F4 / WM_CLOSE 等)都算"有回应", 视为取消;
            // FormClosed 在任何关闭路径都必然执行, 比 FormClosing 可靠
            if (!_shutdownOrdered && !_cancelLogged)
            {
                _cancelLogged = true;
                Config.Log(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + test_suffix("关闭窗口，视为取消"));
            }
            if (_onClosed != null) _onClosed();
        }
    }
}
