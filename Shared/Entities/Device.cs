using Remotely.Shared.Attributes;
using Remotely.Shared.Models;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace Remotely.Shared.Entities;

public class Device
{

    [Sortable]
    [Display(Name = "Version de l'agent")]
    public string? AgentVersion { get; set; }

    public ICollection<Alert>? Alerts { get; set; }

    [StringLength(100)]
    [Sortable]
    [Display(Name = "Alias")]
    public string? Alias { get; set; }

    [Sortable]
    [Display(Name = "Utilisation CPU")]
    public double CpuUtilization { get; set; }

    [Sortable]
    [Display(Name = "Utilisateur")]
    public string? CurrentUser { get; set; }

    public DeviceGroup? DeviceGroup { get; set; }
    public string? DeviceGroupID { get; set; }

    [Sortable]
    [Display(Name = "Nom")]
    public string? DeviceName { get; set; }

    /// <summary>UUID matériel de la connexion en cours : en mémoire seulement, pas en base.</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    [JsonIgnore]
    public string? HardwareId { get; set; }
    public List<Drive>? Drives { get; set; }

    [Key]
    public string ID { get; set; } = Guid.NewGuid().ToString();

    public bool Is64Bit { get; set; }
    public bool IsOnline { get; set; }

    [Sortable]
    [Display(Name = "Dernière connexion")]
    public DateTimeOffset LastOnline { get; set; }

    [Display(Name = "Adresses MAC")]
    public string[] MacAddresses { get; set; } = Array.Empty<string>();

    [StringLength(5000)]
    public string? Notes { get; set; }

    [JsonIgnore]
    public Organization? Organization { get; set; }

    public string OrganizationID { get; set; } = null!;
    public Architecture OSArchitecture { get; set; }

    [Sortable]
    [Display(Name = "Système d'exploitation")]
    public string? OSDescription { get; set; }

    [Sortable]
    [Display(Name = "Système")]
    public string? Platform { get; set; }

    [Sortable]
    [Display(Name = "Nombre de processeurs")]
    public int ProcessorCount { get; set; }

    public string? PublicIP { get; set; }

    [JsonIgnore]
    public List<ScriptResult> ScriptResults { get; set; } = new();

    [JsonIgnore]
    public List<ScriptRun> ScriptRuns { get; set; } = new();

    [JsonIgnore]
    public List<ScriptSchedule> ScriptSchedules { get; set; } = new();

    public string? ServerVerificationToken { get; set; }

    [StringLength(200)]
    [Sortable]
    [Display(Name = "Tags")]
    public string? Tags { get; set; }

    [Sortable]
    [Display(Name = "Mémoire totale")]
    public double TotalMemory { get; set; }

    [Sortable]
    [Display(Name = "Stockage total")]
    public double TotalStorage { get; set; }

    [Sortable]
    [Display(Name = "Mémoire utilisée")]
    public double UsedMemory { get; set; }

    [Sortable]
    [Display(Name = "Mémoire utilisée %")]
    public double UsedMemoryPercent
    {
        get
        {
            if (TotalMemory == 0)
            {
                return 0;
            }
            return UsedMemory / TotalMemory;
        }
    }

    [Sortable]
    [Display(Name = "Stockage utilisé")]
    public double UsedStorage { get; set; }

    [Sortable]
    [Display(Name = "Stockage utilisé %")]
    public double UsedStoragePercent
    {
        get
        {
            if (TotalStorage == 0)
            {
                return 0;
            }
            return UsedStorage / TotalStorage;
        }
    }
}