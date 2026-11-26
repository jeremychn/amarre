using System.Runtime.Serialization;

namespace Remotely.Shared.Dtos;

/// <summary>Élément d'un dossier du poste (explorateur de fichiers de la console).</summary>
[DataContract]
public class DeviceFileEntryDto
{
    [DataMember]
    public string Name { get; set; } = string.Empty;

    [DataMember]
    public string FullPath { get; set; } = string.Empty;

    [DataMember]
    public bool IsDirectory { get; set; }

    [DataMember]
    public long Size { get; set; }

    [DataMember]
    public DateTimeOffset? LastModified { get; set; }
}

/// <summary>Contenu d'un dossier du poste ; chemin vide = racine (lecteurs sous Windows).</summary>
[DataContract]
public class DeviceFileListDto
{
    /// <summary>Au-delà, la liste est tronquée (taille des messages SignalR).</summary>
    public const int MaxEntries = 1000;

    [DataMember]
    public string Path { get; set; } = string.Empty;

    /// <summary>Dossier parent ; vide à la racine.</summary>
    [DataMember]
    public string ParentPath { get; set; } = string.Empty;

    [DataMember]
    public List<DeviceFileEntryDto> Entries { get; set; } = [];

    [DataMember]
    public bool Truncated { get; set; }

    /// <summary>Message d'erreur à afficher ; vide si la lecture a réussi.</summary>
    [DataMember]
    public string Error { get; set; } = string.Empty;
}
