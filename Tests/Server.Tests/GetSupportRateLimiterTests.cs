using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.RateLimiting;
using System.Linq;

namespace Remotely.Server.Tests;

[TestClass]
public class GetSupportRateLimiterTests
{
    [TestMethod]
    public void TryAcquireForIp_BlocksAfterLimit_PerIp()
    {
        using var limiter = new GetSupportRateLimiter();

        var results = Enumerable.Range(0, GetSupportRateLimiter.PermitsPerIp + 1)
            .Select(_ => limiter.TryAcquireForIp("203.0.113.5"))
            .ToArray();

        Assert.IsTrue(results.Take(GetSupportRateLimiter.PermitsPerIp).All(x => x));
        Assert.IsFalse(results[^1]);
        // Une autre adresse a sa propre limite.
        Assert.IsTrue(limiter.TryAcquireForIp("203.0.113.6"));
    }

    [TestMethod]
    public void TryAcquireForIp_UnknownAddress_NotLimited()
    {
        using var limiter = new GetSupportRateLimiter();

        for (var i = 0; i < GetSupportRateLimiter.PermitsPerIp * 2; i++)
        {
            Assert.IsTrue(limiter.TryAcquireForIp(i % 2 == 0 ? null : ""));
        }
    }

    [TestMethod]
    public void TryAcquireForDevice_BlocksAfterLimit_IgnoringCase()
    {
        using var limiter = new GetSupportRateLimiter();
        const string deviceId = "f2b0c8e4-0000-4000-8000-00000000abcd";

        for (var i = 0; i < GetSupportRateLimiter.PermitsPerDevice; i++)
        {
            Assert.IsTrue(limiter.TryAcquireForDevice(i % 2 == 0 ? deviceId : deviceId.ToUpperInvariant()));
        }

        Assert.IsFalse(limiter.TryAcquireForDevice(deviceId));
        Assert.IsTrue(limiter.TryAcquireForDevice("autre-appareil"));
    }
}
