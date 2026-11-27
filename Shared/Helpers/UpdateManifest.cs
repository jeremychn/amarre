using System.Security.Cryptography;
using System.Text;

namespace Remotely.Shared.Helpers;

/// <summary>
/// Manifeste signé des mises à jour d'agent (<c>Content/Manifest.txt</c>, écrit par <c>Utilities/Sign-Manifest.ps1</c>) :
/// <code>
/// amarre-manifest 1
/// version 1.28
/// sha256 &lt;hex&gt; &lt;fichier&gt;     (un par fichier du dossier Content)
/// signature &lt;base64&gt;         (ECDSA P-256, SHA-256, sur tout ce qui précède)
/// </code>
/// L'agent n'installe un paquet et son script que s'ils y figurent avec la bonne empreinte, sous une signature
/// d'une clé de confiance, et pour une version au moins égale à la sienne. Un serveur ou un volume content
/// compromis ne peut donc pas pousser de mise à jour piégée.
/// </summary>
public static class UpdateManifest
{
    public const string FileName = "Manifest.txt";

    /// <summary>Clés publiques en plus (une par ligne), livrées dans le paquet de l'agent : clé d'une instance
    /// qui re-signe ses agents en Authenticode. Lue dans le dossier de l'agent installé, donc venue d'un paquet déjà vérifié.</summary>
    public const string ExtraKeysFileName = "cles-maj.pub";

    /// <summary>Posé à la main par un administrateur du poste : la prochaine mise à jour passe sans vérification.
    /// L'agent l'efface en s'en servant.</summary>
    public const string BypassFileName = "AutoriserMajNonSignee";

    /// <summary>Clés publiques du projet (SubjectPublicKeyInfo en base64) : principale (secret GitHub
    /// AMARRE_UPDATE_KEY), puis secours (hors ligne). Voir docs/GUIDE.md, « Signature des mises à jour ».</summary>
    public static readonly string[] ProjectKeys =
    [
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEMWjWb3+766rzmyrrZICedgjLwDMkZF+n0uV80RYgKPYwWo8+gazx0CCWoesxg10Uwo4jHcdhKCHUYaSQrq/AYg==",
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEUaMQCFXKU8TnktWigd9tWns2z6Jamj6grTjkZvWaMIcy9p0L7NLZpgb5RbR9RL6bDu+sOmFnEJzoZqBqqNoZJA==",
    ];

    public static bool IsBypassed(string installDir) => File.Exists(Path.Combine(installDir, BypassFileName));

    /// <summary>
    /// Écrit le manifeste signé du dossier Content (tous ses fichiers de premier niveau, version lue dans
    /// AgentVersion.txt) et le renvoie. Dernier geste avant le dépôt : tout fichier modifié après ne colle plus.
    /// </summary>
    public static string Write(string contentDir, ECDsa key)
    {
        var version = File.ReadAllText(Path.Combine(contentDir, "AgentVersion.txt")).Trim();
        var body = new StringBuilder($"amarre-manifest 1\nversion {version}\n");
        foreach (var file in Directory.GetFiles(contentDir).Where(x => Path.GetFileName(x) != FileName).Order(StringComparer.Ordinal))
        {
            using var stream = File.OpenRead(file);
            body.Append($"sha256 {Convert.ToHexString(SHA256.HashData(stream))} {Path.GetFileName(file)}\n");
        }

        var signature = key.SignData(Encoding.UTF8.GetBytes(body.ToString()), HashAlgorithmName.SHA256);
        var manifest = body + $"signature {Convert.ToBase64String(signature)}\n";
        File.WriteAllText(Path.Combine(contentDir, FileName), manifest);
        return manifest;
    }

    /// <summary>
    /// Vérifie les fichiers téléchargés d'une mise à jour avec les clés du projet et celles de
    /// <see cref="ExtraKeysFileName"/> du dossier de l'agent. Renvoie null si tout colle, sinon le motif du refus.
    /// </summary>
    public static string? CheckPackage(string manifestPath, string installDir, string currentVersion,
        params (string Name, string Path)[] files)
    {
        if (IsBypassed(installDir))
        {
            return null;
        }
        if (!File.Exists(manifestPath))
        {
            return "manifeste absent";
        }

        var extraKeysPath = Path.Combine(installDir, ExtraKeysFileName);
        var extraKeys = File.Exists(extraKeysPath)
            ? File.ReadAllLines(extraKeysPath).Select(x => x.Trim()).Where(x => x.Length > 0)
            : [];

        return Check(File.ReadAllText(manifestPath), ProjectKeys.Concat(extraKeys), currentVersion, files);
    }

    public static string? Check(string manifest, IEnumerable<string> trustedKeys, string currentVersion,
        params (string Name, string Path)[] files)
    {
        const string signatureTag = "\nsignature ";
        var signatureIndex = manifest.LastIndexOf(signatureTag, StringComparison.Ordinal);
        if (!manifest.StartsWith("amarre-manifest 1\n", StringComparison.Ordinal) || signatureIndex < 0)
        {
            return "manifeste illisible";
        }

        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(manifest[(signatureIndex + signatureTag.Length)..].Trim());
        }
        catch (FormatException)
        {
            return "signature illisible";
        }

        var body = Encoding.UTF8.GetBytes(manifest[..(signatureIndex + 1)]);
        if (!trustedKeys.Any(key => IsSignedBy(key, body, signature)))
        {
            return "signature invalide ou clé inconnue";
        }

        // Signé : le contenu vient d'une clé de confiance, on peut le lire sans méfiance.
        var lines = manifest[..signatureIndex].Split('\n');
        var version = lines.FirstOrDefault(x => x.StartsWith("version ", StringComparison.Ordinal))?[8..].Trim();
        if (!Version.TryParse(version, out var manifestVersion) || !Version.TryParse(currentVersion, out var agentVersion))
        {
            return "version illisible";
        }
        // Anti-retour arrière : un ancien paquet signé (faille connue) ne remplace pas un agent plus récent.
        if (Normalize(manifestVersion) < Normalize(agentVersion))
        {
            return $"version {version} plus ancienne que l'agent ({currentVersion})";
        }

        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var parts in lines.Select(x => x.Split(' ', 3)).Where(x => x.Length == 3 && x[0] == "sha256"))
        {
            hashes[parts[2]] = parts[1];
        }

        foreach (var (name, path) in files)
        {
            if (!hashes.TryGetValue(name, out var expected))
            {
                return $"{name} absent du manifeste";
            }
            using var stream = File.OpenRead(path);
            if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                return $"empreinte de {name} différente du manifeste";
            }
        }
        return null;
    }

    /// <summary>
    /// Remplit <c>HostName=</c> et <c>Organization=</c> d'un script d'installation bash, comme le fait le serveur
    /// au téléchargement. L'agent le fait lui-même après vérification : le script brut est celui du manifeste.
    /// </summary>
    public static string FillBashInstaller(string script, string host, string organizationId)
    {
        // Valeurs recopiées entre guillemets dans un script lancé en root.
        if (!Uri.TryCreate(host, UriKind.Absolute, out _) || !Guid.TryParse(organizationId, out _) ||
            host.IndexOfAny(['"', '$', '`', '\\', '\n', '\r']) >= 0)
        {
            throw new ArgumentException("URL du serveur ou organisation invalide.");
        }

        var lines = script.Split('\n').Select(x => x.TrimEnd('\r')).ToArray();
        var hostIndex = Array.IndexOf(lines, "HostName=");
        var orgIndex = Array.IndexOf(lines, "Organization=");
        if (hostIndex < 0 || orgIndex < 0)
        {
            throw new FormatException("Script d'installation sans ligne HostName= ou Organization=.");
        }
        lines[hostIndex] = $"HostName=\"{host}\"";
        lines[orgIndex] = $"Organization=\"{organizationId}\"";
        return string.Join("\n", lines);
    }

    private static bool IsSignedBy(string publicKey, byte[] body, byte[] signature)
    {
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
            return ecdsa.VerifyData(body, signature, HashAlgorithmName.SHA256);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return false;
        }
    }

    // 1.28 et 1.28.0.0 sont la même version (Version les ordonne sinon).
    private static Version Normalize(Version v) =>
        new(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));
}
