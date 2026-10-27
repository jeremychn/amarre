using System.Runtime.Serialization;

namespace Remotely.Shared.Models.Dtos;

[DataContract]
public class ScreenThumbnailDto
{
    [DataMember(Name = "DisplayName")]
    public string DisplayName { get; init; } = string.Empty;

    [DataMember(Name = "ImageBytes")]
    public byte[] ImageBytes { get; init; } = Array.Empty<byte>();

    [DataMember(Name = "Width")]
    public int Width { get; init; }

    [DataMember(Name = "Height")]
    public int Height { get; init; }
}
