using Microsoft.Win32;

namespace Remotely.Shared.Utilities;

/// <summary>
/// UUID matériel (SMBIOS) du poste : distingue un poste cloné de l'original, même avec le même nom.
/// Ne change pas avec le wifi, le RJ45 ou un dock. Vide si illisible ou bidon : pas de contrôle dans ce cas.
/// </summary>
public static class HardwareId
{
    public static string Get()
    {
        try
        {
            string? raw = null;
            if (OperatingSystem.IsWindows())
            {
                // Mis à jour par Windows au démarrage quand le matériel change.
                raw = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\HardwareConfig", "LastConfig", null) as string;
            }
            else if (OperatingSystem.IsLinux() && File.Exists("/sys/class/dmi/id/product_uuid"))
            {
                raw = File.ReadAllText("/sys/class/dmi/id/product_uuid");
            }
            // ponytail: macOS non géré (IOPlatformUUID via ioreg) : vide, donc comportement d'avant.
            return Normalize(raw);
        }
        catch
        {
            return string.Empty;
        }
    }

    // Valeurs par défaut de certaines cartes mères : partagées par des milliers de postes.
    public static string Normalize(string? raw)
    {
        if (!Guid.TryParse(raw?.Trim(), out var guid))
        {
            return string.Empty;
        }

        var value = guid.ToString();
        if (value.Replace("-", "").Distinct().Count() == 1 || value == "03000200-0400-0500-0006-000700080009")
        {
            return string.Empty;
        }
        return value;
    }
}
