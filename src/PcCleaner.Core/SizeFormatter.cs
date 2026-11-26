using System.Globalization;

namespace PcCleaner.Core;

/// <summary>Bayt değerini Türkçe, okunabilir biçime çevirir (ör. "1,5 MB").</summary>
public static class SizeFormatter
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly string[] Units = ["bayt", "KB", "MB", "GB", "TB"];

    public static string Format(long bytes)
    {
        if (bytes < 1024)
            return string.Create(Tr, $"{Math.Max(bytes, 0)} bayt");

        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return string.Create(Tr, $"{value:0.#} {Units[unit]}");
    }
}
