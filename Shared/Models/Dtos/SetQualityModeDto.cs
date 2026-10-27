using System.Runtime.Serialization;
using Remotely.Shared.Enums;

namespace Remotely.Shared.Models.Dtos;

[DataContract]
public class SetQualityModeDto
{
    [DataMember]
    public RemoteControlQualityMode QualityMode { get; set; }
}
