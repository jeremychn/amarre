using Remotely.Server.Hubs;
using Remotely.Server.Services;
using Bitbound.SimpleMessenger;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Remotely.Shared.Extensions;
using Remotely.Shared.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Remotely.Server.Tests;

[TestClass]
public class AgentHubTests
{
    private TestData _testData = null!;
    private IDataService _dataService = null!;

    [TestMethod]
    [DoNotParallelize]
    public async Task DeviceCameOnline_BannedByName()
    {
        var circuitManager = new Mock<ICircuitManager>();
        var circuitConnection = new Mock<ICircuitConnection>();
        circuitManager.Setup(x => x.Connections).Returns(new[] { circuitConnection.Object });
        circuitConnection.Setup(x => x.User).Returns(_testData.Org1Admin1);
        var viewerHub = new Mock<IHubContext<ViewerHub>>();
        var expiringTokenService = new Mock<IExpiringTokenService>();
        var serviceSessionCache = new Mock<IAgentHubSessionCache>();
        var remoteControlSessions = new Mock<IRemoteControlSessionCache>();
        var messenger = new Mock<IMessenger>();
        var logger = new Mock<ILogger<AgentHub>>();

        var settings = await _dataService.GetSettings();
        settings.BannedDevices = [_testData.Org1Device1.DeviceName!];
        await _dataService.SaveSettings(settings);

        var hub = new AgentHub(
            _dataService,
            serviceSessionCache.Object,
            viewerHub.Object,
            circuitManager.Object,
            expiringTokenService.Object,
            remoteControlSessions.Object,
            messenger.Object,
            logger.Object);

        var hubClients = new Mock<IHubCallerClients<IAgentHubClient>>();
        var caller = new Mock<IAgentHubClient>();
        hubClients.Setup(x => x.Caller).Returns(caller.Object);
        hub.Clients = hubClients.Object;

        var result = await hub.DeviceCameOnline(_testData.Org1Device1.ToDto());
        Assert.IsFalse(result);
        hubClients.Verify(x => x.Caller, Times.Once);
        caller.Verify(x => x.UninstallAgent(), Times.Once);
    }

    // TODO: Checking of device ban should be pulled out into
    // a separate service that's better testable.
    [TestMethod]
    [DoNotParallelize]
    public async Task DeviceCameOnline_BannedById()
    {
        var circuitManager = new Mock<ICircuitManager>();
        var circuitConnection = new Mock<ICircuitConnection>();
        circuitManager.Setup(x => x.Connections).Returns(new[] { circuitConnection.Object });
        circuitConnection.Setup(x => x.User).Returns(_testData.Org1Admin1);
        var viewerHub = new Mock<IHubContext<ViewerHub>>();
        var expiringTokenService = new Mock<IExpiringTokenService>();
        var serviceSessionCache = new Mock<IAgentHubSessionCache>();
        var remoteControlSessions = new Mock<IRemoteControlSessionCache>();
        var messenger = new Mock<IMessenger>();
        var logger = new Mock<ILogger<AgentHub>>();


        var settings = await _dataService.GetSettings();
        settings.BannedDevices = [$"{_testData.Org1Device1.ID}"];
        await _dataService.SaveSettings(settings);

        var hub = new AgentHub(
            _dataService,
            serviceSessionCache.Object,
            viewerHub.Object,
            circuitManager.Object,
            expiringTokenService.Object,
            remoteControlSessions.Object,
            messenger.Object,
            logger.Object);

        var hubClients = new Mock<IHubCallerClients<IAgentHubClient>>();
        var caller = new Mock<IAgentHubClient>();
        hubClients.Setup(x => x.Caller).Returns(caller.Object);
        hub.Clients = hubClients.Object;

        var result = await hub.DeviceCameOnline(_testData.Org1Device1.ToDto());
        Assert.IsFalse(result);
        hubClients.Verify(x => x.Caller, Times.Once);
        caller.Verify(x => x.UninstallAgent(), Times.Once);
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task DeviceCameOnline_DeviceIdEnDouble_NouvelIdDemande()
    {
        var circuitManager = new Mock<ICircuitManager>();
        var sessionCache = new AgentHubSessionCache();
        var connected = _testData.Org1Device1;
        connected.HardwareId = "11111111-2222-3333-4444-555555555555";
        sessionCache.AddOrUpdateByConnectionId("connexion-poste-a", connected);

        var hub = new AgentHub(
            _dataService,
            sessionCache,
            new Mock<IHubContext<ViewerHub>>().Object,
            circuitManager.Object,
            new Mock<IExpiringTokenService>().Object,
            new Mock<IRemoteControlSessionCache>().Object,
            new Mock<IMessenger>().Object,
            new Mock<ILogger<AgentHub>>().Object);

        var context = new Mock<HubCallerContext>();
        context.Setup(x => x.ConnectionId).Returns("connexion-clone");
        context.Setup(x => x.Features).Returns(new FeatureCollection());
        context.Setup(x => x.Items).Returns(new Dictionary<object, object?>());
        hub.Context = context.Object;
        var hubClients = new Mock<IHubCallerClients<IAgentHubClient>>();
        var caller = new Mock<IAgentHubClient>();
        hubClients.Setup(x => x.Caller).Returns(caller.Object);
        hub.Clients = hubClients.Object;

        var clone = connected.ToDto();
        clone.DeviceName = connected.DeviceName!; // même nom : seul l'UUID distingue
        clone.HardwareId = "4c4c4544-0035-3010-8052-b4c04f4a4d32";

        Assert.IsFalse(await hub.DeviceCameOnline(clone));
        caller.Verify(x => x.DeviceIdConflict(), Times.Once);
        Assert.IsTrue(sessionCache.TryGetConnectionId(connected.ID, out var connectionId));
        Assert.AreEqual("connexion-poste-a", connectionId);
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task DeviceCameOnline_DeviceIdEnDouble_AncienAgentAccepteCommeAvant()
    {
        var sessionCache = new AgentHubSessionCache();
        var connected = _testData.Org1Device1;
        sessionCache.AddOrUpdateByConnectionId("connexion-poste-a", connected);

        var logger = new Mock<ILogger<AgentHub>>();
        var hub = new AgentHub(
            _dataService,
            sessionCache,
            new Mock<IHubContext<ViewerHub>>().Object,
            Mock.Of<ICircuitManager>(x => x.Connections == Array.Empty<ICircuitConnection>()),
            new Mock<IExpiringTokenService>().Object,
            new Mock<IRemoteControlSessionCache>().Object,
            new Mock<IMessenger>().Object,
            logger.Object);

        var context = new Mock<HubCallerContext>();
        context.Setup(x => x.ConnectionId).Returns("connexion-ancien-clone");
        context.Setup(x => x.Features).Returns(new FeatureCollection());
        context.Setup(x => x.Items).Returns(new Dictionary<object, object?>());
        hub.Context = context.Object;
        var hubClients = new Mock<IHubCallerClients<IAgentHubClient>>();
        var caller = new Mock<IAgentHubClient>();
        hubClients.Setup(x => x.Caller).Returns(caller.Object);
        hub.Clients = hubClients.Object;

        var oldClone = connected.ToDto();
        oldClone.DeviceName = "POSTE-CLONE"; // pas d'UUID : agent d'avant le correctif
        oldClone.Drives = []; // un vrai agent envoie toujours ses disques

        var result = await hub.DeviceCameOnline(oldClone);
        Assert.IsTrue(result, string.Join(" | ", logger.Invocations.Select(x => string.Join(" ; ", x.Arguments))));
        caller.Verify(x => x.DeviceIdConflict(), Times.Never);
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task DeviceCameOnline_JetonAgent()
    {
        var device = _testData.Org1Device1;
        device.Drives = [];
        await _dataService.AddOrUpdateDevice(device.ToDto());
        _dataService.SetServerVerificationToken(device.ID, "jeton-du-poste");

        Task<bool> Connect(string agentVersion, string token)
        {
            var dto = device.ToDto();
            dto.AgentVersion = agentVersion;
            dto.ServerVerificationToken = token;
            return CreateHub(new AgentHubSessionCache(), out _).DeviceCameOnline(dto);
        }

        // Poste encore en agent daté : un ancien agent sans jeton passe comme avant, un faux jeton jamais.
        await SetAgentVersion(device.ID, "2026.10.15.1721");
        Assert.IsTrue(await Connect("2026.10.15.1721", ""));
        Assert.IsFalse(await Connect("1.16", "faux"));

        // Poste passé en 1.16 : le jeton est exigé.
        await SetAgentVersion(device.ID, "1.16.0.0");
        Assert.IsFalse(await Connect("2026.10.15.1721", ""));
        Assert.IsFalse(await Connect("1.16.0.0", "faux"));
        Assert.IsTrue(await Connect("1.16.0.0", "jeton-du-poste"));
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task DeviceHeartbeat_SansEnregistrement_Ignore()
    {
        var hub = CreateHub(new AgentHubSessionCache(), out _);
        var dto = _testData.Org1Device1.ToDto();
        dto.DeviceName = "IMPOSTEUR";
        dto.Drives = [];

        await hub.DeviceHeartbeat(dto);

        var stored = await _dataService.GetDevice(dto.ID);
        Assert.AreNotEqual("IMPOSTEUR", stored.Value?.DeviceName);
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task SetServerVerificationToken_NeRemplacePasUnJetonPose()
    {
        var device = _testData.Org1Device1;
        device.Drives = [];
        var hub = CreateHub(new AgentHubSessionCache(), out _);
        var dto = device.ToDto();
        dto.ServerVerificationToken = "";
        Assert.IsTrue(await hub.DeviceCameOnline(dto));

        hub.SetServerVerificationToken("premier");
        hub.SetServerVerificationToken("imposteur");

        Assert.AreEqual("premier", (await _dataService.GetDevice(device.ID)).Value?.ServerVerificationToken);
    }

    private async Task SetAgentVersion(string deviceId, string agentVersion)
    {
        var dto = (await _dataService.GetDevice(deviceId)).Value!.ToDto();
        dto.AgentVersion = agentVersion;
        dto.Drives ??= [];
        await _dataService.AddOrUpdateDevice(dto);
    }

    private AgentHub CreateHub(AgentHubSessionCache sessionCache, out Mock<IAgentHubClient> caller)
    {
        var hub = new AgentHub(
            _dataService,
            sessionCache,
            new Mock<IHubContext<ViewerHub>>().Object,
            Mock.Of<ICircuitManager>(x => x.Connections == Array.Empty<ICircuitConnection>()),
            new Mock<IExpiringTokenService>().Object,
            new Mock<IRemoteControlSessionCache>().Object,
            new Mock<IMessenger>().Object,
            new Mock<ILogger<AgentHub>>().Object);

        var context = new Mock<HubCallerContext>();
        context.Setup(x => x.ConnectionId).Returns(Guid.NewGuid().ToString());
        context.Setup(x => x.Features).Returns(new FeatureCollection());
        context.Setup(x => x.Items).Returns(new Dictionary<object, object?>());
        hub.Context = context.Object;
        var hubClients = new Mock<IHubCallerClients<IAgentHubClient>>();
        caller = new Mock<IAgentHubClient>();
        hubClients.Setup(x => x.Caller).Returns(caller.Object);
        hub.Clients = hubClients.Object;
        return hub;
    }

    [TestMethod]
    public void SessionCache_DeconnexionAncienne_GardeLaConnexionRecente()
    {
        var sessionCache = new AgentHubSessionCache();
        var device = _testData.Org1Device1;
        sessionCache.AddOrUpdateByConnectionId("ancienne", device);
        sessionCache.AddOrUpdateByConnectionId("recente", device);

        Assert.IsTrue(sessionCache.TryRemoveByConnectionId("ancienne", out _));

        Assert.IsTrue(sessionCache.TryGetConnectionId(device.ID, out var connectionId));
        Assert.AreEqual("recente", connectionId);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        _testData.ClearData();
    }

    [TestInitialize]
    public async Task TestInit()
    {
        _testData = new TestData();
        await _testData.Init();
        _dataService = IoCActivator.ServiceProvider.GetRequiredService<IDataService>();
    }
}
