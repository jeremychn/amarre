using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Shared.Helpers;
using System.IO;
using System.Linq;

namespace Remotely.Shared.Tests;

[TestClass]
public class DeviceDirectoryTests
{
    [TestMethod]
    public void List_DossiersDAbordPuisFichiers_ParNom()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "b.txt"), "12345");
            File.WriteAllText(Path.Combine(root, "a.txt"), "1");
            Directory.CreateDirectory(Path.Combine(root, "z"));

            var listing = DeviceDirectory.List(root);

            Assert.AreEqual(string.Empty, listing.Error);
            CollectionAssert.AreEqual(new[] { "z", "a.txt", "b.txt" }, listing.Entries.Select(x => x.Name).ToArray());
            Assert.IsTrue(listing.Entries[0].IsDirectory);
            Assert.AreEqual(5, listing.Entries[2].Size);
            Assert.AreEqual(Directory.GetParent(root)!.FullName, listing.ParentPath);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void List_DossierInexistant_Erreur() =>
        Assert.AreEqual("Dossier introuvable.", DeviceDirectory.List(Path.Combine(Path.GetTempPath(), "absent-" + Path.GetRandomFileName())).Error);

    [TestMethod]
    public void List_Racine_NonVide() =>
        Assert.IsTrue(DeviceDirectory.List(string.Empty).Entries.Count > 0);

    [TestMethod]
    public void GetFreePath_NEcrasePas()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            Assert.AreEqual(Path.Combine(root, "f.txt"), DeviceDirectory.GetFreePath(root, "f.txt"));
            File.WriteAllText(Path.Combine(root, "f.txt"), "");
            Assert.AreEqual(Path.Combine(root, "f (2).txt"), DeviceDirectory.GetFreePath(root, "f.txt"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
