using Remotely.Shared.Helpers;
using System.Security.Cryptography;
using System.Text;

namespace Remotely.Shared.Tests;

[TestClass]
public class UpdateManifestTests
{
    private string _dir = "";
    private string _zip = "";
    private string _script = "";
    private ECDsa _key = null!;
    private string _publicKey = "";

    [TestInitialize]
    public void Init()
    {
        _dir = Directory.CreateTempSubdirectory("amarre-test-").FullName;
        _zip = Path.Combine(_dir, "Remotely-Win-x64.zip");
        _script = Path.Combine(_dir, "Install-Remotely.ps1");
        File.WriteAllText(_zip, "paquet");
        File.WriteAllText(_script, "script");
        _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        _publicKey = Convert.ToBase64String(_key.ExportSubjectPublicKeyInfo());
    }

    [TestCleanup]
    public void Cleanup()
    {
        _key.Dispose();
        Directory.Delete(_dir, true);
    }

    // Écrit par le même code que l'outil de signature (Utilities/amarre-maj.cs)
    private string Sign(ECDsa key, string version = "1.28")
    {
        File.WriteAllText(Path.Combine(_dir, "AgentVersion.txt"), version);
        return UpdateManifest.Write(_dir, key);
    }

    private string? Check(string manifest, string version = "1.27.1") =>
        UpdateManifest.Check(manifest, [_publicKey], version,
            ("Remotely-Win-x64.zip", _zip), ("Install-Remotely.ps1", _script));

    [TestMethod]
    public void ValidSignature_Accepted()
    {
        Assert.IsNull(Check(Sign(_key)));
        Assert.IsNull(Check(Sign(_key), "1.28.0.0"), "même version, écrite autrement");
    }

    [TestMethod]
    public void ProjectKeys_AreP256PublicKeys()
    {
        Assert.HasCount(2, UpdateManifest.ProjectKeys.Distinct());
        foreach (var key in UpdateManifest.ProjectKeys)
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(key), out _);
            Assert.AreEqual(256, ecdsa.KeySize);
        }
    }

    [TestMethod]
    public void ModifiedPackage_Refused()
    {
        var manifest = Sign(_key);
        File.WriteAllText(_zip, "paquet piégé");
        StringAssert.Contains(Check(manifest), "Remotely-Win-x64.zip");
    }

    [TestMethod]
    public void ModifiedScript_Refused()
    {
        var manifest = Sign(_key);
        File.AppendAllText(_script, "\nInvoke-Malveillant");
        StringAssert.Contains(Check(manifest), "Install-Remotely.ps1");
    }

    [TestMethod]
    public void ModifiedManifest_Refused()
    {
        // Le serveur remplace l'empreinte par celle de son paquet piégé : la signature ne colle plus.
        var manifest = Sign(_key);
        File.WriteAllText(_zip, "paquet piégé");
        var forged = manifest.Replace(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("paquet"))),
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(_zip))));
        Assert.AreNotEqual(manifest, forged);
        Assert.AreEqual("signature invalide ou clé inconnue", Check(forged));
    }

    [TestMethod]
    public void UnknownKey_Refused()
    {
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Assert.AreEqual("signature invalide ou clé inconnue", Check(Sign(other)));
    }

    [TestMethod]
    public void OlderVersion_Refused()
    {
        StringAssert.Contains(Check(Sign(_key, "1.27")), "plus ancienne");
    }

    [TestMethod]
    public void FileMissingFromManifest_Refused()
    {
        var result = UpdateManifest.Check(Sign(_key), [_publicKey], "1.27",
            ("Remotely-Linux.zip", _zip));
        Assert.AreEqual("Remotely-Linux.zip absent du manifeste", result);
    }

    [TestMethod]
    public void Garbage_Refused()
    {
        Assert.AreEqual("manifeste illisible", Check("<html>404</html>"));
        Assert.AreEqual("signature illisible", Check("amarre-manifest 1\nversion 1.28\nsignature ???\n"));
    }

    [TestMethod]
    public void CheckPackage_MissingManifest_RefusedUnlessBypass()
    {
        var installDir = Directory.CreateDirectory(Path.Combine(_dir, "agent")).FullName;
        var missing = Path.Combine(_dir, "Manifest.txt");

        Assert.AreEqual("manifeste absent", UpdateManifest.CheckPackage(missing, installDir, "1.27", ("Remotely-Win-x64.zip", _zip)));

        File.WriteAllText(Path.Combine(installDir, UpdateManifest.BypassFileName), "");
        Assert.IsNull(UpdateManifest.CheckPackage(missing, installDir, "1.27", ("Remotely-Win-x64.zip", _zip)));
    }

    [TestMethod]
    public void CheckPackage_ExtraKeyOfInstalledAgent_Accepted()
    {
        var installDir = Directory.CreateDirectory(Path.Combine(_dir, "agent")).FullName;
        var manifestPath = Path.Combine(_dir, "Manifest.txt");
        File.WriteAllText(manifestPath, Sign(_key));

        // Clé de l'instance (APEI) inconnue de l'agent : refus.
        Assert.AreEqual("signature invalide ou clé inconnue",
            UpdateManifest.CheckPackage(manifestPath, installDir, "1.27", ("Remotely-Win-x64.zip", _zip)));

        File.WriteAllText(Path.Combine(installDir, UpdateManifest.ExtraKeysFileName), _publicKey + "\r\n");
        Assert.IsNull(UpdateManifest.CheckPackage(manifestPath, installDir, "1.27", ("Remotely-Win-x64.zip", _zip)));
    }

    [TestMethod]
    public void FillBashInstaller_FillsAndRejectsInjection()
    {
        var org = Guid.NewGuid().ToString();
        var filled = UpdateManifest.FillBashInstaller("#!/bin/bash\r\nHostName=\r\nOrganization=\r\necho ok", "https://amarre.example", org);
        Assert.AreEqual($"#!/bin/bash\nHostName=\"https://amarre.example\"\nOrganization=\"{org}\"\necho ok", filled);

        Assert.ThrowsExactly<ArgumentException>(() =>
            UpdateManifest.FillBashInstaller("HostName=\nOrganization=", "https://x\"; rm -rf /; \"", org));
        Assert.ThrowsExactly<ArgumentException>(() =>
            UpdateManifest.FillBashInstaller("HostName=\nOrganization=", "https://x", "pas-un-guid"));
    }
}
