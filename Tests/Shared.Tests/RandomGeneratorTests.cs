using Remotely.Shared.Helpers;

namespace Remotely.Shared.Tests;

[TestClass]
public class RandomGeneratorTests
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    [TestMethod]
    public void GenerateString_LongueurEtAlphabet()
    {
        var value = RandomGenerator.GenerateString(20_000);

        Assert.AreEqual(20_000, value.Length);
        Assert.IsTrue(value.All(Alphabet.Contains));
        // Les 62 caractères sortent (le J manquait, le G était doublé).
        CollectionAssert.AreEquivalent(Alphabet.ToCharArray(), value.Distinct().ToArray());
    }

    [TestMethod]
    public void GenerateAccessKey_64Caracteres() =>
        Assert.AreEqual(64, RandomGenerator.GenerateAccessKey().Length);
}