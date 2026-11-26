using Remotely.Shared.Helpers;

namespace Remotely.Shared.Tests;

[TestClass]
public class RestrictedDirectoryTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void Init()
    {
        // mkdtemp sous Linux : parent en 0700, comme le répertoire d'installation de l'agent (0755 root).
        _root = Directory.CreateTempSubdirectory("amarre-rd-").FullName;
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Dossier temporaire.
        }
    }

    [TestMethod]
    public void GetOrCreate_CreatesDirectory()
    {
        var path = Path.Combine(_root, "SharedFiles");

        var result = RestrictedDirectory.GetOrCreate(path);

        Assert.AreEqual(Path.GetFullPath(path), result);
        Assert.IsTrue(Directory.Exists(path));
        if (!OperatingSystem.IsWindows())
        {
            Assert.AreEqual(
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                File.GetUnixFileMode(path));
        }
    }

    [TestMethod]
    public void GetOrCreate_ExistingOwnDirectory_IsAcceptedAndTightened()
    {
        var path = Path.Combine(_root, "SharedFiles");
        Directory.CreateDirectory(path);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, (UnixFileMode)0x1FF); // 0777
        }

        RestrictedDirectory.GetOrCreate(path);

        Assert.IsTrue(Directory.Exists(path));
        if (!OperatingSystem.IsWindows())
        {
            Assert.AreEqual(
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                File.GetUnixFileMode(path));
        }
    }

    [TestMethod]
    public void GetOrCreate_FileAtPath_Throws()
    {
        var path = Path.Combine(_root, "SharedFiles");
        File.WriteAllText(path, "x");

        Assert.ThrowsExactly<IOException>(() => RestrictedDirectory.GetOrCreate(path));
    }

    [TestMethod]
    public void GetOrCreate_SymbolicLink_Throws()
    {
        var target = Directory.CreateDirectory(Path.Combine(_root, "ailleurs")).FullName;
        var path = Path.Combine(_root, "SharedFiles");
        try
        {
            Directory.CreateSymbolicLink(path, target);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            Assert.Inconclusive("Création de lien symbolique refusée sur ce poste (Windows sans mode développeur).");
        }

        Assert.ThrowsExactly<IOException>(() => RestrictedDirectory.GetOrCreate(path));
    }

    [TestMethod]
    public void GetOrCreate_ParentWritableByOthers_Throws()
    {
        // Contrôle propre à Linux et macOS.
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var parent = Directory.CreateDirectory(Path.Combine(_root, "partage")).FullName;
        File.SetUnixFileMode(parent, (UnixFileMode)0x1FF); // 0777

        Assert.ThrowsExactly<IOException>(() => RestrictedDirectory.GetOrCreate(Path.Combine(parent, "SharedFiles")));
    }
}
