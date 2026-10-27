using Remotely.Server.Models;
using Remotely.Shared.Entities;
using System.Text.Json;

namespace Remotely.Server.Services;

public interface IBackupService
{
    string CreateBackupJson(string userName, Device[] devices, DeviceGroup[] deviceGroups);
    BackupData? ReadBackup(string json);
    Task<int> RestoreBackup(BackupData backup, Device[] userDevices, DeviceGroup[] userGroups);
}

public class BackupService : IBackupService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IDataService _dataService;
    private readonly ILogger<BackupService> _logger;

    public BackupService(IDataService dataService, ILogger<BackupService> logger)
    {
        _dataService = dataService;
        _logger = logger;
    }

    public string CreateBackupJson(string userName, Device[] devices, DeviceGroup[] deviceGroups)
    {
        var backup = new BackupData
        {
            UserName = userName,
            Devices = devices.Select(d => new DeviceBackupDto
            {
                ID = d.ID,
                DeviceName = d.DeviceName,
                Alias = d.Alias,
                Tags = d.Tags,
                Notes = d.Notes,
                DeviceGroupID = d.DeviceGroupID,
                Platform = d.Platform,
                OSDescription = d.OSDescription,
                AgentVersion = d.AgentVersion,
                LastOnline = d.LastOnline
            }).ToList(),
            DeviceGroups = deviceGroups.Select(g => new DeviceGroupBackupDto
            {
                ID = g.ID,
                Name = g.Name
            }).ToList()
        };

        return JsonSerializer.Serialize(backup, _jsonOptions);
    }

    public BackupData? ReadBackup(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<BackupData>(json, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Fichier de sauvegarde illisible.");
            return null;
        }
    }

    // Ne touche qu'aux appareils visibles par l'utilisateur, et n'accepte que ses propres groupes.
    public async Task<int> RestoreBackup(BackupData backup, Device[] userDevices, DeviceGroup[] userGroups)
    {
        var devicesById = userDevices.ToDictionary(d => d.ID, StringComparer.OrdinalIgnoreCase);
        var groupIds = new HashSet<string>(userGroups.Select(g => g.ID), StringComparer.OrdinalIgnoreCase);
        var restored = 0;

        foreach (var dto in backup.Devices)
        {
            if (string.IsNullOrEmpty(dto.ID) || !devicesById.TryGetValue(dto.ID, out var device))
            {
                continue;
            }

            var groupId = string.IsNullOrEmpty(dto.DeviceGroupID) || groupIds.Contains(dto.DeviceGroupID)
                ? dto.DeviceGroupID
                : device.DeviceGroupID;

            await _dataService.UpdateDevice(device.ID, dto.Tags, dto.Alias, groupId, dto.Notes);
            restored++;
        }

        _logger.LogInformation("Sauvegarde restaurée : {count} appareils.", restored);
        return restored;
    }
}
