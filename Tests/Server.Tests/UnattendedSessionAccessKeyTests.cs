using Bitbound.SimpleMessenger;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Remotely.Server.Hubs;
using Remotely.Server.Models;
using Remotely.Server.Services;
using Remotely.Server.Services.Stores;
using Remotely.Server.Tests.Mocks;
using Remotely.Shared.Entities;
using Remotely.Shared.Interfaces;
using Remotely.Shared.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Remotely.Server.Tests;

// L4 : une session sans surveillance porte sa clé d'accès dès sa création, et seul le client de
// bureau qui présente cette clé peut s'enregistrer sous son ID.
[TestClass]
public class UnattendedSessionAccessKeyTests
{
    private TestData _testData = null!;
    private IDataService _dataService = null!;

    [TestInitialize]
    public async Task Init()
    {
        _testData = new TestData();
        await _testData.Init();
        _dataService = IoCActivator.ServiceProvider.GetRequiredService<IDataService>();
    }

    [TestCleanup]
    public void Cleanup() => _testData.ClearData();

    [TestMethod]
    [DoNotParallelize]
    public async Task PriseEnMain_CleRangeeDansLaSession_EtRemiseALAgent()
    {
        Device? device = _testData.Org1Device1;
        string? agentConnectionId = "agent-1";
        var agentHub = new HubContextFixture<AgentHub, IAgentHubClient>();
        var agentCache = new Mock<IAgentHubSessionCache>();
        agentCache.Setup(x => x.TryGetByDeviceId(_testData.Org1Device1.ID, out device)).Returns(true);
        agentCache.Setup(x => x.TryGetConnectionId(_testData.Org1Device1.ID, out agentConnectionId)).Returns(true);
        var sessionCache = new Mock<IRemoteControlSessionCache>();

        var circuit = new CircuitConnection(
            new Mock<IAuthService>().Object,
            _dataService,
            new Mock<ISelectedCardsStore>().Object,
            agentHub.HubContextMock.Object,
            new Mock<ICircuitManager>().Object,
            new Mock<IToastService>().Object,
            new Mock<IExpiringTokenService>().Object,
            sessionCache.Object,
            agentCache.Object,
            new Mock<IMessenger>().Object,
            new Mock<ILogger<CircuitConnection>>().Object)
        {
            User = _testData.Org1Admin1,
        };

        var result = await circuit.RemoteControl(_testData.Org1Device1.ID, false);

        Assert.IsTrue(result.IsSuccess);
        var session = result.Value;
        Assert.IsFalse(string.IsNullOrEmpty(session.AccessKey));
        sessionCache.Verify(x => x.AddOrUpdate($"{session.UnattendedSessionId}", session), Times.Once);
        agentHub.SingleClientProxyMock.Verify(x => x.RemoteControl(
            session.UnattendedSessionId,
            session.AccessKey,
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    [DataRow("mauvaise-cle")]
    [DataRow("")]
    public async Task ClientDeBureau_MauvaiseCle_Refuse(string accessKey)
    {
        var (hub, session, _) = CreateDesktopHub("bonne-cle");

        var result = await hub.ReceiveUnattendedSessionInfo(session.UnattendedSessionId, accessKey, "poste-pirate", "", "");

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(string.Empty, session.DesktopConnectionId);
        Assert.AreEqual("bonne-cle", session.AccessKey);
    }

    [TestMethod]
    public async Task ClientDeBureau_BonneCle_Enregistre()
    {
        var (hub, session, context) = CreateDesktopHub("bonne-cle");

        var result = await hub.ReceiveUnattendedSessionInfo(session.UnattendedSessionId, "bonne-cle", "poste", "", "");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(context.Object.ConnectionId, session.DesktopConnectionId);
    }

    // Session inconnue du serveur (après un redémarrage, ou ID inventé) : refusée, rien n'est créé.
    [TestMethod]
    public async Task SessionInconnue_Refusee()
    {
        var (hub, session, _) = CreateDesktopHub("bonne-cle");
        var unknownId = Guid.NewGuid();

        var result = await hub.ReceiveUnattendedSessionInfo(unknownId, "cle-du-client", "poste", "", "");

        Assert.IsFalse(result.IsSuccess);
        Assert.IsFalse(_lastSessionCache!.TryGetValue($"{unknownId}", out _));
        Assert.AreEqual(string.Empty, session.DesktopConnectionId);
    }

    // Client relancé (changement de session Windows, redémarrage du client) : même ID, même clé, accepté.
    [TestMethod]
    public async Task ClientRelance_MemeCle_Accepte()
    {
        var (hub, session, _) = CreateDesktopHub("bonne-cle");
        Assert.IsTrue((await hub.ReceiveUnattendedSessionInfo(session.UnattendedSessionId, "bonne-cle", "poste", "", "")).IsSuccess);

        var relaunched = CreateDesktopHub(_lastSessionCache!, "bureau-2");
        var result = await relaunched.ReceiveUnattendedSessionInfo(session.UnattendedSessionId, "bonne-cle", "poste", "", "");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("bureau-2", session.DesktopConnectionId);
    }

    // Session sans clé (ne devrait plus exister) : plus personne ne peut s'en emparer.
    [TestMethod]
    public async Task SessionSansCle_Refuse()
    {
        var (hub, session, _) = CreateDesktopHub(string.Empty);

        var result = await hub.ReceiveUnattendedSessionInfo(session.UnattendedSessionId, "n-importe-quoi", "poste", "", "");

        Assert.IsFalse(result.IsSuccess);
    }

    private (DesktopHub Hub, RemoteControlSession Session, Mock<HubCallerContext> Context) CreateDesktopHub(string accessKey)
    {
        var session = new RemoteControlSession
        {
            UnattendedSessionId = Guid.NewGuid(),
            AccessKey = accessKey,
        };

        var sessionCache = new RemoteControlSessionCache(
            new Mock<ISystemTime>().Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RemoteControlSessionCache>.Instance);
        sessionCache.AddOrUpdate($"{session.UnattendedSessionId}", session);
        _lastSessionCache = sessionCache;

        var hub = CreateDesktopHub(sessionCache, "bureau-1", out var context);
        return (hub, session, context);
    }

    private RemoteControlSessionCache? _lastSessionCache;

    private static DesktopHub CreateDesktopHub(RemoteControlSessionCache sessionCache, string connectionId) =>
        CreateDesktopHub(sessionCache, connectionId, out _);

    private static DesktopHub CreateDesktopHub(RemoteControlSessionCache sessionCache, string connectionId, out Mock<HubCallerContext> context)
    {
        context = new Mock<HubCallerContext>();
        context.Setup(x => x.ConnectionId).Returns(connectionId);
        context.Setup(x => x.Items).Returns(new Dictionary<object, object?>());

        var hub = new DesktopHub(
            sessionCache,
            new Mock<IDesktopStreamCache>().Object,
            new Mock<IAgentHubSessionCache>().Object,
            new Mock<IHubContext<AgentHub, IAgentHubClient>>().Object,
            new Mock<IHubContext<ViewerHub, IViewerHubClient>>().Object,
            new Mock<ILogger<DesktopHub>>().Object)
        {
            Context = context.Object,
        };
        return hub;
    }
}
