using PcCleaner.Core;

namespace PcCleaner.Tests;

public class ScannerTests
{
    [Fact]
    public void Scan_FindsFilesInNestedFolders_AndSumsSizes()
    {
        using var s = new TestSandbox();
        s.CreateFile("a.tmp", 1000);
        s.CreateFile(Path.Combine("sub", "b.log"), 2048);
        s.CreateFile(Path.Combine("sub", "deep", "c.dat"), 5);

        var result = TestSandbox.NoAgeLimitScanner().Scan([s.Location]);

        Assert.Equal(3, result.Files.Count);
        Assert.Equal(1000 + 2048 + 5, result.TotalBytes);
        Assert.Contains(result.Files, f => f.RelativePath == Path.Combine("sub", "deep", "c.dat") && f.Size == 5);
    }

    [Fact]
    public void Scan_NeverReturnsFilesOutsideApprovedRoot()
    {
        using var s = new TestSandbox();
        s.CreateFile("inside.tmp", 10);
        s.CreateFile("personal.docx", 10, root: s.Outside);

        var result = TestSandbox.NoAgeLimitScanner().Scan([s.Location]);

        Assert.Single(result.Files);
        Assert.All(result.Files, f => Assert.True(PathGuard.IsStrictlyInside(f.FullPath, s.TempRoot)));
    }

    [Fact]
    public void Scan_SkipsRecentFiles_ByDefault()
    {
        using var s = new TestSandbox();
        s.CreateFile("old.tmp", 10, DateTime.UtcNow.AddDays(-2));
        s.CreateFile("new.tmp", 10, DateTime.UtcNow.AddMinutes(-5));

        var result = new Scanner().Scan([s.Location]); // varsayılan: 24 saat

        Assert.Single(result.Files);
        Assert.Equal("old.tmp", result.Files[0].RelativePath);
        Assert.Equal(1, result.SkippedRecent);
    }

    [Fact]
    public void Scan_SkipsReadOnlyFiles()
    {
        using var s = new TestSandbox();
        s.CreateFile("normal.tmp", 10);
        string ro = s.CreateFile("readonly.tmp", 10);
        File.SetAttributes(ro, FileAttributes.ReadOnly);

        var result = TestSandbox.NoAgeLimitScanner().Scan([s.Location]);

        Assert.Single(result.Files);
        Assert.Equal(1, result.SkippedProtected);
        File.SetAttributes(ro, FileAttributes.Normal);
    }

    [Fact]
    public void Scan_DoesNotFollowSymbolicLinks()
    {
        using var s = new TestSandbox();
        s.CreateFile("inside.tmp", 10);
        s.CreateFile("secret.txt", 10, root: s.Outside);

        // Kök içinde, kök DIŞINA işaret eden klasör ve dosya bağlantıları
        Directory.CreateSymbolicLink(Path.Combine(s.TempRoot, "link-to-personal"), s.Outside);
        File.CreateSymbolicLink(Path.Combine(s.TempRoot, "link-file.txt"), Path.Combine(s.Outside, "secret.txt"));

        var result = TestSandbox.NoAgeLimitScanner().Scan([s.Location]);

        Assert.Single(result.Files);
        Assert.Equal("inside.tmp", result.Files[0].RelativePath);
        Assert.Equal(2, result.SkippedLinks);
    }

    [Fact]
    public void Scan_CanBeCancelled()
    {
        using var s = new TestSandbox();
        for (int i = 0; i < 50; i++)
            s.CreateFile(Path.Combine($"d{i}", "f.tmp"), 1);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            TestSandbox.NoAgeLimitScanner().Scan([s.Location], null, cts.Token));
    }

    [UnixNonRootFact]
    public void Scan_InaccessibleDirectory_IsCountedAndDoesNotCrash()
    {
        using var s = new TestSandbox();
        s.CreateFile("ok.tmp", 10);
        string locked = Path.Combine(s.TempRoot, "locked");
        s.CreateFile(Path.Combine("locked", "hidden.tmp"), 10);
        TestSandbox.TryChmod(locked, UnixFileMode.None);

        var result = TestSandbox.NoAgeLimitScanner().Scan([s.Location]);

        Assert.Single(result.Files);
        Assert.True(result.InaccessibleDirectories >= 1);
    }

    [Fact]
    public void Scan_ReportsProgress()
    {
        using var s = new TestSandbox();
        s.CreateFile("a.tmp", 7);
        var reports = new List<ScanProgress>();

        TestSandbox.NoAgeLimitScanner().Scan([s.Location], new SyncProgress<ScanProgress>(reports.Add));

        Assert.NotEmpty(reports);
        Assert.Equal(1, reports[^1].FilesFound);
        Assert.Equal(7, reports[^1].BytesFound);
    }

    [Fact]
    public void Scan_UnsafeRoot_IsIgnored()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dangerous = new ApprovedLocation("Kötü kök", profile);

        var result = TestSandbox.NoAgeLimitScanner().Scan([dangerous]);

        Assert.Empty(result.Files);
    }

    internal sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
