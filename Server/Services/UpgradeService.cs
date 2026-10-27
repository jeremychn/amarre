namespace Remotely.Server.Services;

public interface IUpgradeService
{
    Version GetCurrentVersion();
    Version? GetCurrentAgentVersion();
    Task<bool> IsNewVersionAvailable();
}

public class UpgradeService : IUpgradeService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UpgradeService> _logger;
    private readonly IWebHostEnvironment _hostEnv;
    private Version? _currentVersion;

    public UpgradeService(IHttpClientFactory httpClientFactory, ILogger<UpgradeService> logger, IWebHostEnvironment hostEnv)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _hostEnv = hostEnv;
    }

    // Version des agents servis : Content/AgentVersion.txt, écrit par la release avec Content.zip.
    // ponytail: relu à chaque appel (fichier de quelques octets), suit un dépôt d'agents sans redémarrage.
    public Version? GetCurrentAgentVersion()
    {
        try
        {
            var path = Path.Combine(_hostEnv.WebRootPath, "Content", "AgentVersion.txt");
            return File.Exists(path) && Version.TryParse(File.ReadAllText(path).Trim(), out var version) ?
                version :
                null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lecture de la version des agents impossible.");
            return null;
        }
    }

    public Version GetCurrentVersion()
    {
        try
        {
            if (_currentVersion is not null)
            {
                return _currentVersion;
            }

            var asmVersion = typeof(UpgradeService).Assembly.GetName().Version;
            if (asmVersion is not null)
            {
                _currentVersion = asmVersion;
                return _currentVersion;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting current version.");
        }

        _currentVersion = new(1, 0);
        return _currentVersion;
    }
    public async Task<bool> IsNewVersionAvailable()
    {

        try
        {
            using var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync("https://github.com/immense/Remotely/releases/latest");
            var versionTag = $"{response.RequestMessage?.RequestUri}".Split("/").LastOrDefault();
            if (string.IsNullOrWhiteSpace(versionTag))
            {
                return false;
            }
            var versionString = versionTag[1..];
            var remoteVersion = Version.Parse(versionString);

            var filePath = Directory.GetFiles(Directory.GetCurrentDirectory(), "Remotely_Server.dll", SearchOption.AllDirectories).First();
            var fileVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(filePath).FileVersion;
            if (string.IsNullOrWhiteSpace(fileVersion))
            {
                return false;
            }
            var localVersion = Version.Parse(fileVersion);

            if (remoteVersion > localVersion)
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve latest version info.");
        }

        return false;
    }
}

public static class AgentVersion
{
    // Obsolète = différent des agents servis (pas « inférieur » : les anciens agents datés 2026.x sont plus grands que 1.x).
    // Sans AgentVersion.txt, aucun poste n'est signalé.
    public static bool IsOutdated(string? agentVersion, Version? currentAgentVersion) =>
        currentAgentVersion is not null &&
        Version.TryParse(agentVersion, out var version) &&
        Normalize(version) != Normalize(currentAgentVersion);

    private static Version Normalize(Version v) =>
        new(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));
}
