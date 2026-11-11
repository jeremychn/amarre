using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Remotely.Shared.Entities;

public class DeviceGroup
{
    /// <summary>
    /// Nom donné au groupe par défaut à sa création. L'administrateur peut le renommer :
    /// le groupe par défaut se reconnaît à <see cref="IsDefault"/>, pas à son nom.
    /// </summary>
    public const string DefaultGroupName = "Tous";

    [StringLength(200)]
    public required string Name { get; set; }

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string ID { get; set; } = null!;

    [JsonIgnore]
    public List<Device> Devices { get; set; } = new();

    /// <summary>
    /// Groupe par défaut de l'organisation (un seul par organisation) : les postes enregistrés
    /// sans groupe et les utilisateurs non administrateurs y sont ajoutés. Il ne peut pas être supprimé.
    /// </summary>
    public bool IsDefault { get; set; }

    [JsonIgnore]
    public Organization? Organization { get; set; }

    public string OrganizationID { get; set; } = null!;

    [JsonIgnore]
    public List<RemotelyUser> Users { get; set; } = new();

    [JsonIgnore]
    public List<ScriptSchedule>? ScriptSchedules { get; set; }
}
