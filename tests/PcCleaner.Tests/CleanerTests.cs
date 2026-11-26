using PcCleaner.Core;

namespace PcCleaner.Tests;

public class CleanerTests
{
    private static TempFile Find(ScanResult r, string relative) =>
        r.Files.Single(f => f.RelativePath == relative);

    [Fact]
    public void Delete_RemovesOnlySelectedEligibleFiles()
    {
        using var s = new TestSandbox();
        string a = s.CreateFile("a.tmp", 100);
        string b = s.CreateFile(Path.Combine("x", "b.tmp"), 200);
        string keep = s.CreateFile("keep.tmp", 300);
        var scan = TestSandbox.NoAgeLimitScanner().Scan([s.Location]);

        var result = new Cleaner([s.Location]).Delete([Find(scan, "a.tmp"), Find(scan, Path.Combine("x", "b.tmp"))]);

        Assert.Equal(2, result.DeletedCount);
        Assert.Equal(300, result.DeletedBytes);
        Assert.False(File.Exists(a));
        Assert.False(File.Exists(b));
        Assert.True(File.Exists(keep)); // seçilmeyen dosyaya dokunulmadı
    }

    [Fact]
    public void Delete_EmptySelection_DeletesNothing()
    {
        using var s = new TestSandbox();
        string a = s.CreateFile("a.tmp", 100);

        var result = new Cleaner([s.Location]).Delete([]);

        Assert.Equal(0, result.DeletedCount);
        Assert.True(File.Exists(a));
    }

    [Fact]
    public void Delete_FileOutsideApprovedRoot_IsRejected()
    {
        using var s = new TestSandbox();
        string personal = s.CreateFile("cv.docx", 50, root: s.Outside);
        var forged = new TempFile(s.Location, personal, "cv.docx", 50, DateTime.UtcNow);

        var result = new Cleaner([s.Location]).Delete([forged]);

        Assert.Equal(DeleteOutcome.Rejected, result.Items.Single().Outcome);
        Assert.True(File.Exists(personal));
    }

    [Fact]
    public void Delete_PathTraversal_IsRejected()
    {
        using var s = new TestSandbox();
        string personal = s.CreateFile("photo.jpg", 50, root: s.Outside);
        string traversal = Path.Combine(s.TempRoot, "..", "Personal", "photo.jpg");
        var forged = new TempFile(s.Location, traversal, Path.Combine("..", "Personal", "photo.jpg"), 50, DateTime.UtcNow);

        var result = new Cleaner([s.Location]).Delete([forged]);

        Assert.Equal(DeleteOutcome.Rejected, result.Items.Single().Outcome);
        Assert.True(File.Exists(personal));
    }

    [Fact]
    public void Delete_FileFromUnapprovedLocation_IsRejected()
    {
        using var s = new TestSandbox();
        string f = s.CreateFile("data.bin", 10, root: s.Outside);
        var otherLocation = new ApprovedLocation("Onaysız", s.Outside);
        var file = new TempFile(otherLocation, f, "data.bin", 10, DateTime.UtcNow);

        // Cleaner yalnızca s.Location'ı onaylı biliyor.
        var result = new Cleaner([s.Location]).Delete([file]);

        Assert.Equal(DeleteOutcome.Rejected, result.Items.Single().Outcome);
        Assert.True(File.Exists(f));
    }

    [Fact]
    public void Delete_ThroughSymlinkedFolder_IsRejected_AndTargetSurvives()
    {
        using var s = new TestSandbox();
        string secret = s.CreateFile("secret.txt", 10, root: s.Outside);
        string link = Path.Combine(s.TempRoot, "linkdir");
        Directory.CreateSymbolicLink(link, s.Outside);

        var forged = new TempFile(s.Location, Path.Combine(link, "secret.txt"), Path.Combine("linkdir", "secret.txt"), 10, DateTime.UtcNow);
        var result = new Cleaner([s.Location]).Delete([forged]);

        Assert.Equal(DeleteOutcome.Rejected, result.Items.Single().Outcome);
        Assert.True(File.Exists(secret));
    }

    [Fact]
    public void Delete_SymlinkFile_IsRejected_AndTargetSurvives()
    {
        using var s = new TestSandbox();
        string secret = s.CreateFile("secret.txt", 10, root: s.Outside);
        string link = Path.Combine(s.TempRoot, "link.txt");
        File.CreateSymbolicLink(link, secret);

        var forged = new TempFile(s.Location, link, "link.txt", 10, DateTime.UtcNow);
        var result = new Cleaner([s.Location]).Delete([forged]);

        Assert.Equal(DeleteOutcome.Rejected, result.Items.Single().Outcome);
        Assert.True(File.Exists(secret));
        Assert.True(File.Exists(link));
    }

    [Fact]
    public void Delete_LockedFile_IsSkippedAsInUse_OthersStillDeleted()
    {
        using var s = new TestSandbox();
        string locked = s.CreateFile("locked.tmp", 10);
        string free = s.CreateFile("free.tmp", 10);
        var scan = TestSandbox.NoAgeLimitScanner().Scan([s.Location]);

        DeleteOutcome lockedOutcome;
        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var result = new Cleaner([s.Location]).Delete([Find(scan, "locked.tmp"), Find(scan, "free.tmp")]);
            lockedOutcome = result.Items.Single(i => i.File.RelativePath == "locked.tmp").Outcome;
            Assert.Equal(1, result.DeletedCount);
        }

        Assert.Equal(DeleteOutcome.InUse, lockedOutcome);
        Assert.True(File.Exists(locked));
        Assert.False(File.Exists(free));
    }

    [Fact]
    public void Delete_FileBecameReadOnlyAfterScan_IsNotTouched()
    {
        using var s = new TestSandbox();
        string f = s.CreateFile("ro.tmp", 10);
        var scan = TestSandbox.NoAgeLimitScanner().Scan([s.Location]);
        File.SetAttributes(f, FileAttributes.ReadOnly);

        var result = new Cleaner([s.Location]).Delete([Find(scan, "ro.tmp")]);

        Assert.Equal(DeleteOutcome.Changed, result.Items.Single().Outcome);
        Assert.True(File.Exists(f));
        File.SetAttributes(f, FileAttributes.Normal);
    }

    [Fact]
    public void Delete_AlreadyRemovedFile_ReportsNotFound()
    {
        using var s = new TestSandbox();
        string f = s.CreateFile("gone.tmp", 10);
        var scan = TestSandbox.NoAgeLimitScanner().Scan([s.Location]);
        File.Delete(f);

        var result = new Cleaner([s.Location]).Delete([Find(scan, "gone.tmp")]);

        Assert.Equal(DeleteOutcome.NotFound, result.Items.Single().Outcome);
    }

    [UnixNonRootFact]
    public void Delete_PermissionDenied_IsSkippedAndReported()
    {
        using var s = new TestSandbox();
        string f = s.CreateFile(Path.Combine("protected", "p.tmp"), 10);
        var scan = TestSandbox.NoAgeLimitScanner().Scan([s.Location]);
        // Klasöre yazma izni yok → içindeki dosya silinemez.
        TestSandbox.TryChmod(Path.Combine(s.TempRoot, "protected"), UnixFileMode.UserRead | UnixFileMode.UserExecute);

        var result = new Cleaner([s.Location]).Delete([Find(scan, Path.Combine("protected", "p.tmp"))]);

        Assert.Equal(DeleteOutcome.AccessDenied, result.Items.Single().Outcome);
        Assert.True(File.Exists(f));
    }

    [Fact]
    public void Delete_CancelledBeforeStart_DeletesNothing()
    {
        using var s = new TestSandbox();
        string a = s.CreateFile("a.tmp", 10);
        var scan = TestSandbox.NoAgeLimitScanner().Scan([s.Location]);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = new Cleaner([s.Location]).Delete(scan.Files, null, cts.Token);

        Assert.True(result.Cancelled);
        Assert.Empty(result.Items);
        Assert.True(File.Exists(a));
    }

    [Fact]
    public void Delete_DirectoryPath_IsRejected()
    {
        using var s = new TestSandbox();
        string dir = Path.Combine(s.TempRoot, "folder");
        Directory.CreateDirectory(dir);
        var forged = new TempFile(s.Location, dir, "folder", 0, DateTime.UtcNow);

        var result = new Cleaner([s.Location]).Delete([forged]);

        Assert.Equal(DeleteOutcome.Rejected, result.Items.Single().Outcome);
        Assert.True(Directory.Exists(dir));
    }

    [Fact]
    public void Delete_RootItself_IsRejected()
    {
        using var s = new TestSandbox();
        var forged = new TempFile(s.Location, s.TempRoot, ".", 0, DateTime.UtcNow);

        var result = new Cleaner([s.Location]).Delete([forged]);

        Assert.Equal(DeleteOutcome.Rejected, result.Items.Single().Outcome);
        Assert.True(Directory.Exists(s.TempRoot));
    }
}
