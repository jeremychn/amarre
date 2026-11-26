using Remotely.Shared.Entities;

namespace Remotely.Server.Services;

/// <summary>
/// Durée de fonctionnement d'un poste en ligne, d'après le dernier démarrage envoyé par l'agent.
/// ponytail: seuils fixes (7 et 30 jours) ; réglage par organisation si un client en demande d'autres.
/// </summary>
public static class DeviceUptime
{
    public static readonly TimeSpan Long = TimeSpan.FromDays(7);
    public static readonly TimeSpan VeryLong = TimeSpan.FromDays(30);

    /// <summary>Vide si le poste est hors ligne ou si son agent ne l'envoie pas.</summary>
    public static TimeSpan? Get(Device device, DateTimeOffset now)
    {
        if (!device.IsOnline || device.LastBootTime is not { } boot)
        {
            return null;
        }
        var uptime = now - boot;
        return uptime < TimeSpan.Zero ? TimeSpan.Zero : uptime;
    }

    public static bool IsLong(Device device, DateTimeOffset now) => Get(device, now) >= Long;

    public static string Format(TimeSpan? uptime) => uptime switch
    {
        null => "—",
        { TotalDays: >= 1 } u => $"{(int)u.TotalDays} j",
        { TotalHours: >= 1 } u => $"{(int)u.TotalHours} h",
        { } u => $"{(int)u.TotalMinutes} min",
    };

    public static string CssClass(TimeSpan? uptime) =>
        uptime >= VeryLong ? "text-danger fw-semibold" :
        uptime >= Long ? "text-warning fw-semibold" :
        string.Empty;
}
