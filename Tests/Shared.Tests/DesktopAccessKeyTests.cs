using Remotely.Shared.Helpers;

namespace Remotely.Shared.Tests;

[TestClass]
public class DesktopAccessKeyTests
{
    [TestMethod]
    public void OptionRenseignee_AncienAgent() =>
        Assert.AreEqual("cle-option", DesktopAccessKey.Resolve("cle-option", () => "cle-env", () => "cle-stdin"));

    [TestMethod]
    public void Tiret_LueSurEntreeStandard() =>
        Assert.AreEqual("cle-stdin", DesktopAccessKey.Resolve("-", () => "cle-env", () => "cle-stdin\r"));

    [TestMethod]
    public void Tiret_EntreeStandardFermee_Vide() =>
        Assert.AreEqual(string.Empty, DesktopAccessKey.Resolve("-", () => "cle-env", () => null));

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void SansOption_VariableDEnvironnement(string? option) =>
        Assert.AreEqual("cle-env", DesktopAccessKey.Resolve(option, () => "cle-env", () => throw new AssertFailedException("entrée standard lue")));

    [TestMethod]
    public void Rien_Vide() =>
        Assert.AreEqual(string.Empty, DesktopAccessKey.Resolve(null, () => null, () => null));
}
