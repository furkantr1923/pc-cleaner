using System.Security;

namespace PcCleaner.Core;

/// <summary>
/// Yalnızca kullanıcının seçtiği dosyaları, tek tek doğrulayarak siler.
/// Zorla silme, izin değiştirme, salt-okunur kaldırma veya yönetici yetkisi YOKTUR.
/// </summary>
public sealed class Cleaner
{
    private readonly IReadOnlyList<ApprovedLocation> _approved;

    public Cleaner(IReadOnlyList<ApprovedLocation> approvedLocations)
    {
        _approved = approvedLocations ?? throw new ArgumentNullException(nameof(approvedLocations));
    }

    public CleanResult Delete(
        IReadOnlyList<TempFile> selected,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<DeleteItemResult>(selected.Count);
        bool cancelled = false;

        for (int i = 0; i < selected.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }

            results.Add(new DeleteItemResult(selected[i], DeleteOne(selected[i])));

            if ((i & 0x3F) == 0 || i == selected.Count - 1)
                progress?.Report(i + 1);
        }

        return new CleanResult { Items = results, Cancelled = cancelled };
    }

    internal DeleteOutcome DeleteOne(TempFile file)
    {
        // 1) Dosyanın ait olduğu konum hâlâ onaylı listede mi?
        var location = _approved.FirstOrDefault(a => PathGuard.PathEquals(a.RootPath, file.Location.RootPath));
        if (location is null || !PathGuard.IsSafeRoot(location.RootPath))
            return DeleteOutcome.Rejected;

        // 2) Yol, onaylı kökün içinde mi? (path traversal: "..\..\" vb. burada elenir)
        if (!PathGuard.IsStrictlyInside(file.FullPath, location.RootPath))
            return DeleteOutcome.Rejected;

        string fullPath = PathGuard.Normalize(file.FullPath);

        if (!File.Exists(fullPath))
            return Directory.Exists(fullPath) ? DeleteOutcome.Rejected : DeleteOutcome.NotFound;

        // 3) Dosya veya üst klasörlerinden biri bağlantı (symlink/junction) mi?
        if (PathGuard.HasReparsePointOnPath(fullPath, location.RootPath))
            return DeleteOutcome.Rejected;

        try
        {
            // 4) Tarama sonrası salt okunur / sistem dosyasına dönüştüyse dokunma.
            var attrs = File.GetAttributes(fullPath);
            if ((attrs & (FileAttributes.ReadOnly | FileAttributes.System | FileAttributes.Directory)) != 0)
                return DeleteOutcome.Changed;

            // 5) Kullanımda mı? Dosyayı paylaşımsız açmayı dene: başka bir program
            //    açık tutuyorsa Windows bunu reddeder → dosyayı atla.
            using (new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.None, 1, FileOptions.None))
            {
            }

            // 6) Normal Windows silmesi (Geri Dönüşüm Kutusu'na değil; bunlar geçici dosyalar).
            File.Delete(fullPath);
            return DeleteOutcome.Deleted;
        }
        catch (FileNotFoundException) { return DeleteOutcome.NotFound; }
        catch (DirectoryNotFoundException) { return DeleteOutcome.NotFound; }
        catch (UnauthorizedAccessException) { return DeleteOutcome.AccessDenied; }
        catch (SecurityException) { return DeleteOutcome.AccessDenied; }
        catch (IOException) { return DeleteOutcome.InUse; }
    }
}
