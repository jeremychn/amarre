namespace Remotely.Shared.Enums;

public enum RemoteControlQualityMode
{
    /// <summary>
    /// Automatically adapts quality based on network conditions (latency and frame delivery).
    /// This is the default mode.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Uses lower image quality to reduce bandwidth usage and improve performance
    /// on slow or congested networks.
    /// </summary>
    Performance = 1,

    /// <summary>
    /// Uses the highest image quality at the cost of increased bandwidth.
    /// Best for high-speed local networks where quality is the priority.
    /// </summary>
    Quality = 2
}
