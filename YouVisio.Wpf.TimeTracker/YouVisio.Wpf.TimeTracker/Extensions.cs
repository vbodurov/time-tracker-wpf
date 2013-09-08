using System.Globalization;

namespace YouVisio.Wpf.TimeTracker
{
    public static class Extensions
    {
         public static string ToPadString(this int i, int len)
         {
             return i.ToString(CultureInfo.InvariantCulture).PadLeft(len, '0');
         }
    }
}