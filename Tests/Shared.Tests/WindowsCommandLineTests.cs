using Remotely.Shared.Helpers;

namespace Remotely.Shared.Tests;

[TestClass]
public class WindowsCommandLineTests
{
    private const string Exe = @"C:\Program Files\Amarre\Desktop\Remotely_Desktop.exe";

    [TestMethod]
    public void Build_CheminDuProgrammeEntreGuillemets() =>
        Assert.AreEqual($"\"{Exe}\" --mode Chat", WindowsCommandLine.Build(Exe, ["--mode", "Chat"]));

    [TestMethod]
    [DataRow("simple", "simple")]
    [DataRow("", "\"\"")]
    [DataRow("deux mots", "\"deux mots\"")]
    [DataRow("Org\" --access-key \"x", "\"Org\\\" --access-key \\\"x\"")]
    [DataRow(@"C:\dossier\", @"C:\dossier\")]
    [DataRow(@"C:\mon dossier\", "\"C:\\mon dossier\\\\\"")]
    [DataRow("a\\\"b", "\"a\\\\\\\"b\"")]
    [DataRow("ligne\nsuivante", "\"ligne\nsuivante\"")]
    public void Build_Argument(string argument, string expected) =>
        Assert.AreEqual($"\"{Exe}\" {expected}", WindowsCommandLine.Build(Exe, [argument]));

    [TestMethod]
    public void Build_GuillemetDansLeNomDuProgramme_Refuse() =>
        Assert.ThrowsExactly<ArgumentException>(() => WindowsCommandLine.Build("C:\\a\"b.exe", []));
}