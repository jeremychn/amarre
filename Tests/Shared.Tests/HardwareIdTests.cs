using Remotely.Shared.Utilities;

namespace Remotely.Shared.Tests;

[TestClass]
public class HardwareIdTests
{
    [TestMethod]
    public void Normalize_AccoladesEtCasse() =>
        Assert.AreEqual("4c4c4544-0035-3010-8052-b4c04f4a4d32", HardwareId.Normalize("{4C4C4544-0035-3010-8052-B4C04F4A4D32}\n"));

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("pas un uuid")]
    [DataRow("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")]
    [DataRow("00000000-0000-0000-0000-000000000000")]
    [DataRow("03000200-0400-0500-0006-000700080009")]
    public void Normalize_ValeurBidon_Vide(string? raw) => Assert.AreEqual(string.Empty, HardwareId.Normalize(raw));
}
