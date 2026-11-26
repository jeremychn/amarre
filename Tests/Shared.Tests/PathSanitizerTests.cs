using Remotely.Shared.Helpers;

namespace Remotely.Shared.Tests;

[TestClass]
public class PathSanitizerTests
{
    [TestMethod]
    [DataRow("rapport.pdf", "rapport.pdf")]
    [DataRow("../../etc/cron.d/x", "x")]
    [DataRow(@"..\..\Windows\System32\x.dll", "x.dll")]
    [DataRow(@"C:\Users\Public\a.txt", "a.txt")]
    [DataRow("/root/.ssh/authorized_keys", "authorized_keys")]
    [DataRow(@"\\serveur\partage\b.txt", "b.txt")]
    [DataRow("a.txt:flux", "a.txtflux")]
    [DataRow("fin.  ", "fin")]
    [DataRow("CON", "_CON")]
    [DataRow("nul.txt", "_nul.txt")]
    [DataRow("console.txt", "console.txt")]
    [DataRow("..", "fichier")]
    [DataRow("dossier/", "fichier")]
    [DataRow("", "fichier")]
    [DataRow(null, "fichier")]
    public void GetSafeFileName(string? fileName, string expected) =>
        Assert.AreEqual(expected, PathSanitizer.GetSafeFileName(fileName));
}