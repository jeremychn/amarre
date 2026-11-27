using Remotely.Shared.Models;
using Remotely.Shared.Services;
using System.Buffers.Binary;

namespace Remotely.Shared.Tests;

[TestClass]
public class EmbeddedServerDataTests
{
    private static readonly EmbeddedServerData _data = new(new Uri("https://remote.example.org"), "org-42");

    [TestMethod]
    public void NonSigne_RelueQuelQueSoitLeNom()
    {
        var path = Path.Combine(Path.GetTempPath(), $"Mon assistance {Guid.NewGuid()}.exe");
        File.WriteAllBytes(path, EmbeddedServerDataProvider.Instance.Embed([0x4D, 0x5A, 1, 2, 3], _data));
        try
        {
            AssertReadBack(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Signe_TableDeCertificatsAllongee()
    {
        // PE32+ minimal : en-tête PE à 0x40, table de certificats de 16 octets en fin de fichier.
        var pe = new byte[0x200];
        pe[0] = (byte)'M'; pe[1] = (byte)'Z';
        BinaryPrimitives.WriteInt32LittleEndian(pe.AsSpan(0x3C), 0x40);
        "PE\0\0"u8.CopyTo(pe.AsSpan(0x40));
        BinaryPrimitives.WriteUInt16LittleEndian(pe.AsSpan(0x40 + 24), 0x20B);
        var securityEntry = 0x40 + 24 + 112 + 4 * 8;
        BinaryPrimitives.WriteInt32LittleEndian(pe.AsSpan(securityEntry), 0x1F0);
        BinaryPrimitives.WriteInt32LittleEndian(pe.AsSpan(securityEntry + 4), 16);
        BinaryPrimitives.WriteInt32LittleEndian(pe.AsSpan(0x1F0), 13); // dwLength non aligné, comme souvent

        var tagged = EmbeddedServerDataProvider.Instance.Embed(pe, _data);
        var added = tagged.Length - pe.Length;

        Assert.AreEqual(0, added % 8);
        Assert.AreEqual(16 + added, BinaryPrimitives.ReadInt32LittleEndian(tagged.AsSpan(securityEntry + 4)));
        Assert.AreEqual(16 + added, BinaryPrimitives.ReadInt32LittleEndian(tagged.AsSpan(0x1F0)));
    }

    [TestMethod]
    public void AncienFormat_NomDeFichierToujoursLu()
    {
        var encoded = Convert.ToBase64String(MessagePack.MessagePackSerializer.Serialize(_data)).Replace("/", "_").Replace("+", "-");
        var path = Path.Combine(Path.GetTempPath(), $"Remotely_Desktop[{encoded}].exe");
        File.WriteAllBytes(path, [0x4D, 0x5A]);
        try
        {
            AssertReadBack(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void AssertReadBack(string path)
    {
        var result = EmbeddedServerDataProvider.Instance.TryGetEmbeddedData(path);
        Assert.IsTrue(result.IsSuccess, result.Reason);
        Assert.AreEqual("https://remote.example.org/", result.Value.ServerUrl.AbsoluteUri);
        Assert.AreEqual("org-42", result.Value.OrganizationId);
    }
}
