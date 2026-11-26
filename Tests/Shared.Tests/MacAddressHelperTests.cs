using Remotely.Shared.Helpers;

namespace Remotely.Shared.Tests;

[TestClass]
public class MacAddressHelperTests
{
    [TestMethod]
    [DataRow("78E3B5A1E45B", "78E3B5A1E45B")]
    [DataRow("78:e3:b5:a1:e4:5b", "78E3B5A1E45B")]
    [DataRow("78-E3-B5-A1-E4-5B", "78E3B5A1E45B")]
    [DataRow("78e3.b5a1.e45b", "78E3B5A1E45B")]
    public void TryNormalize_Valid(string mac, string expected)
    {
        Assert.IsTrue(MacAddressHelper.TryNormalize(mac, out var normalized));
        Assert.AreEqual(expected, normalized);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("78E3B5A1E4")]
    [DataRow("78E3B5A1E45B00")]
    [DataRow("00000000000000E0")]
    [DataRow("78E3B5A1E45G")]
    [DataRow("000000000000")]
    [DataRow("FFFFFFFFFFFF")]
    [DataRow("78:E3:B5:A1:E4:5B:00:11:22:33")]
    public void TryNormalize_Invalid(string? mac)
    {
        Assert.IsFalse(MacAddressHelper.TryNormalize(mac, out var normalized));
        Assert.AreEqual(string.Empty, normalized);
    }

    [TestMethod]
    public void GetBytes_ReturnsSixBytes()
    {
        CollectionAssert.AreEqual(
            new byte[] { 0x78, 0xE3, 0xB5, 0xA1, 0xE4, 0x5B },
            MacAddressHelper.GetBytes("78:E3:B5:A1:E4:5B"));
    }

    [TestMethod]
    public void GetBytes_Invalid_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => MacAddressHelper.GetBytes(new string('A', 2000)));
    }

    [TestMethod]
    public void Sanitize_FiltersDeduplicatesAndCaps()
    {
        var input = new List<string?> { "bad", null, "78E3B5A1E45B", "78:e3:b5:a1:e4:5b" };
        input.AddRange(Enumerable.Range(1, 20).Select(x => $"0200000000{x:X2}"));

        var result = MacAddressHelper.Sanitize(input);

        Assert.HasCount(MacAddressHelper.MaxAddressesPerDevice, result);
        Assert.AreEqual("78E3B5A1E45B", result[0]);
        Assert.AreEqual("020000000001", result[1]);
        Assert.AreEqual(result.Length, result.Distinct().Count());
    }

    [TestMethod]
    public void Sanitize_Null_ReturnsEmpty() =>
        Assert.IsEmpty(MacAddressHelper.Sanitize(null));
}
