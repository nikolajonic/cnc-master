using CNC.Infrastructure.Paths;
using CNC.Tests.TestSupport;

namespace CNC.Tests.Infrastructure;

public sealed class AppPathsTests
{
    [Fact]
    public void Subdirectories_AreUnderRoot()
    {
        using var temp = new TempDirectory();

        var paths = new AppPaths(temp.Path);

        Assert.Equal(Path.Combine(temp.Path, "logs"), paths.LogsDirectory);
        Assert.Equal(Path.Combine(temp.Path, "config"), paths.ConfigurationDirectory);
    }

    [Fact]
    public void EnsureCreated_CreatesAllDirectories()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(Path.Combine(temp.Path, "nested", "root"));

        paths.EnsureCreated();

        Assert.True(Directory.Exists(paths.RootDirectory));
        Assert.True(Directory.Exists(paths.LogsDirectory));
        Assert.True(Directory.Exists(paths.ConfigurationDirectory));
    }

    [Fact]
    public void CreateDefault_UsesApplicationFolderName()
    {
        var paths = AppPaths.CreateDefault();

        Assert.Equal(AppPaths.ApplicationFolderName, Path.GetFileName(paths.RootDirectory));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsBlankRoot(string root)
    {
        Assert.Throws<ArgumentException>(() => new AppPaths(root));
    }
}
