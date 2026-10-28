using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace AutoDayNight
{
    internal static class Config
    {
        public static readonly string Dir = Environment.GetEnvironmentVariable("AUTODAYNIGHT_DATA_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoDayNight");
        public static readonly string ConfigFile = Path.Combine(Dir, "config.ini");
        public static readonly string LogFile = Path.Combine(Dir, "log.txt");
        public static double Lat, Lon;
        public static string City = "";
        public static string TimeZoneId = ""; // Empty = follow Windows, including DST.
        public static bool LocationConfigured;
        public static bool AutoRun = true;
        public static int SunriseOffset, SunsetOffset;
        public static bool LowBattAlert; // New installations opt in; existing preference is retained.
        public static int LowBattPercent = 20, LowBattGraceSec = 60;

        public static TimeZoneInfo Zone
        {
            get { return string.IsNullOrEmpty(TimeZoneId) ? TimeZoneInfo.Local : TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId); }
        }

        public static bool ValidCoordinates(double lat, double lon)
        {
            return !double.IsNaN(lat) && !double.IsInfinity(lat) && lat >= -90 && lat <= 90
                && !double.IsNaN(lon) && !double.IsInfinity(lon) && lon >= -180 && lon <= 180;
        }

        public static void Load()
        {
            Lat = Lon = 0; City = ""; TimeZoneId = ""; LocationConfigured = false;
            AutoRun = true; SunriseOffset = SunsetOffset = 0;
            LowBattAlert = false; LowBattPercent = 20; LowBattGraceSec = 60;
            bool seenLat = false, seenLon = false, seenConfigured = false;
            try
            {
                if (!File.Exists(ConfigFile)) return;
                foreach (string raw in File.ReadAllLines(ConfigFile, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int eq = line.IndexOf('=');
                    if (eq < 1) continue;
                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Split('#')[0].Trim();
                    try
                    {
                        if (key == "lat") { Lat = double.Parse(val, CultureInfo.InvariantCulture); seenLat = true; }
                        else if (key == "lon") { Lon = double.Parse(val, CultureInfo.InvariantCulture); seenLon = true; }
                        else if (key == "city") City = val;
                        else if (key == "time_zone") TimeZoneId = val;
                        else if (key == "location_configured") { LocationConfigured = val == "1"; seenConfigured = true; }
                        else if (key == "auto") AutoRun = val != "0";
                        else if (key == "sunrise_offset_min") SunriseOffset = int.Parse(val, CultureInfo.InvariantCulture);
                        else if (key == "sunset_offset_min") SunsetOffset = int.Parse(val, CultureInfo.InvariantCulture);
                        else if (key == "low_batt_alert") LowBattAlert = val != "0";
                        else if (key == "low_batt_percent") LowBattPercent = int.Parse(val, CultureInfo.InvariantCulture);
                        else if (key == "low_batt_grace_sec") LowBattGraceSec = int.Parse(val, CultureInfo.InvariantCulture);
                    }
                    catch (FormatException) { }
                    catch (OverflowException) { }
                }
                // Legacy installations explicitly stored both coordinates. Do not reset their location.
                if (!seenConfigured) LocationConfigured = seenLat && seenLon;
                LocationConfigured = LocationConfigured && seenLat && seenLon && ValidCoordinates(Lat, Lon);
                try { TimeZoneInfo ignored = Zone; }
                catch (TimeZoneNotFoundException) { LocationConfigured = false; TimeZoneId = ""; }
                catch (InvalidTimeZoneException) { LocationConfigured = false; TimeZoneId = ""; }
                SunriseOffset = Math.Max(-180, Math.Min(180, SunriseOffset));
                SunsetOffset = Math.Max(-180, Math.Min(180, SunsetOffset));
                LowBattPercent = Math.Max(1, Math.Min(99, LowBattPercent));
                LowBattGraceSec = Math.Max(10, Math.Min(3600, LowBattGraceSec));
                City = City.Replace("\r", " ").Replace("\n", " ");
                if (City.Length > 80) City = City.Substring(0, 80);
            }
            catch (Exception ex) { Log("Configuration read error: " + ex.Message); LocationConfigured = false; }
        }

        public static bool Save()
        {
            string temp = ConfigFile + ".tmp";
            try
            {
                Directory.CreateDirectory(Dir);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# AutoDayNight configuration (UTF-8); use tray Location settings, or restart after editing.");
                sb.AppendLine("location_configured=" + (LocationConfigured ? "1" : "0"));
                sb.AppendLine("city=" + City.Replace("\r", " ").Replace("\n", " "));
                sb.AppendLine("lat=" + Lat.ToString("0.######", CultureInfo.InvariantCulture));
                sb.AppendLine("lon=" + Lon.ToString("0.######", CultureInfo.InvariantCulture));
                sb.AppendLine("time_zone=" + TimeZoneId);
                sb.AppendLine("auto=" + (AutoRun ? "1" : "0"));
                sb.AppendLine("sunrise_offset_min=" + SunriseOffset);
                sb.AppendLine("sunset_offset_min=" + SunsetOffset);
                sb.AppendLine("low_batt_alert=" + (LowBattAlert ? "1" : "0"));
                sb.AppendLine("low_batt_percent=" + LowBattPercent);
                sb.AppendLine("low_batt_grace_sec=" + LowBattGraceSec);
                File.WriteAllText(temp, sb.ToString(), new UTF8Encoding(true));
                if (File.Exists(ConfigFile)) File.Replace(temp, ConfigFile, null);
                else File.Move(temp, ConfigFile);
                return true;
            }
            catch (Exception ex) { Log("Configuration save error: " + ex.Message); return false; }
        }

        public static void Log(string msg)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                FileInfo fi = new FileInfo(LogFile);
                if (fi.Exists && fi.Length > 200000) File.Delete(LogFile);
                File.AppendAllText(LogFile, msg + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }
    }

    internal static class Texts
    {
        public static string Get(string zh, string en)
        { return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? zh : en; }
    }
}
