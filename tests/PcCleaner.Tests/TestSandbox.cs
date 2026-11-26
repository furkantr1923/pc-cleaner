using PcCleaner.Core;

namespace PcCleaner.Tests;

/// <summary>
/// Her test için izole, kontrollü bir sahte "Temp" klasörü ve bunun DIŞINDA
/// korunması gereken bir "kişisel" klasör oluşturur. Gerçek kişisel dosyalara asla dokunulmaz.
/// </summary>
internal sealed class TestSandbox : IDisposable
{
    public string Base { get; }
    public string TempRoot { get; }
    public string Outside { get; }
    public ApprovedLocation Location { get; }

    public TestSandbox()
    {
        Base = Path.Combine(Path.GetTempPath(), "pccleaner-tests", Guid.NewGuid().ToString("N"));
        TempRoot = Path.Combine(Base, "FakeTemp");
        Outside = Path.Combine(Base, "Personal");
        Directory.CreateDirectory(TempRoot);
        Directory.CreateDirectory(Outside);
        Location = new ApprovedLocation("Test Temp", TempRoot);
    }

    public string CreateFile(string relative, int size, DateTime? lastWriteUtc = null, string? root = null)
    {
        string path = Path.Combine(root ?? TempRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[size]);
        File.SetLastWriteTimeUtc(path, lastWriteUtc ?? DateTime.UtcNow.AddDays(-3));
        return path;
    }

    public static Scanner NoAgeLimitScanner() => new() { MinimumAge = TimeSpan.Zero };

    public void Dispose()
    {
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(Base, "*", SearchOption.AllDirectories))
                TryChmod(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Directory.Delete(Base, recursive: true);
        }
        catch
        {
            // Test temizliği başarısız olursa testi düşürme.
        }
    }

    public static void TryChmod(string path, UnixFileMode mode)
    {
        if (!OperatingSystem.IsWindows())
        {
            try { File.SetUnixFileMode(path, mode); } catch { }
        }
    }
}

/// <summary>
/// Yalnızca Linux/macOS'ta ve root OLMAYAN kullanıcıyla çalışan test.
/// (Unix izinleriyle "erişim reddedildi" durumu ancak böyle güvenilir şekilde oluşturulabilir.)
/// Diğer ortamlarda test "geçti" değil, açıkça "atlandı" olarak raporlanır.
/// </summary>
public sealed class UnixNonRootFactAttribute : FactAttribute
{
    public UnixNonRootFactAttribute()
    {
        if (OperatingSystem.IsWindows())
            Skip = "Windows'ta Unix izin testi uygulanamaz.";
        else if (Environment.UserName == "root")
            Skip = "root kullanıcısı izin kısıtlamalarına takılmaz; yetkisiz kullanıcıyla çalıştırın.";
    }
}
