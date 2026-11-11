using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.Services;

namespace Remotely.Server.Tests;

[TestClass]
public class SessionRecordingSinkTests
{
    [TestMethod]
    [DataRow("Jean Dupont", "Jean_Dupont")]
    [DataRow("../../../etc/x", "etcx")]
    [DataRow("C:\\Windows\\x", "CWindowsx")]
    [DataRow("/tmp/x", "tmpx")]
    [DataRow("..", "AnonymousUser")]
    [DataRow("", "AnonymousUser")]
    [DataRow(null, "AnonymousUser")]
    public void GetSafeFileNamePart(string? requesterName, string expected) =>
        Assert.AreEqual(expected, SessionRecordingSink.GetSafeFileNamePart(requesterName));

    [TestMethod]
    public void GetSafeFileNamePart_Tronque() =>
        Assert.AreEqual(50, SessionRecordingSink.GetSafeFileNamePart(new string('a', 300)).Length);
}
