using System.Globalization;

namespace PcCleaner.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var tr = CultureInfo.GetCultureInfo("tr-TR");
        CultureInfo.DefaultThreadCurrentCulture = tr;
        CultureInfo.DefaultThreadCurrentUICulture = tr;
        Thread.CurrentThread.CurrentCulture = tr;
        Thread.CurrentThread.CurrentUICulture = tr;

        // Yüksek DPI, görsel stiller ve varsayılan yazı tipi (csproj'dan üretilir).
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
