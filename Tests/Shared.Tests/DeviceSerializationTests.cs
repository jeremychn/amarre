using System.Text.Json;
using Remotely.Shared.Entities;
using Remotely.Shared.Extensions;

namespace Remotely.Shared.Tests;

[TestClass]
public class DeviceSerializationTests
{
    [TestMethod]
    public void Json_SansJetonDuPoste()
    {
        var device = new Device { ID = "poste", ServerVerificationToken = "jeton-secret" };

        var json = JsonSerializer.Serialize(device);

        Assert.IsFalse(json.Contains("jeton-secret"));
        Assert.IsFalse(json.Contains(nameof(Device.ServerVerificationToken)));
    }

    [TestMethod]
    public void ToDto_SansJetonDuPoste() =>
        Assert.AreEqual(string.Empty, new Device { ID = "poste", ServerVerificationToken = "jeton-secret" }.ToDto().ServerVerificationToken);
}
