using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace AutoDayNight.Tests
{
    internal static class TestRunner
    {
        private static int _checks;
        private static void Check(bool condition, string name)
        { if (!condition) throw new Exception("FAIL: " + name); _checks++; Console.WriteLine("PASS: " + name); }
        private static TimeZoneInfo Zone(string id) { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        private static DateTime D(string date) { return DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture); }
        private static SolarDay Day(string date, double lat, double lon, string zone)
        { return SolarSchedule.GetDay(D(date), lat, lon, Zone(zone)); }
        private static void Approx(DateTime? actual, int hour, int minute, string name)
        { Check(actual.HasValue && Math.Abs(actual.Value.TimeOfDay.TotalMinutes - hour * 60 - minute) <= 15, name + " (+/-15 min reference)"); }
        private static void Throws(Action fn, string name)
        { bool threw = false; try { fn(); } catch (ArgumentException) { threw = true; } Check(threw, name); }

        [STAThread]
        public static int Main()
        {
            try
            {
                string dir = Environment.GetEnvironmentVariable("AUTODAYNIGHT_DATA_DIR");
                if (string.IsNullOrEmpty(dir) || Path.GetFullPath(dir) == Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoDayNight")))
                    throw new Exception("Tests require an isolated AUTODAYNIGHT_DATA_DIR.");
                Directory.CreateDirectory(dir);
                SolarDay sh = Day("2026-09-24", 38.0406, 114.5149, "China Standard Time");
                Approx(sh.Sunrise, 6, 10, "Shijiazhuang sunrise"); Approx(sh.Sunset, 18, 18, "Shijiazhuang sunset");
                SolarDay london = Day("2026-06-21", 51.5074, -0.1278, "GMT Standard Time");
                Approx(london.Sunrise, 4, 43, "London summer sunrise"); Approx(london.Sunset, 21, 21, "London summer sunset");
                SolarDay spring = Day("2026-03-08", 40.7128, -74.0060, "Eastern Standard Time");
                Approx(spring.Sunrise, 7, 19, "New York DST start sunrise");
                Check(spring.SunriseInstant.Value.Offset == TimeSpan.FromHours(-4), "DST start uses event offset, not midnight -05");
                SolarDay autumn = Day("2026-11-01", 40.7128, -74.0060, "Eastern Standard Time");
                Check(autumn.SunriseInstant.Value.Offset == TimeSpan.FromHours(-5), "DST end uses event offset, not midnight -04");
                Check(Day("2026-03-29", 51.5074, -0.1278, "GMT Standard Time").SunriseInstant.Value.Offset == TimeSpan.FromHours(1), "London DST transition");
                SolarDay summer = Day("2026-12-21", -33.8688, 151.2093, "AUS Eastern Standard Time");
                SolarDay winter = Day("2026-06-21", -33.8688, 151.2093, "AUS Eastern Standard Time");
                Check((summer.Sunset.Value - summer.Sunrise.Value) > (winter.Sunset.Value - winter.Sunrise.Value), "Southern hemisphere seasons");
                foreach (string id in new string[] { "Line Islands Standard Time", "Hawaiian Standard Time" })
                {
                    double lat = id.StartsWith("Line") ? 1.8721 : 21.3099;
                    double lon = id.StartsWith("Line") ? -157.4278 : -157.8581;
                    SolarDay d = Day("2026-01-01", lat, lon, id);
                    Check(d.Sunrise.HasValue && d.Sunset.HasValue && d.Sunrise.Value.Date == D("2026-01-01") && d.Sunset.Value.Date == D("2026-01-01"), "Date line local date: " + id);
                }
                Check(Day("2026-06-21",69.6492,18.9553,"W. Europe Standard Time").Kind == SolarDayKind.PolarDay, "Tromso polar day");
                Check(Day("2026-12-21",69.6492,18.9553,"W. Europe Standard Time").Kind == SolarDayKind.PolarNight, "Tromso polar night");
                Check(Day("2026-06-21",90,0,"UTC").Kind == SolarDayKind.PolarDay, "North pole polar day");
                Check(Day("2026-06-21",-90,0,"UTC").Kind == SolarDayKind.PolarNight, "South pole polar night");
                Check(!SolarSchedule.IsDark(new DateTimeOffset(2026,6,21,12,0,0,TimeSpan.Zero),69.6492,18.9553,Zone("W. Europe Standard Time"),0,0), "Polar day schedules light");
                Check(SolarSchedule.IsDark(new DateTimeOffset(2026,12,21,12,0,0,TimeSpan.Zero),69.6492,18.9553,Zone("W. Europe Standard Time"),0,0), "Polar night schedules dark");
                DateTimeOffset next; bool target;
                Check(SolarSchedule.NextTransition(new DateTimeOffset(2026,6,21,12,0,0,TimeSpan.Zero),90,0,Zone("UTC"),0,0,out next,out target) && target, "Pole seasonal transition is found");
                DateTimeOffset sunrise = sh.SunriseInstant.Value;
                Check(SolarSchedule.IsDark(sunrise.AddSeconds(-1),38.0406,114.5149,Zone("China Standard Time"),0,0), "Before sunrise is dark");
                Check(!SolarSchedule.IsDark(sunrise.AddSeconds(1),38.0406,114.5149,Zone("China Standard Time"),0,0), "After sunrise is light");
                Check(SolarSchedule.IsDark(sunrise.AddMinutes(10),38.0406,114.5149,Zone("China Standard Time"),30,0), "Positive sunrise offset delays light");
                Check(!SolarSchedule.IsDark(sh.SunsetInstant.Value.AddMinutes(10),38.0406,114.5149,Zone("China Standard Time"),0,30), "Positive sunset offset delays dark");
                SolarDay misplaced = Day("2026-03-20",1.3521,103.8198,"UTC");
                DateTimeOffset shifted = misplaced.SunriseInstant.Value.AddMinutes(180);
                Check(!SolarSchedule.IsDark(shifted.AddSeconds(2),1.3521,103.8198,Zone("UTC"),180,0), "Sunrise offset crosses midnight");
                Check(SolarSchedule.NextTransition(shifted.AddSeconds(-1),1.3521,103.8198,Zone("UTC"),180,0,out next,out target) && !target && Math.Abs((next-shifted).TotalSeconds)<1, "Next boundary respects shifted date");
                foreach (double v in new double[] { double.NaN,double.PositiveInfinity,91,-91 }) Throws(delegate { SolarSchedule.GetDay(D("2026-03-20"),v,0,Zone("UTC")); }, "Reject invalid latitude " + v);
                Throws(delegate { SolarSchedule.GetDay(D("2026-03-20"),0,181,Zone("UTC")); }, "Reject longitude outside range");

                // Config fixtures are isolated from the installed app's preferences.
                if (File.Exists(Config.ConfigFile)) File.Delete(Config.ConfigFile);
                Config.Load(); Check(!Config.LocationConfigured && !Config.LowBattAlert, "Fresh install has no assumed city and no automatic shutdown");
                File.WriteAllText(Config.ConfigFile,"city=Shijiazhuang\nlat=38.0406\nlon=114.5149\nauto=1\nlow_batt_alert=1\n");
                Config.Load(); Check(Config.LocationConfigured && Config.LowBattAlert && Config.Lat==38.0406, "Legacy city and low-battery preference migrate");
                File.WriteAllText(Config.ConfigFile,"location_configured=0\nlat=38.0406\nlon=114.5149\n");
                Config.Load(); Check(!Config.LocationConfigured, "Explicit first-run flag is respected");
                File.WriteAllText(Config.ConfigFile,"lat=NaN\nlon=114\n");
                Config.Load(); Check(!Config.LocationConfigured, "Invalid coordinates disable solar sync");
                File.WriteAllText(Config.ConfigFile,"lat=51.5 # North positive\nlon=-0.12 # West negative\ntime_zone=GMT Standard Time\nlow_batt_percent=999\nlow_batt_grace_sec=-1\n");
                Config.Load(); Check(Config.LocationConfigured && Config.LowBattPercent==99 && Config.LowBattGraceSec==10, "Inline comments and config bounds");
                Check(Config.Save(), "Atomic config save"); Config.Load(); Check(Config.LocationConfigured && Config.TimeZoneId=="GMT Standard Time", "Timezone persists after reload");
                using (Icon sun = Icons.Sun()) using (Icon moon = Icons.Moon()) Check(sun.Width>0 && moon.Width>0, "Multi-size tray icons load");
                using (LocationForm form = new LocationForm()) Check(form.Controls.Count>0, "Location dialog constructs without early-resize crash");
                using (LowBatteryForm form = new LowBatteryForm(15,20,60,true,null)) Check(form.Controls.Count==6, "Low-battery test form constructs");
                Console.WriteLine("SUCCESS: " + _checks + " checks. No registry, theme, startup or shutdown changes.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
    }
}
