namespace PcCleaner.Core;

/// <summary>
/// Silme öncesi tüm yol güvenlik kontrolleri.
/// Kural: şüphe varsa silme.
/// </summary>
public static class PathGuard
{
    private static StringComparison Comparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>Yolu tam yola çevirir ve sondaki ayırıcıyı kaldırır (sürücü kökü hariç).</summary>
    public static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    /// <summary>
    /// Windows'ta 8.3 kısa adları (ör. C:\Users\FURKAN~1\...) uzun ada çevirir.
    /// Böylece aynı klasör iki farklı yazımla iki kez taranmaz ve karşılaştırmalar doğru çalışır.
    /// Diğer platformlarda yalnızca normalleştirir.
    /// </summary>
    public static string ToLongPath(string path)
    {
        string full = Normalize(path);
        if (!OperatingSystem.IsWindows() || !full.Contains('~'))
            return full;

        var buffer = new char[32768];
        uint length = NativeMethods.GetLongPathNameW(full, buffer, (uint)buffer.Length);
        return length > 0 && length < buffer.Length
            ? Normalize(new string(buffer, 0, (int)length))
            : full;
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        internal static extern uint GetLongPathNameW(string lpszShortPath, [System.Runtime.InteropServices.Out] char[] lpszLongPath, uint cchBuffer);
    }

    public static bool PathEquals(string a, string b) =>
        string.Equals(Normalize(a), Normalize(b), Comparison);

    /// <summary><paramref name="path"/>, <paramref name="root"/> klasörünün İÇİNDE mi? (kökün kendisi değil)</summary>
    public static bool IsStrictlyInside(string path, string root)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
            return false;

        string fullPath;
        string fullRoot;
        try
        {
            // GetFullPath, "..\" gibi parçaları çözer → path traversal burada etkisizleşir.
            fullPath = Normalize(path);
            fullRoot = Normalize(root);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        string prefix = fullRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;

        return fullPath.Length > prefix.Length && fullPath.StartsWith(prefix, Comparison);
    }

    /// <summary>
    /// Bir klasörün geçici-dosya kökü olarak kullanılması güvenli mi?
    /// TEMP ortam değişkeni yanlışlıkla (veya kötü niyetle) kullanıcı klasörüne,
    /// sürücü köküne ya da Windows klasörüne ayarlanmışsa reddedilir.
    /// </summary>
    public static bool IsSafeRoot(string root)
    {
        string full;
        try { full = Normalize(root); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }

        // Sürücü kökü (C:\) veya / asla kök olamaz.
        string? driveRoot = Path.GetPathRoot(full);
        if (driveRoot is null || PathEquals(full, driveRoot))
            return false;

        foreach (string protectedPath in ProtectedFolders())
        {
            // Kök, korunan bir klasörün kendisi ya da üst klasörü olamaz.
            if (PathEquals(full, protectedPath) || IsStrictlyInside(protectedPath, full))
                return false;
        }
        return true;
    }

    /// <summary>Asla taranmaması/silinmemesi gereken kişisel ve sistem klasörleri.</summary>
    internal static IEnumerable<string> ProtectedFolders()
    {
        Environment.SpecialFolder[] folders =
        [
            Environment.SpecialFolder.UserProfile,
            Environment.SpecialFolder.Desktop,
            Environment.SpecialFolder.DesktopDirectory,
            Environment.SpecialFolder.MyDocuments,
            Environment.SpecialFolder.MyPictures,
            Environment.SpecialFolder.MyVideos,
            Environment.SpecialFolder.MyMusic,
            Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolder.Windows,
            Environment.SpecialFolder.System,
            Environment.SpecialFolder.ProgramFiles,
            Environment.SpecialFolder.ProgramFilesX86,
            Environment.SpecialFolder.CommonApplicationData,
        ];

        foreach (var folder in folders)
        {
            string path = Environment.GetFolderPath(folder, Environment.SpecialFolderOption.DoNotVerify);
            if (!string.IsNullOrWhiteSpace(path))
                yield return path;
        }

        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile))
            yield return Path.Combine(profile, "Downloads");
    }

    /// <summary>
    /// Dosyanın kendisi veya kök ile dosya arasındaki herhangi bir klasör
    /// sembolik bağlantı / junction (reparse point) mi? Öyleyse dosya kök dışına
    /// işaret ediyor olabilir → güvenli değil.
    /// </summary>
    public static bool HasReparsePointOnPath(string filePath, string root)
    {
        try
        {
            string fullRoot = Normalize(root);
            string current = Normalize(filePath);

            while (!PathEquals(current, fullRoot))
            {
                if (!IsStrictlyInside(current, fullRoot))
                    return true; // köke ulaşamadan dışarı çıktık → güvensiz say.

                var attrs = File.GetAttributes(current);
                if (attrs.HasFlag(FileAttributes.ReparsePoint))
                    return true;

                string? parent = Path.GetDirectoryName(current);
                if (parent is null)
                    return true;
                current = parent;
            }
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return true; // okunamıyorsa güvensiz say.
        }
    }
}
