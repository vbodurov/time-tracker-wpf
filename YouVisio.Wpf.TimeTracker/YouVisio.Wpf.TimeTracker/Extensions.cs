using System;
using System.Globalization;

namespace YouVisio.Wpf.TimeTracker
{
    public static class Extensions
    {
        public static string ToPadString(this int i, int len)
        {
            return i.ToString(CultureInfo.InvariantCulture).PadLeft(len, '0');
        }
        public static double Round(this double n, int digits)
        {
            return Math.Round(n, digits);
        }
        public static string ToYearMonthDay(this DateTime d)
        {
            return d.Year.ToPadString(4) + "-" + d.Month.ToPadString(2) + "-" + d.Day.ToPadString(2);;
        }
    }
}