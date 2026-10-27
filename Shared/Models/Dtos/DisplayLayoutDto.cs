using System.Runtime.Serialization;

namespace Remotely.Shared.Models.Dtos;

[DataContract]
public class DisplayLayoutDto
{
    [DataMember(Name = "DisplayName")]
    public string DisplayName { get; init; } = string.Empty;

    [DataMember(Name = "X")]
    public int X { get; init; }

    [DataMember(Name = "Y")]
    public int Y { get; init; }

    [DataMember(Name = "Width")]
    public int Width { get; init; }

    [DataMember(Name = "Height")]
    public int Height { get; init; }

    [DataMember(Name = "IsPrimary")]
    public bool IsPrimary { get; init; }
}
