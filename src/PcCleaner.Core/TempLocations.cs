namespace PcCleaner.Core;

/// <summary>
/// Onaylı geçici klasörler. Yalnızca o anki Windows kullanıcısına ait,
/// yönetici izni gerektirmeyen konumlar kullanılır.
/// </summary>
public static class TempLocations
{
    /// <summary>Bu bilgisayarda gerçekten var olan, güvenlik kontrolünden geçen onaylı konumlar.</summary>
    public static IReadOnlyList<ApprovedLocation> GetDefault()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var candidates = new List<ApprovedLocation>
        {
            // %TEMP% — Windows'un bu kullanıcı için bildirdiği geçici klasör.
            new("Kullanıcı Geçici Dosyaları (%TEMP%)", Path.GetTempPath()),
        };

        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            // Çoğu sistemde %TEMP% ile aynıdır; aynıysa aşağıda tekilleştirilir.
            candidates.Add(new("Yerel Geçici Klasör", Path.Combine(localAppData, "Temp")));
            // Çöken uygulamaların bıraktığı döküm dosyaları; yeniden oluşturulabilir.
            candidates.Add(new("Uygulama Çökme Dökümleri", Path.Combine(localAppData, "CrashDumps")));
        }

        return Filter(candidates);
    }

    /// <summary>Var olmayan, güvensiz veya tekrar eden konumları eler.</summary>
    public static IReadOnlyList<ApprovedLocation> Filter(IEnumerable<ApprovedLocation> candidates)
    {
        var result = new List<ApprovedLocation>();
        foreach (var c in candidates)
        {
            string root;
            try { root = PathGuard.ToLongPath(c.RootPath); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { continue; }

            if (!Directory.Exists(root) || !PathGuard.IsSafeRoot(root))
                continue;

            // Aynı klasör veya birinin alt klasörü zaten listedeyse ekleme (çift sayımı önler).
            bool overlaps = result.Any(r =>
                PathGuard.PathEquals(r.RootPath, root) ||
                PathGuard.IsStrictlyInside(root, r.RootPath) ||
                PathGuard.IsStrictlyInside(r.RootPath, root));
            if (overlaps)
                continue;

            result.Add(c with { RootPath = root });
        }
        return result;
    }
}
