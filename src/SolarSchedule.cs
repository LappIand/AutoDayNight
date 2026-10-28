using System;
using System.Collections.Generic;

namespace AutoDayNight
{
    internal enum SolarDayKind { Normal, PolarDay, PolarNight }
    internal sealed class SolarDay
    {
        public DateTime Date;
        public DateTime? Sunrise, Sunset;
        public DateTimeOffset? SunriseInstant, SunsetInstant;
        public SolarDayKind Kind;
    }

    // NOAA/Meeus solar coordinates. All astronomy is calculated in UTC; timezone conversion
    // is applied to each EVENT instant, not to the offset at midnight (important on DST days).
    internal static class SolarSchedule
    {
        private const double Rad = Math.PI / 180.0;
        private const double Horizon = -0.833; // refraction + apparent solar radius; level horizon

        public static SolarDay GetDay(DateTime date, double lat, double lon, TimeZoneInfo zone)
        {
            Validate(lat, lon, zone);
            date = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
            SolarDay day = new SolarDay { Date = date };
            for (int i = -2; i <= 2; i++)
            {
                DateTime utcDate = DateTime.SpecifyKind(date.AddDays(i), DateTimeKind.Utc);
                foreach (bool rising in new bool[] { true, false })
                {
                    DateTimeOffset? instant = Event(utcDate, lat, lon, rising);
                    if (!instant.HasValue) continue;
                    DateTimeOffset local = TimeZoneInfo.ConvertTime(instant.Value, zone);
                    if (local.Date != date) continue;
                    if (rising) { day.SunriseInstant = local; day.Sunrise = local.DateTime; }
                    else { day.SunsetInstant = local; day.Sunset = local.DateTime; }
                }
            }
            if (day.Sunrise.HasValue || day.Sunset.HasValue) day.Kind = SolarDayKind.Normal;
            else
            {
                DateTime localNoon = date.AddHours(12);
                DateTime utc = TimeZoneInfo.ConvertTimeToUtc(localNoon, zone);
                day.Kind = Altitude(new DateTimeOffset(utc), lat, lon) >= Horizon
                    ? SolarDayKind.PolarDay : SolarDayKind.PolarNight;
            }
            return day;
        }

        public static bool IsDark(DateTimeOffset now, double lat, double lon, TimeZoneInfo zone, int riseOffset, int setOffset)
        {
            Validate(lat, lon, zone);
            ValidateOffsets(riseOffset, setOffset);
            DateTime date = TimeZoneInfo.ConvertTime(now, zone).Date;
            DateTimeOffset? latest = null;
            bool dark = Altitude(now, lat, lon) < Horizon;
            for (int i = -2; i <= 1; i++)
            {
                SolarDay d = GetDay(date.AddDays(i), lat, lon, zone);
                ConsiderPrevious(d.SunriseInstant, riseOffset, false, now, ref latest, ref dark);
                ConsiderPrevious(d.SunsetInstant, setOffset, true, now, ref latest, ref dark);
            }
            return dark;
        }

        public static bool NextTransition(DateTimeOffset now, double lat, double lon, TimeZoneInfo zone,
            int riseOffset, int setOffset, out DateTimeOffset when, out bool dark)
        {
            Validate(lat, lon, zone);
            ValidateOffsets(riseOffset, setOffset);
            DateTime date = TimeZoneInfo.ConvertTime(now, zone).Date;
            DateTimeOffset? next = null;
            dark = false;
            for (int i = -1; i <= 370; i++)
            {
                SolarDay d = GetDay(date.AddDays(i), lat, lon, zone);
                ConsiderNext(d.SunriseInstant, riseOffset, false, now, ref next, ref dark);
                ConsiderNext(d.SunsetInstant, setOffset, true, now, ref next, ref dark);
                // Also inspect the following date: offsets can move a boundary across midnight.
                if (next.HasValue && date.AddDays(i) > TimeZoneInfo.ConvertTime(next.Value, zone).Date) break;
            }
            when = next.HasValue ? next.Value : DateTimeOffset.MaxValue;
            return next.HasValue;
        }

        private static void ConsiderPrevious(DateTimeOffset? value, int offset, bool target, DateTimeOffset now,
            ref DateTimeOffset? latest, ref bool dark)
        {
            if (!value.HasValue) return;
            DateTimeOffset t = value.Value.AddMinutes(offset);
            if (t <= now && (!latest.HasValue || t >= latest.Value)) { latest = t; dark = target; }
        }
        private static void ConsiderNext(DateTimeOffset? value, int offset, bool target, DateTimeOffset now,
            ref DateTimeOffset? next, ref bool dark)
        {
            if (!value.HasValue) return;
            DateTimeOffset t = value.Value.AddMinutes(offset);
            if (t > now && (!next.HasValue || t < next.Value)) { next = t; dark = target; }
        }
        private static void Validate(double lat, double lon, TimeZoneInfo zone)
        {
            if (double.IsNaN(lat) || double.IsInfinity(lat) || lat < -90 || lat > 90) throw new ArgumentOutOfRangeException("lat");
            if (double.IsNaN(lon) || double.IsInfinity(lon) || lon < -180 || lon > 180) throw new ArgumentOutOfRangeException("lon");
            if (zone == null) throw new ArgumentNullException("zone");
        }
        private static void ValidateOffsets(int a, int b)
        { if (Math.Abs((long)a) > 180 || Math.Abs((long)b) > 180) throw new ArgumentOutOfRangeException("offset"); }

        private static DateTimeOffset? Event(DateTime utcDate, double lat, double lon, bool rising)
        {
            if (Math.Abs(lat) > 89.9999) return PoleEvent(utcDate, lat, lon, rising);
            // Anchor to a solar-noon cycle; unwrapped UTC minutes are intentional at the date line.
            double noon = 720.0 - 4.0 * lon;
            double eq, decl;
            for (int i = 0; i < 4; i++)
            {
                Coordinates(utcDate.AddMinutes(noon), out eq, out decl);
                noon = 720.0 - 4.0 * lon - eq;
            }
            double minutes = noon;
            for (int i = 0; i < 8; i++)
            {
                Coordinates(utcDate.AddMinutes(minutes), out eq, out decl);
                double denominator = Math.Cos(lat * Rad) * Math.Cos(decl * Rad);
                if (Math.Abs(denominator) < 1e-12) return null;
                double cosH = (Math.Sin(Horizon * Rad) - Math.Sin(lat * Rad) * Math.Sin(decl * Rad)) / denominator;
                if (cosH < -1 || cosH > 1) return null;
                double hourAngle = Math.Acos(cosH) / Rad;
                double candidate = 720.0 - 4.0 * lon - eq + (rising ? -4.0 : 4.0) * hourAngle;
                if (Math.Abs(candidate - minutes) < 0.0001) { minutes = candidate; break; }
                minutes = candidate;
            }
            return new DateTimeOffset(utcDate.AddMinutes(minutes));
        }

        private static DateTimeOffset? PoleEvent(DateTime utcDate, double lat, double lon, bool rising)
        {
            DateTimeOffset a = new DateTimeOffset(utcDate), b = a.AddDays(1);
            double fa = Altitude(a, lat, lon) - Horizon, fb = Altitude(b, lat, lon) - Horizon;
            if (rising ? !(fa < 0 && fb >= 0) : !(fa >= 0 && fb < 0)) return null;
            for (int i = 0; i < 40; i++)
            {
                DateTimeOffset mid = a.AddTicks((b - a).Ticks / 2);
                double fm = Altitude(mid, lat, lon) - Horizon;
                if ((fa < 0) == (fm < 0)) { a = mid; fa = fm; } else b = mid;
            }
            return b;
        }

        private static double Altitude(DateTimeOffset instant, double lat, double lon)
        {
            DateTime utc = instant.UtcDateTime;
            double eq, decl;
            Coordinates(utc, out eq, out decl);
            double solarMinutes = Mod(utc.TimeOfDay.TotalMinutes + eq + 4.0 * lon, 1440.0);
            double h = (solarMinutes / 4.0 - 180.0) * Rad;
            double sinAlt = Math.Sin(lat * Rad) * Math.Sin(decl * Rad)
                + Math.Cos(lat * Rad) * Math.Cos(decl * Rad) * Math.Cos(h);
            return Math.Asin(Math.Max(-1, Math.Min(1, sinAlt))) / Rad;
        }

        private static double Mod(double x, double m) { return (x % m + m) % m; }
        private static void Coordinates(DateTime utc, out double eq, out double decl)
        {
            double jd = utc.ToOADate() + 2415018.5;
            double t = (jd - 2451545.0) / 36525.0;
            double l0 = Mod(280.46646 + t * (36000.76983 + 0.0003032 * t), 360);
            double m = 357.52911 + t * (35999.05029 - 0.0001537 * t);
            double e = 0.016708634 - t * (0.000042037 + 0.0000001267 * t);
            double c = Math.Sin(m * Rad) * (1.914602 - t * (0.004817 + 0.000014 * t))
                + Math.Sin(2 * m * Rad) * (0.019993 - 0.000101 * t) + Math.Sin(3 * m * Rad) * 0.000289;
            double omega = 125.04 - 1934.136 * t;
            double lambda = l0 + c - 0.00569 - 0.00478 * Math.Sin(omega * Rad);
            double seconds = 21.448 - t * (46.815 + t * (0.00059 - t * 0.001813));
            double epsilon = 23.0 + (26.0 + seconds / 60.0) / 60.0 + 0.00256 * Math.Cos(omega * Rad);
            decl = Math.Asin(Math.Sin(epsilon * Rad) * Math.Sin(lambda * Rad)) / Rad;
            double y = Math.Pow(Math.Tan(epsilon * Rad / 2.0), 2);
            eq = 4.0 / Rad * (y * Math.Sin(2 * l0 * Rad) - 2 * e * Math.Sin(m * Rad)
                + 4 * e * y * Math.Sin(m * Rad) * Math.Cos(2 * l0 * Rad)
                - 0.5 * y * y * Math.Sin(4 * l0 * Rad) - 1.25 * e * e * Math.Sin(2 * m * Rad));
        }
    }
}
