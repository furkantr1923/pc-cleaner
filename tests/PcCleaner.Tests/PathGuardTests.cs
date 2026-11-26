using PcCleaner.Core;

namespace PcCleaner.Tests;

public class PathGuardTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "pcg-root");

    [Fact]
    public void IsStrictlyInside_AcceptsChildPaths()
    {
        Assert.True(PathGuard.IsStrictlyInside(Path.Combine(Root, "a.tmp"), Root));
        Assert.True(PathGuard.IsStrictlyInside(Path.Combine(Root, "x", "y", "z.tmp"), Root));
    }

    [Fact]
    public void IsStrictlyInside_RejectsRootItself()
    {
        Assert.False(PathGuard.IsStrictlyInside(Root, Root));
        Assert.False(PathGuard.IsStrictlyInside(Root + Path.DirectorySeparatorChar, Root));
    }

    [Fact]
    public void IsStrictlyInside_RejectsTraversal()
    {
        Assert.False(PathGuard.IsStrictlyInside(Path.Combine(Root, "..", "other.txt"), Root));
        Assert.False(PathGuard.IsStrictlyInside(Path.Combine(Root, "sub", "..", "..", "x"), Root));
    }

    [Fact]
    public void IsStrictlyInside_RejectsSiblingWithSamePrefix()
    {
        // "pcg-root-evil" "pcg-root" ile başlıyor ama içinde DEĞİL.
        Assert.False(PathGuard.IsStrictlyInside(Root + "-evil" + Path.DirectorySeparatorChar + "a.tmp", Root));
    }

    [Fact]
    public void IsStrictlyInside_RejectsEmptyAndInvalid()
    {
        Assert.False(PathGuard.IsStrictlyInside("", Root));
        Assert.False(PathGuard.IsStrictlyInside("   ", Root));
        Assert.False(PathGuard.IsStrictlyInside(Path.Combine(Root, "a"), ""));
    }

    [Fact]
    public void IsSafeRoot_RejectsDriveRootAndPersonalFolders()
    {
        Assert.False(PathGuard.IsSafeRoot(Path.GetPathRoot(Path.GetTempPath())!));
        Assert.False(PathGuard.IsSafeRoot(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));

        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrEmpty(docs))
            Assert.False(PathGuard.IsSafeRoot(docs));

        string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        Assert.False(PathGuard.IsSafeRoot(downloads));
    }

    [Fact]
    public void IsSafeRoot_RejectsParentOfProtectedFolder()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string? parent = Path.GetDirectoryName(profile);
        if (parent is not null)
            Assert.False(PathGuard.IsSafeRoot(parent));
    }

    [Fact]
    public void IsSafeRoot_AcceptsRegularTempFolder()
    {
        Assert.True(PathGuard.IsSafeRoot(Path.Combine(Path.GetTempPath(), "some-app-temp")));
    }
}

public class TempLocationsTests
{
    [Fact]
    public void GetDefault_ReturnsOnlyExistingSafeAndDistinctLocations()
    {
        var locations = TempLocations.GetDefault();

        Assert.NotEmpty(locations); // en azından sistem geçici klasörü var olmalı
        Assert.All(locations, l =>
        {
            Assert.True(Directory.Exists(l.RootPath));
            Assert.True(PathGuard.IsSafeRoot(l.RootPath));
        });
        Assert.Equal(locations.Count, locations.Select(l => l.RootPath).Distinct().Count());
        Assert.Contains(locations, l => PathGuard.PathEquals(l.RootPath, PathGuard.ToLongPath(Path.GetTempPath())));
    }

    [Fact]
    public void Filter_RemovesDuplicatesNestedMissingAndUnsafe()
    {
        using var s = new TestSandbox();
        string nested = Path.Combine(s.TempRoot, "nested");
        Directory.CreateDirectory(nested);

        var result = TempLocations.Filter(
        [
            new("A", s.TempRoot),
            new("A-dup", s.TempRoot + Path.DirectorySeparatorChar),
            new("Nested", nested),
            new("Missing", Path.Combine(s.Base, "does-not-exist")),
            new("Profile", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
        ]);

        Assert.Single(result);
        Assert.Equal("A", result[0].DisplayName);
    }
}

public class SizeFormatterTests
{
    [Theory]
    [InlineData(0, "0 bayt")]
    [InlineData(512, "512 bayt")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1,5 KB")]
    [InlineData(10L * 1024 * 1024, "10 MB")]
    [InlineData(3L * 1024 * 1024 * 1024 + 512L * 1024 * 1024, "3,5 GB")]
    public void Format_UsesTurkishUnitsAndDecimalComma(long bytes, string expected)
    {
        Assert.Equal(expected, SizeFormatter.Format(bytes));
    }
}
