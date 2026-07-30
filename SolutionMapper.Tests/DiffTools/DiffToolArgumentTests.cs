using SolutionMapper.DiffTools;

namespace SolutionMapper.Tests.DiffTools;

public class DiffToolArgumentTests
{
    [Fact]
    public void WinMerge_args_include_recursive_and_labels()
    {
        var psi = WinMergeDiffTool.CreateStartInfo(@"C:\WM\WinMergeU.exe", @"C:\L", @"C:\R");
        Assert.Equal("/r", psi.ArgumentList[0]);
        Assert.Contains("Legacy", psi.ArgumentList);
        Assert.Contains(".NET 10", psi.ArgumentList);
        Assert.Equal(@"C:\L", psi.ArgumentList[^2]);
        Assert.Equal(@"C:\R", psi.ArgumentList[^1]);
    }

    [Fact]
    public void BeyondCompare_args_are_left_and_right()
    {
        var psi = BeyondCompareDiffTool.CreateStartInfo(@"C:\BC\BCompare.exe", @"C:\L", @"C:\R");
        Assert.Equal(@"C:\L", psi.ArgumentList[0]);
        Assert.Equal(@"C:\R", psi.ArgumentList[1]);
    }

    [Fact]
    public void VsCode_args_open_both_folders()
    {
        var psi = VsCodeDiffTool.CreateStartInfo(@"C:\code\Code.exe", @"C:\L", @"C:\R");
        Assert.Equal("-n", psi.ArgumentList[0]);
        Assert.Equal(@"C:\L", psi.ArgumentList[1]);
        Assert.Equal(@"C:\R", psi.ArgumentList[2]);
    }

    [Fact]
    public void EmptyFolder_creates_temp_path()
    {
        var path = EmptyFolder.GetPath();
        Assert.True(Directory.Exists(path));
        Assert.Contains(Path.Combine("SolutionMapper", "empty"), path);
    }
}
