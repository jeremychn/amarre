using System.Threading.RateLimiting;

namespace Remotely.Server.RateLimiting;

/// <summary>
/// Limite de la page anonyme « Demander de l'aide » : chaque demande crée une alerte et un
/// e-mail par utilisateur de l'organisation. Fenêtres fixes par adresse IP et par appareil,
/// avec les limiteurs de System.Threading.RateLimiting (ceux du middleware du serveur).
/// </summary>
public interface IGetSupportRateLimiter
{
    /// <summary>
    /// À appeler avant toute recherche de l'appareil (sonde d'ID comprise). Adresse inconnue :
    /// accepté (une limite commune bloquerait tous les visiteurs), la limite par appareil reste.
    /// </summary>
    bool TryAcquireForIp(string? ipAddress);

    /// <summary>À appeler avec l'ID enregistré de l'appareil, pas celui de l'URL.</summary>
    bool TryAcquireForDevice(string deviceId);
}

public sealed class GetSupportRateLimiter : IGetSupportRateLimiter, IDisposable
{
    public const int PermitsPerIp = 5;
    public const int PermitsPerDevice = 3;
    public static readonly TimeSpan IpWindow = TimeSpan.FromHours(1);
    public static readonly TimeSpan DeviceWindow = TimeSpan.FromMinutes(15);

    private readonly PartitionedRateLimiter<string> _byIp = Create(PermitsPerIp, IpWindow);
    private readonly PartitionedRateLimiter<string> _byDevice = Create(PermitsPerDevice, DeviceWindow);

    public bool TryAcquireForIp(string? ipAddress) =>
        string.IsNullOrWhiteSpace(ipAddress) || TryAcquire(_byIp, ipAddress);

    public bool TryAcquireForDevice(string deviceId) =>
        TryAcquire(_byDevice, deviceId.ToUpperInvariant());

    public void Dispose()
    {
        _byIp.Dispose();
        _byDevice.Dispose();
    }

    private static bool TryAcquire(PartitionedRateLimiter<string> limiter, string key)
    {
        // Fenêtre fixe : libérer le bail ne rend pas le jeton.
        using var lease = limiter.AttemptAcquire(key);
        return lease.IsAcquired;
    }

    private static PartitionedRateLimiter<string> Create(int permits, TimeSpan window) =>
        PartitionedRateLimiter.Create<string, string>(key =>
            RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permits,
                Window = window,
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
}
