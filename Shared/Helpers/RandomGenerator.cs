using System.Security.Cryptography;

namespace Remotely.Shared.Helpers;

public class RandomGenerator
{
    private const string AllowableCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    // Tirage uniforme sur les 62 caractères (pas de modulo sur un octet, qui favorisait les premiers).
    public static string GenerateString(int length) =>
        RandomNumberGenerator.GetString(AllowableCharacters, length);

    public static string GenerateAccessKey()
    {
        return GenerateString(64);
    }
}
