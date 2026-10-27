namespace Remotely.Server.Models;

public class BackupData
{
    public string BackupVersion { get; set; } = "1.0";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public string? UserName { get; set; }
    public List<DeviceBackupDto> Devices { get; set; } = new();
    public List<DeviceGroupBackupDto> DeviceGroups { get; set; } = new();
}

public class DeviceBackupDto
{
    public string? ID { get; set; }
    public string? DeviceName { get; set; }
    public string? Alias { get; set; }
    public string? Tags { get; set; }
    public string? Notes { get; set; }
    public string? DeviceGroupID { get; set; }
    public string? Platform { get; set; }
    public string? OSDescription { get; set; }
    public string? AgentVersion { get; set; }
    public DateTimeOffset LastOnline { get; set; }
}

public class DeviceGroupBackupDto
{
    public string? ID { get; set; }
    public string? Name { get; set; }
}
