using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.Services;
using System;

namespace Remotely.Server.Tests;

[TestClass]
public class AgentVersionTests
{
    [TestMethod]
    [DataRow("1.15", "1.15", false)]
    [DataRow("1.15.0.0", "1.15", false)]
    [DataRow("1.14", "1.15", true)]
    [DataRow("2026.10.15.1721", "1.15", true)]
    [DataRow("1.16", "1.15", true)]
    [DataRow("", "1.15", false)]
    [DataRow(null, "1.15", false)]
    [DataRow("1.14", null, false)]
    public void IsOutdated(string? agentVersion, string? currentAgentVersion, bool expected)
    {
        var current = currentAgentVersion is null ? null : Version.Parse(currentAgentVersion);
        Assert.AreEqual(expected, AgentVersion.IsOutdated(agentVersion, current));
    }
}
