using System.Text.RegularExpressions;

namespace KilometrikisaBE
{
    public static class StringExtensions
    {
        public static string OnlyNumbers(this string str)
        {
            return Regex.Replace(str, @"[^\d.,-]", "").Replace(',', '.');
        }

        public static string TrimPersonName(this string name)
        {
            return name.Trim().Split('\n')[0].Trim();
        }
    }
}
