using Remotely.Server.Services;
using Bitbound.SimpleMessenger;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Remotely.Server.Models.Messages;
using Remotely.Shared;
using Remotely.Shared.Dtos;
using Remotely.Shared.Entities;
using Remotely.Shared.Enums;
using Remotely.Shared.Helpers;
using Remotely.Shared.Interfaces;
using Remotely.Shared.Models;
using Remotely.Shared.Utilities;

namespace Remotely.Server.Hubs;

public class AgentHub : Hub<IAgentHubClient>
{
    private readonly IDataService _dataService;
    private readonly ICircuitManager _circuitManager;
    private readonly IExpiringTokenService _expiringTokenService;
    private readonly ILogger<AgentHub> _logger;
    private readonly IMessenger _messenger;
    private readonly IRemoteControlSessionCache _remoteControlSessions;
    private readonly IAgentHubSessionCache _serviceSessionCache;
    private readonly IHubContext<ViewerHub> _viewerHubContext;

    public AgentHub(
        IDataService dataService,
        IAgentHubSessionCache serviceSessionCache,
        IHubContext<ViewerHub> viewerHubContext,
        ICircuitManager circuitManager,
        IExpiringTokenService expiringTokenService,
        IRemoteControlSessionCache remoteControlSessionCache,
        IMessenger messenger,
        ILogger<AgentHub> logger)
    {
        _dataService = dataService;
        _serviceSessionCache = serviceSessionCache;
        _viewerHubContext = viewerHubContext;
        _circuitManager = circuitManager;
        _expiringTokenService = expiringTokenService;
        _remoteControlSessions = remoteControlSessionCache;
        _messenger = messenger;
        _logger = logger;
    }

    private const string AgentChallengeKey = "AgentChallenge";

    // TODO: Replace with new invoke capability in .NET 7 in ScriptingController.
    public static IMemoryCache ApiScriptResults { get; } = new MemoryCache(new MemoryCacheOptions());

    // Null tant que DeviceCameOnline n'a pas réussi sur cette connexion (reconnexion en cours, refus) : cas normal, pas de log ici.
    private Device? Device
    {
        get => Context.Items.TryGetValue("Device", out var device) ? device as Device : null;
        set
        {
            Context.Items["Device"] = value;
        }
    }

    public async Task Chat(string messageText, bool disconnected, string browserConnectionId)
    {
        if (Device is null)
        {
            return;
        }

        if (_circuitManager.TryGetConnection(browserConnectionId, out _))
        {
            var message = new ChatReceivedMessage(Device.ID, $"{Device.DeviceName}", messageText, disconnected);
            await _messenger.Send(message, browserConnectionId);
        }
        else
        {
            await Clients.Caller.SendChatMessage(
                senderName: string.Empty,
                message: string.Empty,
                orgName: string.Empty,
                orgId: string.Empty,
                disconnected: true,
                senderConnectionId: browserConnectionId);
        }
    }

    public async Task CheckForPendingRemoteControlSessions()
    {
        try
        {
            if (Device is null)
            {
                return;
            }

            _logger.LogDebug(
                "Checking for pending remote control sessions for device {deviceId}.",
                Device.ID);

            var waitingSessions = _remoteControlSessions
                .Sessions
                .Where(x => x.DeviceId == Device.ID);

            foreach (var session in waitingSessions)
            {
                _logger.LogDebug(
                    "Restarting remote control session {sessionId}.",
                    session.UnattendedSessionId);

                session.AgentConnectionId = Context.ConnectionId;
                await Clients.Caller.RestartScreenCaster(
                    session.ViewerList.ToArray(),
                    $"{session.UnattendedSessionId}",
                    session.AccessKey,
                    session.UserConnectionId,
                    session.RequesterName,
                    session.OrganizationName,
                    session.OrganizationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking for pending remote control sessions.");
        }
    }

    public async Task CheckForPendingScriptRuns()
    {
        if (Device is null)
        {
            return;
        }

        var authToken = _expiringTokenService.GetToken(Time.Now.AddMinutes(AppConstants.ScriptRunExpirationMinutes), Device.OrganizationID);
        var scriptRuns = await _dataService.GetPendingScriptRuns(Device.ID);

        foreach (var run in scriptRuns)
        {
            if (run.SavedScriptId is null)
            {
                continue;
            }
            await Clients.Caller.RunScript(
                run.SavedScriptId.Value,
                run.Id,
                run.Initiator ?? "Unknown Initiator",
                run.InputType,
                authToken);
        }
    }

    public async Task<bool> DeviceCameOnline(DeviceClientDto device)
    {
        try
        {
            if (await CheckForDeviceBan(device.ID, device.DeviceName))
            {
                return false;
            }

            var ip = Context.GetHttpContext()?.Connection?.RemoteIpAddress;
            if (ip != null && ip.IsIPv4MappedToIPv6)
            {
                ip = ip.MapToIPv4();
            }
            device.PublicIP = $"{ip}";

            if (await CheckForDeviceBan(device.PublicIP))
            {
                return false;
            }

            if (!await CheckAgentToken(device))
            {
                return false;
            }

            // Poste cloné : même DeviceID, autre machine, déjà connecté.
            if (_serviceSessionCache.TryGetByDeviceId(device.ID, out var connectedDevice) &&
                IsOtherMachine(connectedDevice, device))
            {
                _logger.LogWarning(
                    "DeviceID {DeviceId} en double : déjà connecté par {ConnectedName}, autre poste {DeviceName} ({PublicIp}).",
                    device.ID, connectedDevice.DeviceName, device.DeviceName, device.PublicIP);

                // Agent récent (il envoie son UUID) : il sait tirer un nouvel ID. Un ancien resterait refusé en boucle : accepté comme avant.
                if (!string.IsNullOrEmpty(device.HardwareId))
                {
                    await Clients.Caller.DeviceIdConflict();
                    return false;
                }
            }

            var result = await _dataService.AddOrUpdateDevice(device);
            if (!result.IsSuccess)
            {
                // Organization wasn't found.
                return false;
            }

            Device = result.Value;
            Device.HardwareId = device.HardwareId;

            // Appareil supprimé puis recréé alors que l'agent garde son jeton : le serveur ne peut plus le reprendre
            // (l'agent n'envoie qu'une preuve), l'agent refusera ce serveur. Voir ProveServer.

            _serviceSessionCache.AddOrUpdateByConnectionId(Context.ConnectionId, Device);

            var userIDs = _circuitManager.Connections.Select(x => x.User.Id);

            var filteredUserIDs = _dataService.FilterUsersByDevicePermission(userIDs, Device.ID);

            var connections = _circuitManager.Connections
                .Where(x => x.User.OrganizationID == Device.OrganizationID &&
                    filteredUserIDs.Contains(x.User.Id));

            foreach (var connection in connections)
            {
                var message = new DeviceStateChangedMessage(Device);
                await _messenger.Send(message, connection.ConnectionId);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while setting device to online status.");
        }

        Context.Abort();
        return false;
    }

    // UUID matériel des deux côtés : il fait foi (même nom possible). Sinon : le nom.
    // ponytail: deux clones au même nom et sans UUID lisible ne sont pas distingués.
    private static bool IsOtherMachine(Device connected, DeviceClientDto incoming)
    {
        if (!string.IsNullOrEmpty(connected.HardwareId) && !string.IsNullOrEmpty(incoming.HardwareId))
        {
            return connected.HardwareId != incoming.HardwareId;
        }
        return !string.Equals(connected.DeviceName, incoming.DeviceName, StringComparison.OrdinalIgnoreCase);
    }

    // Le jeton est le seul secret de l'agent : sans lui, connaître un DeviceID suffit pour prendre la place du poste.
    // Exigé dès que le poste a tourné avec un agent qui l'envoie (1.16+) ; les anciens agents passent comme avant.
    // ponytail: verrou déduit de la version d'agent en base (pas de migration) ; colonne dédiée si un agent < 1.16 doit revenir sur un poste.
    private async Task<bool> CheckAgentToken(DeviceClientDto device)
    {
        var stored = await _dataService.GetDevice(device.ID);
        if (!stored.IsSuccess || string.IsNullOrEmpty(stored.Value.ServerVerificationToken))
        {
            return true;
        }

        // Agent 1.17+ : preuve signée sur le défi de cette connexion (usage unique).
        if (Context.Items.Remove(AgentChallengeKey, out var challenge) &&
            challenge is string serverChallenge &&
            AgentTokenProof.AreEqual(AgentTokenProof.FromAgent(stored.Value.ServerVerificationToken, serverChallenge), device.ServerVerificationToken))
        {
            return true;
        }

        // Agent 1.16 : jeton en clair.
        if (AgentTokenProof.AreEqual(stored.Value.ServerVerificationToken, device.ServerVerificationToken))
        {
            return true;
        }

        if (string.IsNullOrEmpty(device.ServerVerificationToken) && !SendsAgentToken(stored.Value.AgentVersion))
        {
            return true;
        }

        _logger.LogWarning(
            "Jeton d'agent absent ou faux pour {DeviceId} ({DeviceName}, {PublicIp}, agent {AgentVersion}) : connexion refusée. " +
            "Agent réinstallé sur ce poste : supprimer l'appareil dans l'interface.",
            device.ID, device.DeviceName, device.PublicIP, device.AgentVersion);
        return false;
    }

    private static bool SendsAgentToken(string? agentVersion)
    {
        // Agents numérotés X.Y depuis la 1.15 ; avant, version datée (2026.x).
        return Version.TryParse(agentVersion, out var version) &&
            version.Major < 2000 &&
            version >= new Version(1, 16);
    }

    public string GetAgentChallenge()
    {
        var challenge = AgentTokenProof.NewChallenge();
        Context.Items[AgentChallengeKey] = challenge;
        return challenge;
    }

    // L'agent vérifie le serveur sans lui avoir jamais envoyé son jeton.
    public string ProveServer(string agentChallenge)
    {
        if (Device is null || string.IsNullOrEmpty(agentChallenge))
        {
            return string.Empty;
        }

        if (string.IsNullOrEmpty(Device.ServerVerificationToken))
        {
            _logger.LogWarning(
                "Poste {DeviceId} ({DeviceName}) inconnu du serveur mais déjà enrôlé : il refusera ce serveur. " +
                "Réinstaller l'agent, ou effacer ServerVerificationToken dans son ConnectionInfo.json.",
                Device.ID, Device.DeviceName);
            return string.Empty;
        }

        return AgentTokenProof.FromServer(Device.ServerVerificationToken, agentChallenge);
    }

    public async Task DeviceHeartbeat(DeviceClientDto device)
    {
        // Seul l'appareil enregistré sur cette connexion (DeviceCameOnline, jeton vérifié) peut se mettre à jour.
        if (Device is null || Device.ID != device.ID)
        {
            return;
        }

        if (await CheckForDeviceBan(device.ID, device.DeviceName))
        {
            return;
        }

        var ip = Context.GetHttpContext()?.Connection?.RemoteIpAddress;
        if (ip != null && ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }
        device.PublicIP = $"{ip}";

        if (await CheckForDeviceBan(device.PublicIP))
        {
            return;
        }


        var result = await _dataService.AddOrUpdateDevice(device);

        if (!result.IsSuccess)
        {
            return;
        }

        Device = result.Value;

        _serviceSessionCache.AddOrUpdateByConnectionId(Context.ConnectionId, Device);

        var userIDs = _circuitManager.Connections.Select(x => x.User.Id);

        var filteredUserIDs = _dataService.FilterUsersByDevicePermission(userIDs, Device.ID);

        var connections = _circuitManager.Connections
            .Where(x => x.User.OrganizationID == Device.OrganizationID &&
                filteredUserIDs.Contains(x.User.Id));

        foreach (var connection in connections)
        {
            var message = new DeviceStateChangedMessage(Device);
            await _messenger.Send(message, connection.ConnectionId);
        }


        await CheckForPendingScriptRuns();
    }

    public Task DisplayMessage(string consoleMessage, string popupMessage, string className, string requesterId)
    {
        var message = new DisplayNotificationMessage(consoleMessage, popupMessage, className);
        return _messenger.Send(message, requesterId);
    }

    public Task DownloadFile(string fileID, string requesterId)
    {
        var message = new DownloadFileMessage(fileID);
        return _messenger.Send(message, requesterId);
    }

    public Task DownloadFileProgress(int progressPercent, string requesterId)
    {
        var message = new DownloadFileProgressMessage(progressPercent);
        return _messenger.Send(message, requesterId);
    }

    public async Task<string> GetServerUrl()
    {
        var settings = await _dataService.GetSettings();
        return settings.ServerUrl;
    }

    // Agents antérieurs à la 1.17 uniquement : les récents passent par ProveServer.
    public string GetServerVerificationToken()
    {
        return $"{Device?.ServerVerificationToken}";
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            // Diagnostic des postes qui clignotent : cause de chaque déconnexion (coupure WebSocket, délai dépassé...).
            if (Device != null)
            {
                _logger.LogInformation(
                    "Agent {DeviceName} ({DeviceId}) déconnecté : {Reason}",
                    Device.DeviceName, Device.ID, exception?.Message ?? "fermeture normale");
            }
            else
            {
                _logger.LogDebug(
                    "Connexion agent fermée avant enregistrement : {Reason}",
                    exception?.Message ?? "fermeture normale");
            }

            // Si une connexion plus récente tient ce DeviceID, le poste n'est pas hors ligne.
            if (Device != null &&
                (!_serviceSessionCache.TryGetConnectionId(Device.ID, out var currentConnectionId) ||
                 currentConnectionId == Context.ConnectionId))
            {
                _dataService.DeviceDisconnected(Device.ID);

                Device.IsOnline = false;

                var userIDs = _circuitManager.Connections.Select(x => x.User.Id);

                var filteredUserIDs = _dataService.FilterUsersByDevicePermission(userIDs, Device.ID);

                var connections = _circuitManager.Connections
                    .Where(x => x.User.OrganizationID == Device.OrganizationID &&
                        filteredUserIDs.Contains(x.User.Id));

                foreach (var connection in connections)
                {
                    var message = new DeviceStateChangedMessage(Device);
                    await _messenger.Send(message, connection.ConnectionId);
                }
            }
            await base.OnDisconnectedAsync(exception);
        }
        finally
        {
            _serviceSessionCache.TryRemoveByConnectionId(Context.ConnectionId, out _);
        }
    }

    public Task ReturnPowerShellCompletions(PwshCommandCompletion completion, CompletionIntent intent, string senderConnectionId)
    {
        var message = new PowerShellCompletionsMessage(completion, intent);
        return _messenger.Send(message, senderConnectionId);
    }

    public async Task ScriptResult(string scriptResultId)
    {
        var result = await _dataService.GetScriptResult(scriptResultId);
        if (!result.IsSuccess)
        {
            return;
        }

        var message = new ScriptResultMessage(result.Value);
        await _messenger.Send(message, $"{result.Value.SenderConnectionID}");
    }

    public void ScriptResultViaApi(string commandID, string requestID)
    {
        ApiScriptResults.Set(requestID, commandID, DateTimeOffset.Now.AddHours(1));
    }

    public Task SendConnectionFailedToViewers(List<string> viewerIDs)
    {
        return _viewerHubContext.Clients.Clients(viewerIDs).SendAsync("ConnectionFailed");
    }

    public Task SendLogs(string logChunk, string requesterConnectionId)
    {
        var message = new ReceiveLogsMessage(logChunk);
        return _messenger.Send(message, requesterConnectionId);
    }

    public void SetServerVerificationToken(string verificationToken)
    {
        // Un jeton déjà posé ne se remplace pas : sinon un imposteur coupe le vrai poste.
        if (Device is null || !string.IsNullOrEmpty(Device.ServerVerificationToken))
        {
            return;
        }
        Device.ServerVerificationToken = verificationToken;
        _dataService.SetServerVerificationToken(Device.ID, verificationToken);
    }

    public Task TransferCompleted(string transferId, string requesterId)
    {
        var message = new TransferCompleteMessage(transferId);
        return _messenger.Send(message, requesterId);
    }

    private async Task<bool> CheckForDeviceBan(params string[] deviceIdNameOrIPs)
    {
        var settings = await _dataService.GetSettings();
        foreach (var device in deviceIdNameOrIPs)
        {
            if (string.IsNullOrWhiteSpace(device))
            {
                continue;
            }

            if (settings.BannedDevices.Any(x => !string.IsNullOrWhiteSpace(x) &&
                x.Equals(device, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning("Device ID/name/IP ({device}) is banned.  Sending uninstall command.", device);

                await Clients.Caller.UninstallAgent();
                return true;
            }
        }

        return false;
    }
}
