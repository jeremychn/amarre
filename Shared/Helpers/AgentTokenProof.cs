using System.Security.Cryptography;
using System.Text;

namespace Remotely.Shared.Helpers;

/// <summary>
/// Le jeton d'un poste ne transite plus après l'enrôlement : agent et serveur prouvent qu'ils le connaissent
/// en signant le défi de l'autre. Un faux serveur ne reçoit donc rien qu'il puisse renvoyer.
/// </summary>
/// <remarks>
/// ponytail: pas de liaison au canal TLS ; un attaquant qui relaie en direct vers le vrai serveur passerait encore.
/// </remarks>
public static class AgentTokenProof
{
    public static string NewChallenge() => RandomGenerator.GenerateString(32);

    public static string FromAgent(string token, string serverChallenge) => Compute(token, "agent:" + serverChallenge);

    public static string FromServer(string token, string agentChallenge) => Compute(token, "server:" + agentChallenge);

    public static bool AreEqual(string? expected, string? actual)
    {
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected ?? string.Empty),
            Encoding.UTF8.GetBytes(actual ?? string.Empty));
    }

    private static string Compute(string token, string message)
    {
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(message)));
    }
}
