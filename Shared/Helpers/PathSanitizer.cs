namespace Remotely.Shared.Helpers;

public static class PathSanitizer
{
    /// <summary>
    /// Sanitizes a file name by removing invalid characters.
    /// </summary>
    /// <param name="fileName"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static string SanitizeFileName(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            throw new ArgumentNullException(nameof(fileName));
        }

        var invalidChars = Path.GetInvalidFileNameChars().ToHashSet();
        var validChars = fileName.Where(x => !invalidChars.Contains(x));
        return new string(validChars.ToArray());
    }

    private static readonly HashSet<string> _windowsDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    /// Nom de fichier reçu d'un tiers (viewer) réduit à un simple nom, à écrire dans un dossier
    /// choisi par l'appelant : chemin retiré (« / » et « \ », absolu ou « .. »), caractères
    /// interdits et de contrôle retirés, points et espaces finaux retirés (ignorés par Windows),
    /// noms de périphériques Windows préfixés. Vide après nettoyage : « fichier ».
    /// </summary>
    public static string GetSafeFileName(string? fileName)
    {
        var name = Path.GetFileName((fileName ?? string.Empty).Replace('\\', '/'));
        var invalidChars = Path.GetInvalidFileNameChars().ToHashSet();
        name = new string(name.Where(x => !invalidChars.Contains(x) && !char.IsControl(x) && x != ':').ToArray());
        name = name.Trim().TrimEnd('.', ' ');

        if (string.IsNullOrEmpty(name))
        {
            return "fichier";
        }

        var stem = name.Split('.')[0].TrimEnd(' ');
        return _windowsDeviceNames.Contains(stem) ? "_" + name : name;
    }

    /// <summary>
    /// Sanitizes a path by removing invalid characters.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static string SanitizePath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentNullException(nameof(path));
        }

        var invalidChars = Path.GetInvalidPathChars().ToHashSet();
        var validChars = path.Where(x => !invalidChars.Contains(x));
        return new string(validChars.ToArray());
    }
}
