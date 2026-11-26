namespace Remotely.Shared.Helpers;

/// <summary>
/// Adresses MAC du réveil à distance (Wake-on-LAN) : 6 octets, rien d'autre.
/// </summary>
public static class MacAddressHelper
{
    /// <summary>Nombre d'adresses gardées par poste (chacune donne un paquet par pair).</summary>
    public const int MaxAddressesPerDevice = 8;

    /// <summary>
    /// Adresse ramenée à 12 chiffres hexadécimaux en majuscules (séparateurs « : », « - »
    /// et « . » acceptés). Refusée : autre longueur, caractère non hexadécimal, adresse nulle
    /// ou de diffusion.
    /// </summary>
    public static bool TryNormalize(string? macAddress, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(macAddress) || macAddress.Length > 17)
        {
            return false;
        }

        var hex = new string(macAddress.Where(x => x is not (':' or '-' or '.')).ToArray());
        if (hex.Length != 12 || !hex.All(char.IsAsciiHexDigit))
        {
            return false;
        }

        hex = hex.ToUpperInvariant();
        if (hex == "000000000000" || hex == "FFFFFFFFFFFF")
        {
            return false;
        }

        normalized = hex;
        return true;
    }

    /// <summary>Les 6 octets d'une adresse valide ; exception sinon.</summary>
    public static byte[] GetBytes(string? macAddress)
    {
        if (!TryNormalize(macAddress, out var normalized))
        {
            throw new ArgumentException("Adresse MAC invalide : 6 octets attendus.", nameof(macAddress));
        }
        return Convert.FromHexString(normalized);
    }

    /// <summary>
    /// Adresses déclarées par un poste : invalides retirées, doublons retirés,
    /// au plus <see cref="MaxAddressesPerDevice"/>.
    /// </summary>
    public static string[] Sanitize(IEnumerable<string?>? macAddresses)
    {
        if (macAddresses is null)
        {
            return [];
        }

        var result = new List<string>();
        foreach (var mac in macAddresses)
        {
            if (TryNormalize(mac, out var normalized) && !result.Contains(normalized))
            {
                result.Add(normalized);
                if (result.Count == MaxAddressesPerDevice)
                {
                    break;
                }
            }
        }
        return [.. result];
    }
}
