using System.Diagnostics;
using System.Security;

namespace PcCleaner.Core;

/// <summary>Onaylı geçici klasörleri tarar. Hiçbir dosyayı değiştirmez veya içeriğini okumaz.</summary>
public sealed class Scanner
{
    /// <summary>
    /// Bu süreden daha yeni değiştirilmiş dosyalar listelenmez:
    /// çalışan bir kurulum veya program tarafından hâlâ kullanılıyor olabilirler.
    /// </summary>
    public TimeSpan MinimumAge { get; init; } = TimeSpan.FromHours(24);

    /// <summary>Test edilebilirlik için saat kaynağı.</summary>
    public Func<DateTime> UtcNow { get; init; } = () => DateTime.UtcNow;

    private static readonly EnumerationOptions EnumOptions = new()
    {
        RecurseSubdirectories = false, // özyinelemeyi kendimiz yapıyoruz → bağlantıları kontrol edebilmek için
        IgnoreInaccessible = false,    // erişilemeyenleri sayıp raporlamak için
        AttributesToSkip = 0,          // gizli dosyalar da geçici olabilir; filtrelemeyi biz yapıyoruz
        ReturnSpecialDirectories = false,
    };

    private const FileAttributes ProtectedAttributes =
        FileAttributes.ReadOnly | FileAttributes.System | FileAttributes.Offline;

    public ScanResult Scan(
        IReadOnlyList<ApprovedLocation> locations,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var files = new List<TempFile>();
        long bytes = 0;
        int inaccessible = 0, links = 0, recent = 0, protectedCount = 0;
        DateTime cutoff = UtcNow() - MinimumAge;
        var throttle = Stopwatch.StartNew();

        foreach (var location in locations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!PathGuard.IsSafeRoot(location.RootPath) || !Directory.Exists(location.RootPath))
                continue;

            var pending = new Stack<string>();
            pending.Push(location.RootPath);

            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string dir = pending.Pop();

                if (throttle.ElapsedMilliseconds >= 100)
                {
                    progress?.Report(new ScanProgress(files.Count, bytes, dir));
                    throttle.Restart();
                }

                try
                {
                    int n = 0;
                    foreach (var entry in new DirectoryInfo(dir).EnumerateFileSystemInfos("*", EnumOptions))
                    {
                        if ((++n & 0xFF) == 0)
                            cancellationToken.ThrowIfCancellationRequested();

                        FileAttributes attrs;
                        try { attrs = entry.Attributes; }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { inaccessible++; continue; }

                        // Sembolik bağlantı / junction: asla takip etme, asla listeleme.
                        if (attrs.HasFlag(FileAttributes.ReparsePoint))
                        {
                            links++;
                            continue;
                        }

                        if (entry is DirectoryInfo sub)
                        {
                            pending.Push(sub.FullName);
                            continue;
                        }

                        if (entry is not FileInfo file)
                            continue;

                        if ((attrs & ProtectedAttributes) != 0)
                        {
                            protectedCount++;
                            continue;
                        }

                        DateTime lastWrite;
                        long length;
                        try
                        {
                            lastWrite = file.LastWriteTimeUtc;
                            length = file.Length;
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                        {
                            inaccessible++;
                            continue;
                        }

                        if (lastWrite > cutoff)
                        {
                            recent++;
                            continue;
                        }

                        // Ek güvence: bulunan her yol gerçekten onaylı kökün içinde olmalı.
                        if (!PathGuard.IsStrictlyInside(file.FullName, location.RootPath))
                            continue;

                        files.Add(new TempFile(
                            location,
                            file.FullName,
                            Path.GetRelativePath(location.RootPath, file.FullName),
                            length,
                            lastWrite));
                        bytes += length;
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException)
                {
                    // Erişilemeyen klasör: çökme yok, sadece say ve devam et.
                    inaccessible++;
                }
            }
        }

        progress?.Report(new ScanProgress(files.Count, bytes, string.Empty));

        return new ScanResult
        {
            Files = files,
            ScannedLocations = locations,
            InaccessibleDirectories = inaccessible,
            SkippedLinks = links,
            SkippedRecent = recent,
            SkippedProtected = protectedCount,
        };
    }
}
