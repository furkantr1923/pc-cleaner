namespace PcCleaner.Core;

/// <summary>Taranmasına ve içinden silme yapılmasına izin verilen bir geçici klasör.</summary>
public sealed record ApprovedLocation(string DisplayName, string RootPath);

/// <summary>Taramada bulunan, silinmeye uygun tek bir dosya.</summary>
public sealed record TempFile(
    ApprovedLocation Location,
    string FullPath,
    string RelativePath,
    long Size,
    DateTime LastWriteTimeUtc);

/// <summary>Tarama sırasında arayüze gönderilen ilerleme bilgisi.</summary>
public readonly record struct ScanProgress(int FilesFound, long BytesFound, string CurrentDirectory);

/// <summary>Tamamlanmış bir taramanın sonucu.</summary>
public sealed class ScanResult
{
    public required IReadOnlyList<TempFile> Files { get; init; }
    public required IReadOnlyList<ApprovedLocation> ScannedLocations { get; init; }

    /// <summary>Erişilemeyen (izin yok vb.) klasör sayısı.</summary>
    public int InaccessibleDirectories { get; init; }

    /// <summary>Sembolik bağlantı / bağlantı noktası olduğu için atlanan öğe sayısı.</summary>
    public int SkippedLinks { get; init; }

    /// <summary>Çok yeni olduğu için (kullanımda olabilir) atlanan dosya sayısı.</summary>
    public int SkippedRecent { get; init; }

    /// <summary>Salt okunur / sistem dosyası olduğu için atlanan dosya sayısı.</summary>
    public int SkippedProtected { get; init; }

    public long TotalBytes => Files.Sum(f => f.Size);
}

/// <summary>Tek bir dosyanın silme denemesinin sonucu.</summary>
public enum DeleteOutcome
{
    Deleted,
    /// <summary>Dosya başka bir program tarafından kullanılıyor.</summary>
    InUse,
    /// <summary>Windows izin vermedi.</summary>
    AccessDenied,
    /// <summary>Dosya zaten yok (başka bir program silmiş).</summary>
    NotFound,
    /// <summary>Yol doğrulamasından geçemedi; güvenlik nedeniyle dokunulmadı.</summary>
    Rejected,
    /// <summary>Tarama sonrası dosya değişti (salt okunur, bağlantı vb. oldu).</summary>
    Changed,
}

public sealed record DeleteItemResult(TempFile File, DeleteOutcome Outcome);

public sealed class CleanResult
{
    public required IReadOnlyList<DeleteItemResult> Items { get; init; }
    public bool Cancelled { get; init; }

    public int DeletedCount => Items.Count(i => i.Outcome == DeleteOutcome.Deleted);
    public long DeletedBytes => Items.Where(i => i.Outcome == DeleteOutcome.Deleted).Sum(i => i.File.Size);
    public int SkippedCount => Items.Count(i => i.Outcome != DeleteOutcome.Deleted);
    public int CountOf(DeleteOutcome outcome) => Items.Count(i => i.Outcome == outcome);
}
