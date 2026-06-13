using ContractClause.Shared;

namespace ContractClause.Tests;

public class DotEnvTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("dotenv-test").FullName;

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
        Environment.SetEnvironmentVariable("DOTENV_TEST_VALUE", null);
        Environment.SetEnvironmentVariable("DOTENV_TEST_EXISTING", null);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Load_SetsVariables_SkipsCommentsAndBlanks()
    {
        File.WriteAllLines(Path.Combine(_dir, ".env"),
            ["# comment", "", "DOTENV_TEST_VALUE=hello=world", "not-a-pair"]);

        DotEnv.Load(_dir);

        // value keeps everything after the first '='
        Assert.Equal("hello=world", Environment.GetEnvironmentVariable("DOTENV_TEST_VALUE"));
    }

    [Fact]
    public void Load_DoesNotOverwriteExistingVariables()
    {
        Environment.SetEnvironmentVariable("DOTENV_TEST_EXISTING", "original");
        File.WriteAllLines(Path.Combine(_dir, ".env"), ["DOTENV_TEST_EXISTING=overwritten"]);

        DotEnv.Load(_dir);

        Assert.Equal("original", Environment.GetEnvironmentVariable("DOTENV_TEST_EXISTING"));
    }

    [Fact]
    public void Load_FindsFileInParentDirectory()
    {
        File.WriteAllLines(Path.Combine(_dir, ".env"), ["DOTENV_TEST_VALUE=from-parent"]);
        var child = Directory.CreateDirectory(Path.Combine(_dir, "a", "b")).FullName;

        DotEnv.Load(child);

        Assert.Equal("from-parent", Environment.GetEnvironmentVariable("DOTENV_TEST_VALUE"));
    }
}
