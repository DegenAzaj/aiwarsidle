using System;
using System.Globalization;

namespace AIWarsIdle.UI.Generators
{
    public static class UiFormat
    {
        public static string Compact(double value, int decimals = 2)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "0";

            var sign = value < 0 ? "-" : "";
            value = Math.Abs(value);

            if (value < 1000.0)
            {
                return sign + value.ToString("0.##", CultureInfo.InvariantCulture);
            }

            var suffixes = new[] { "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc" };
            var exp = (int)Math.Floor(Math.Log10(value) / 3.0);
            if (exp < 1) exp = 1;
            if (exp > suffixes.Length) exp = suffixes.Length;

            var scaled = value / Math.Pow(1000.0, exp);
            var clampedDecimals = decimals;
            if (clampedDecimals < 0) clampedDecimals = 0;
            if (clampedDecimals > 6) clampedDecimals = 6;
            var fmt = clampedDecimals <= 0 ? "0" : "0." + new string('#', clampedDecimals);
            return sign + scaled.ToString(fmt, CultureInfo.InvariantCulture) + suffixes[exp - 1];
        }

        public static string Duration(long seconds)
        {
            if (seconds <= 0) return "0s";
            if (seconds < 60) return $"{seconds}s";

            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalHours >= 1)
            {
                return $"{(int)ts.TotalHours}h {ts.Minutes:D2}m {ts.Seconds:D2}s";
            }
            return $"{ts.Minutes}m {ts.Seconds:D2}s";
        }
    }
}
