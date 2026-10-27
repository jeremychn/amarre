#nullable enable
namespace Remotely.Shared.Models;

public class ConnectionInfo
{
    private string? _host;

    public string DeviceID { get; set; } = Guid.NewGuid().ToString();
    public string? Host
    {
        get
        {
            return _host?.Trim()?.TrimEnd('/');
        }
        set
        {
            _host = value?.Trim()?.TrimEnd('/');
        }
    }
    public string? OrganizationID { get; set; }
    /// <summary>UUID matériel du poste qui a reçu ce DeviceID (voir HardwareId).</summary>
    public string? HardwareId { get; set; }
    public string? ServerVerificationToken { get; set; }

}
