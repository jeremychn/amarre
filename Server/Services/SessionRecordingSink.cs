using Remotely.Server.Models;

namespace Remotely.Server.Services;


/// <summary>
/// The service is responsible for storing session recordings.
/// </summary>
public interface ISessionRecordingSink
{
    /// <summary>
    /// Sink a live webm stream to persistent storage. 
    /// </summary>
    /// <param name="webmStream"></param>
    /// <param name="hubCallerContext"></param>
    /// <param name="session"></param>
    /// <returns></returns>
    Task SinkWebmStream(
        IAsyncEnumerable<byte[]> webmStream,
        RemoteControlSession session);
}


public class SessionRecordingSink : ISessionRecordingSink
{
    private readonly ILogger<SessionRecordingSink> _logger;

    public SessionRecordingSink(ILogger<SessionRecordingSink> logger)
    {
        _logger = logger;
    }

    public static string RecordingsDirectory
    {
        get
        {
            return Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "AppData",
                "recordings");
        }
    }

    // Plafonds d'un enregistrement : la session de prise de main dure au plus 8 h.
    public const long MaxRecordingBytes = 4L * 1024 * 1024 * 1024;
    public static readonly TimeSpan MaxRecordingDuration = TimeSpan.FromHours(8);

    public async Task SinkWebmStream(IAsyncEnumerable<byte[]> webmStream, RemoteControlSession session)
    {
        try
        {
            var targetDir = Path.Combine(RecordingsDirectory, $"{DateTime.Now:yyyy-MM-dd}");
            _ = Directory.CreateDirectory(targetDir);

            var fileName = $"{GetSafeFileNamePart(session.RequesterName)}_{DateTime.Now:yyyyMMdd_HHmmssfff}_{Guid.NewGuid():N}.webm";

            using var fs = new FileStream(Path.Combine(targetDir, fileName), FileMode.CreateNew);
            using var cts = new CancellationTokenSource(MaxRecordingDuration);

            await foreach (var chunk in webmStream.WithCancellation(cts.Token))
            {
                if (fs.Length + chunk.Length > MaxRecordingBytes)
                {
                    _logger.LogWarning("Enregistrement de session arrêté : taille maximale atteinte ({fileName}).", fileName);
                    break;
                }
                await fs.WriteAsync(chunk, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Enregistrement de session arrêté : durée maximale atteinte.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sinking webm stream.");
        }
    }

    /// <summary>
    /// Nom du demandeur réduit à des caractères sûrs pour un nom de fichier (le nom vient du client).
    /// </summary>
    public static string GetSafeFileNamePart(string? requesterName)
    {
        var safe = new string((requesterName ?? string.Empty)
            .Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or ' ')
            .Take(50)
            .ToArray())
            .Trim()
            .Replace(' ', '_');

        return string.IsNullOrEmpty(safe) ? "AnonymousUser" : safe;
    }
}
