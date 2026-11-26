using Remotely.Server.Services;
using Remotely.Shared.Helpers;
using Bitbound.SimpleMessenger;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.SignalR;
using Remotely.Server.Models;
using Remotely.Server.Models.Messages;
using Remotely.Server.Services.Stores;
using Remotely.Shared;
using Remotely.Shared.Dtos;
using Remotely.Shared.Entities;
using Remotely.Shared.Enums;
using Remotely.Shared.Interfaces;
using Remotely.Shared.Utilities;
using System.Collections.Concurrent;

namespace Remotely.Server.Hubs;

public interface ICircuitConnection
{
    string ConnectionId { get; }

    RemotelyUser User { get; }

    Task DeleteRemoteLogs(string deviceId);

    Task ExecuteCommandOnAgent(ScriptingShell shell, string command, string[] deviceIDs);

    Task GetPowerShellCompletions(string inputText, int currentIndex, CompletionIntent intent, bool? forward);

    Task GetRemoteLogs(string deviceId);

    Task ReinstallAgents(string[] deviceIDs);

    Task UpdateAgents(string[] deviceIDs);

    Task<Result<RemoteControlSession>> RemoteControl(string deviceID, bool viewOnly);

    Task RemoveDevices(string[] deviceIDs);

    Task RunScript(IEnumerable<string> deviceIds, Guid savedScriptId, int scriptRunId, ScriptInputType scriptInputType, bool runAsHostedService);

    Task SendChat(string message, string deviceId, bool isDisconnecting = false);
    Task<bool> TransferFileFromBrowserToAgent(string deviceId, string transferId, string[] fileIds);

    // Explorateur de fichiers (agents 1.27+).
    Task<Result<DeviceFileListDto>> ListDeviceDirectory(string deviceId, string path);
    Task<Result<string>> DownloadDeviceFile(string deviceId, string path);
    Task<Result> UploadFilesToDeviceFolder(string deviceId, string folder, string[] fileIds);

    Task TriggerHeartbeat(string deviceId);

    Task UninstallAgents(string[] deviceIDs);
    Task UpdateTags(string deviceID, string tags);

    /// <summary>
    /// Sends a Wake-On-LAN request for the specified device to its peer devices.
    /// Peer devices are those in the same group or the same public IP.
    /// </summary>
    /// <param name="device"></param>
    Task<Result> WakeDevice(Device device);

    /// <summary>
    /// Sends a Wake-On-LAN request for the specified device to its peer devices.
    /// Peer devices are those in the same group or the same public IP.
    /// </summary>
    /// <param name="devices"></param>
    Task<Result> WakeDevices(Device[] devices);
}

public class CircuitConnection : CircuitHandler, ICircuitConnection
{
    private readonly IHubContext<AgentHub, IAgentHubClient> _agentHubContext;
    private readonly IDataService _dataService;
    private readonly ISelectedCardsStore _cardStore;
    private readonly IAuthService _authService;
    private readonly ICircuitManager _circuitManager;
    private readonly IRemoteControlSessionCache _remoteControlSessionCache;
    private readonly IExpiringTokenService _expiringTokenService;
    private readonly ILogger<CircuitConnection> _logger;
    private readonly IAgentHubSessionCache _agentSessionCache;
    private readonly IMessenger _messenger;
    private readonly IToastService _toastService;
    private RemotelyUser? _user;

    public CircuitConnection(
        IAuthService authService,
        IDataService dataService,
        ISelectedCardsStore cardStore,
        IHubContext<AgentHub, IAgentHubClient> agentHubContext,
        ICircuitManager circuitManager,
        IToastService toastService,
        IExpiringTokenService expiringTokenService,
        IRemoteControlSessionCache remoteControlSessionCache,
        IAgentHubSessionCache agentSessionCache,
        IMessenger messenger,
        ILogger<CircuitConnection> logger)
    {
        _dataService = dataService;
        _agentHubContext = agentHubContext;
        _cardStore = cardStore;
        _authService = authService;
        _circuitManager = circuitManager;
        _toastService = toastService;
        _expiringTokenService = expiringTokenService;
        _remoteControlSessionCache = remoteControlSessionCache;
        _agentSessionCache = agentSessionCache;
        _messenger = messenger;
        _logger = logger;
    }


    public string ConnectionId { get; } = Guid.NewGuid().ToString();

    public RemotelyUser User
    {
        get => _user ?? throw new InvalidOperationException("User is not set.");
        internal set => _user = value;
    }


    public Task DeleteRemoteLogs(string deviceId)
    {
        var (canAccess, key) = CanAccessDevice(deviceId);
        if (!canAccess)
        {
            _toastService.ShowToast("Accès refusé.", classString: "bg-warning");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Delete logs command sent.  Device: {deviceId}.  User: {username}",
            deviceId,
            User?.UserName);

        return _agentHubContext.Clients.Client(key).DeleteLogs();
    }

    public async Task ExecuteCommandOnAgent(ScriptingShell shell, string command, string[] deviceIDs)
    {
        deviceIDs = _dataService.FilterDeviceIdsByUserPermission(deviceIDs, User);

        _logger.LogInformation("Command executed by {username}.  Shell: {shell}.  Command: {command}.  Devices: {deviceIds}",
              User.UserName,
              shell,
              command,
              string.Join(", ", deviceIDs));

        // Un jeton par poste : il ne sert qu'à envoyer le résultat de cette commande.
        foreach (var (deviceId, connectionId) in GetActiveDevicesForUserOrg(deviceIDs))
        {
            var authTokenForUploadingResults = _expiringTokenService.GetToken(
                ExpiringTokenScope.ForCommand(User.OrganizationID, deviceId, ExpiringTokenScope.TerminalResultWindow, Time.Now));

            await _agentHubContext.Clients.Client(connectionId).ExecuteCommand(
                shell,
                command,
                authTokenForUploadingResults,
                $"{User.UserName}",
                ConnectionId);
        }
    }

    public Task GetPowerShellCompletions(string inputText, int currentIndex, CompletionIntent intent, bool? forward)
    {
        var device = _cardStore.SelectedDevices.FirstOrDefault();
        if (device is null)
        {
            return Task.CompletedTask;
        }

        var (canAccess, key) = CanAccessDevice(device);
        if (!canAccess)
        {
            return Task.CompletedTask;
        }

        return _agentHubContext.Clients.Client(key).GetPowerShellCompletions(
            inputText,
            currentIndex,
            intent,
            forward,
            ConnectionId);
    }

    public Task GetRemoteLogs(string deviceId)
    {
        var (canAccess, key) = CanAccessDevice(deviceId);
        if (!canAccess)
        {
            _toastService.ShowToast("Accès refusé.", classString: "bg-warning");
            return Task.CompletedTask;
        }

        return _agentHubContext.Clients.Client(key).GetLogs(ConnectionId);
    }

    public override async Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(ConnectionId))
        {
            _circuitManager.TryRemoveConnection(ConnectionId, out _);
        }
        await base.OnCircuitClosedAsync(circuit, cancellationToken);
    }

    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        if (await _authService.IsAuthenticated())
        {
            var userResult = await _authService.GetUser();
            if (!userResult.IsSuccess)
            {
                _toastService.ShowToast2("Échec d'autorisation.", Enums.ToastType.Error);
                return;
            }
            User = userResult.Value;
            _circuitManager.TryAddConnection(ConnectionId, this);
        }
        await base.OnCircuitOpenedAsync(circuit, cancellationToken);
    }

    public async Task ReinstallAgents(string[] deviceIDs)
    {
        deviceIDs = _dataService.FilterDeviceIdsByUserPermission(deviceIDs, User);
        var connections = GetActiveConnectionsForUserOrg(deviceIDs);
        await _agentHubContext.Clients.Clients(connections).ReinstallAgent();
        _dataService.RemoveDevices(deviceIDs);
    }

    public async Task UpdateAgents(string[] deviceIDs)
    {
        deviceIDs = _dataService.FilterDeviceIdsByUserPermission(deviceIDs, User);
        var connections = GetActiveConnectionsForUserOrg(deviceIDs);
        await _agentHubContext.Clients.Clients(connections).ReinstallAgent();
    }

    public async Task<Result<RemoteControlSession>> RemoteControl(string deviceId, bool viewOnly)
    {
        var settings = await _dataService.GetSettings();

        if (!_agentSessionCache.TryGetByDeviceId(deviceId, out var targetDevice))
        {
            var message = new DisplayNotificationMessage(
                 "L'appareil sélectionné n'est pas en ligne.",
                 "Appareil hors ligne.",
                 "bg-warning");

            await _messenger.Send(message, ConnectionId);

            return Result.Fail<RemoteControlSession>("L'appareil n'est pas en ligne.");
        }


        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            var device = _dataService.GetDevice(targetDevice.ID);
            _logger.LogWarning(
                "Remote control attempted by unauthorized user.  Device ID: {deviceId}.  User Name: {userName}.",
                deviceId,
                User.UserName);
            return Result.Fail<RemoteControlSession>("Non autorisé.");

        }

        if (!_agentSessionCache.TryGetConnectionId(targetDevice.ID, out var serviceConnectionId))
        {
            var message = new DisplayNotificationMessage(
                "Connexion de l'agent introuvable.",
                "Connexion de l'agent introuvable.",
                "bg-warning");
            
            await _messenger.Send(message, ConnectionId);
            return Result.Fail<RemoteControlSession>("Connexion de l'agent introuvable.");
        }

        var sessionId = Guid.NewGuid();
        var accessKey = RandomGenerator.GenerateAccessKey();

        // La clé est rangée dans la session : seul le client de bureau lancé par l'agent la connaît
        // et peut s'enregistrer sous cet ID (DesktopHub.ReceiveUnattendedSessionInfo).
        var session = new RemoteControlSession()
        {
            UnattendedSessionId = sessionId,
            AccessKey = accessKey,
            UserConnectionId = ConnectionId,
            AgentConnectionId = serviceConnectionId,
            DeviceId = deviceId,
            ViewOnly = viewOnly,
            OrganizationId = User.OrganizationID,
            RequireConsent = settings.EnforceAttendedAccess,
            NotifyUserOnStart = settings.RemoteControlNotifyUser
        };

        _remoteControlSessionCache.AddOrUpdate($"{sessionId}", session);

        var orgResult = await _dataService.GetOrganizationNameByUserName($"{User.UserName}");

        if (!orgResult.IsSuccess)
        {
            _toastService.ShowToast2(orgResult.Reason, Enums.ToastType.Warning);
            return Result.Fail<RemoteControlSession>(orgResult.Reason);
        }

        await _agentHubContext.Clients.Client(serviceConnectionId).RemoteControl(
            sessionId,
            accessKey,
            ConnectionId,
            $"{User.UserOptions?.DisplayName}",
            orgResult.Value,
            User.OrganizationID);

        return Result.Ok(session);
    }

    public Task RemoveDevices(string[] deviceIDs)
    {
        if (User is not null)
        {
            var filterDevices = _dataService.FilterDeviceIdsByUserPermission(deviceIDs, User);
            _dataService.RemoveDevices(filterDevices);
        }

        return Task.CompletedTask;
    }

    public async Task RunScript(
        IEnumerable<string> deviceIds,
        Guid savedScriptId,
        int scriptRunId,
        ScriptInputType scriptInputType,
        bool runAsHostedService)
    {
        var username = string.Empty;
        if (runAsHostedService)
        {
            username = "Remotely Server";
        }
        else if (User is not null)
        {
            username = User.UserName;
            deviceIds = _dataService.FilterDeviceIdsByUserPermission(deviceIds.ToArray(), User);
        }

        // Planification comprise (sans utilisateur) : le script et les postes doivent être de la même organisation.
        var scriptResult = await _dataService.GetSavedScript(savedScriptId);
        if (!scriptResult.IsSuccess)
        {
            return;
        }
        var orgId = scriptResult.Value.OrganizationID;

        var orgDeviceIds = deviceIds.Where(id =>
            _agentSessionCache.TryGetByDeviceId(id, out var device) &&
            device.OrganizationID == orgId);

        // Un jeton par poste : ce script, cette exécution, un résultat.
        foreach (var deviceId in orgDeviceIds)
        {
            if (!_agentSessionCache.TryGetConnectionId(deviceId, out var connectionId))
            {
                continue;
            }

            var authToken = _expiringTokenService.GetToken(
                ExpiringTokenScope.ForScriptRun(orgId, deviceId, savedScriptId, scriptRunId, Time.Now));

            await _agentHubContext.Clients.Client(connectionId).RunScript(
                savedScriptId,
                scriptRunId,
                $"{username}",
                scriptInputType,
                authToken);
        }

    }

    public async Task SendChat(string message, string deviceId, bool isDisconnecting = false)
    {
        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            return;
        }

        if (!_agentSessionCache.TryGetByDeviceId(deviceId, out var device) ||
            !_agentSessionCache.TryGetConnectionId(deviceId, out var connectionId))
        {
            _toastService.ShowToast("Appareil introuvable.");
            return;
        }

        if (device.OrganizationID != User.OrganizationID)
        {
            _toastService.ShowToast("Non autorisé.");
            return;
        }

        var orgResult = await _dataService.GetOrganizationNameByUserName($"{User.UserName}");
        if (!orgResult.IsSuccess)
        {
            _toastService.ShowToast2("Organisation introuvable.", Enums.ToastType.Warning);
            return;
        }

        await _agentHubContext.Clients.Client(connectionId).SendChatMessage(
            User.UserOptions?.DisplayName ?? $"{User.UserName}",
            message,
            orgResult.Value,
            User.OrganizationID,
            isDisconnecting,
            ConnectionId);
    }

    public async Task<bool> TransferFileFromBrowserToAgent(string deviceId, string transferId, string[] fileIds)
    {
        if (!_agentSessionCache.TryGetConnectionId(deviceId, out var connectionId))
        {
            return false;
        }

        if (!_dataService.DoesUserHaveAccessToDevice(deviceId, User))
        {
            _logger.LogWarning("User {username} does not have access to device ID {deviceId} and attempted file upload.",
                User.UserName,
                deviceId);

            return false;
        }

        // Jeton limité à ces fichiers.
        var authToken = _expiringTokenService.GetToken(
            ExpiringTokenScope.ForFiles(User.OrganizationID, deviceId, fileIds, Time.Now));

        await _agentHubContext.Clients
            .Client(connectionId)
            .TransferFileFromBrowserToAgent(
                transferId,
                fileIds,
                ConnectionId,
                authToken);

        return true;
    }

    // Délai des appels de l'explorateur de fichiers ; un agent antérieur à la 1.27 ne répond pas.
    private static readonly TimeSpan DeviceFileTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DeviceFileTransferTimeout = TimeSpan.FromMinutes(10);
    private const string AgentTooOld = "Le poste ne répond pas : agent injoignable ou antérieur à la 1.27.";

    public async Task<Result<DeviceFileListDto>> ListDeviceDirectory(string deviceId, string path)
    {
        var (canAccess, connectionId) = CanAccessDevice(deviceId);
        if (!canAccess)
        {
            return Result.Fail<DeviceFileListDto>("Appareil introuvable ou accès refusé.");
        }

        try
        {
            var listing = await _agentHubContext.Clients.Client(connectionId)
                .ListDeviceDirectory(path)
                .WaitAsync(DeviceFileTimeout);
            return Result.Ok(listing);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "File explorer listing failed on device {deviceId}.", deviceId);
            return Result.Fail<DeviceFileListDto>(AgentTooOld);
        }
    }

    public async Task<Result<string>> DownloadDeviceFile(string deviceId, string path)
    {
        var (canAccess, connectionId) = CanAccessDevice(deviceId);
        if (!canAccess)
        {
            return Result.Fail<string>("Appareil introuvable ou accès refusé.");
        }

        _logger.LogInformation("File explorer: {username} downloads {path} from device {deviceId}.", User.UserName, path, deviceId);

        var scope = ExpiringTokenScope.ForDeviceUpload(User.OrganizationID, deviceId, Time.Now);
        var authToken = _expiringTokenService.GetToken(scope);
        try
        {
            var error = await _agentHubContext.Clients.Client(connectionId)
                .SendDeviceFile(path, authToken)
                .WaitAsync(DeviceFileTransferTimeout);

            if (!string.IsNullOrEmpty(error))
            {
                return Result.Fail<string>(error);
            }
            return scope.UploadedFileId is { } fileId
                ? Result.Ok(fileId)
                : Result.Fail<string>("Fichier non reçu par le serveur.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "File explorer download failed on device {deviceId}.", deviceId);
            return Result.Fail<string>(AgentTooOld);
        }
    }

    public async Task<Result> UploadFilesToDeviceFolder(string deviceId, string folder, string[] fileIds)
    {
        var (canAccess, connectionId) = CanAccessDevice(deviceId);
        if (!canAccess)
        {
            return Result.Fail("Appareil introuvable ou accès refusé.");
        }

        _logger.LogInformation("File explorer: {username} sends {count} file(s) to {folder} on device {deviceId}.",
            User.UserName, fileIds.Length, folder, deviceId);

        var authToken = _expiringTokenService.GetToken(
            ExpiringTokenScope.ForFiles(User.OrganizationID, deviceId, fileIds, Time.Now));
        try
        {
            var error = await _agentHubContext.Clients.Client(connectionId)
                .ReceiveFilesToFolder(fileIds, folder, authToken)
                .WaitAsync(DeviceFileTransferTimeout);
            return string.IsNullOrEmpty(error) ? Result.Ok() : Result.Fail(error);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "File explorer upload failed on device {deviceId}.", deviceId);
            return Result.Fail(AgentTooOld);
        }
    }

    public async Task TriggerHeartbeat(string deviceId)
    {
        var (canAccess, connectionId) = CanAccessDevice(deviceId);

        if (!canAccess)
        {
            return;
        }

        await _agentHubContext.Clients.Client(connectionId).TriggerHeartbeat();
    }

    public async Task UninstallAgents(string[] deviceIDs)
    {
        deviceIDs = _dataService.FilterDeviceIdsByUserPermission(deviceIDs, User);
        var connections = GetActiveConnectionsForUserOrg(deviceIDs);
        await _agentHubContext.Clients.Clients(connections).UninstallAgent();
        _dataService.RemoveDevices(deviceIDs);
    }

    public async Task UpdateTags(string deviceID, string tags)
    {
        if (_dataService.DoesUserHaveAccessToDevice(deviceID, User))
        {
            if (tags.Length > 200)
            {
                var message = new DisplayNotificationMessage(
                     $"Les étiquettes sont limitées à 200 caractères ({tags.Length} saisis).",
                    "200 caractères au plus.",
                    "bg-warning");

                await _messenger.Send(message, ConnectionId);
                return;
            }

            await _dataService.UpdateTags(deviceID, tags);

            var successMessage = new DisplayNotificationMessage(
                "Appareil mis à jour.",
                "Appareil mis à jour.",
                "bg-success");

            await _messenger.Send(successMessage, ConnectionId);
        }
    }

    public async Task<Result> WakeDevice(Device device)
    {
        try
        {
            if (!_dataService.DoesUserHaveAccessToDevice(device.ID, User.Id))
            {
                return Result.Fail("Non autorisé.");
            }

            var availableDevices = _agentSessionCache
                .GetAllDevices()
                .Where(x =>
                     x.OrganizationID == User.OrganizationID &&
                    (x.DeviceGroupID == device.DeviceGroupID || x.PublicIP == device.PublicIP))
                .ToArray();

            await SendWakeCommand(device, availableDevices);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error waking device {deviceId}.", device.ID);
            return Result.Fail(ex);
        }
    }

    public async Task<Result> WakeDevices(Device[] devices)
    {
        try
        {
            var deviceIds = devices.Select(x => x.ID).ToArray();
            var filteredIds = _dataService.FilterDeviceIdsByUserPermission(deviceIds, User);
            var filteredDevices = devices.Where(x => filteredIds.Contains(x.ID)).ToArray();

            var availableDevices = _agentSessionCache
                .GetAllDevices()
                .Where(x => x.OrganizationID == User.OrganizationID);

            var devicesByGroupId = new ConcurrentDictionary<string, List<Device>>();
            var devicesByPublicIp = new ConcurrentDictionary<string, List<Device>>();

            foreach (var device in availableDevices)
            {
                // Indexé par groupe et par IP : presque tous les postes sont dans un groupe
                // (groupe par défaut), la correspondance par IP doit rester possible.
                if (!string.IsNullOrWhiteSpace(device.DeviceGroupID))
                {
                    var group = devicesByGroupId.GetOrAdd(device.DeviceGroupID, key => new());
                    group.Add(device);
                }

                if (!string.IsNullOrWhiteSpace(device.PublicIP))
                {
                    var group = devicesByPublicIp.GetOrAdd(device.PublicIP, key => new());
                    group.Add(device);
                }
            }

            foreach (var deviceToWake in filteredDevices)
            {
                var peers = new List<Device>();

                if (!string.IsNullOrWhiteSpace(deviceToWake.DeviceGroupID) &&
                    devicesByGroupId.TryGetValue(deviceToWake.DeviceGroupID, out var groupList))
                {
                    peers.AddRange(groupList);
                }

                if (!string.IsNullOrWhiteSpace(deviceToWake.PublicIP) &&
                    devicesByPublicIp.TryGetValue(deviceToWake.PublicIP, out var ipList))
                {
                    peers.AddRange(ipList);
                }

                // Un pair du même groupe et de la même IP ne reçoit la commande qu'une fois.
                await SendWakeCommand(deviceToWake, peers.Distinct());
            }
            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while waking devices.");
            return Result.Fail(ex);
        }
    }

    private (bool canAccess, string connectionId) CanAccessDevice(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return (false, string.Empty);
        }

        if (!_agentSessionCache.TryGetByDeviceId(deviceId, out var device) ||
            !_dataService.DoesUserHaveAccessToDevice(device.ID, User) ||
            !_agentSessionCache.TryGetConnectionId(device.ID, out var connectionId))
        {
            return (false, string.Empty);
        }


        return (true, connectionId);
    }

    private IEnumerable<string> GetActiveConnectionsForUserOrg(IEnumerable<string> deviceIds) =>
        GetActiveDevicesForUserOrg(deviceIds).Select(x => x.ConnectionId);

    private IEnumerable<(string DeviceId, string ConnectionId)> GetActiveDevicesForUserOrg(IEnumerable<string> deviceIds)
    {

        foreach (var deviceId in deviceIds)
        {
            if (!_agentSessionCache.TryGetByDeviceId(deviceId, out var device))
            {
                continue;
            }

            if (device.OrganizationID != User.OrganizationID)
            {
                continue;
            }

            if (_agentSessionCache.TryGetConnectionId(device.ID, out var connectionId))
            {
                yield return (device.ID, connectionId);
            }
        }
    }


    private async Task SendWakeCommand(Device deviceToWake, IEnumerable<Device> peerDevices)
    {
        foreach (var peerDevice in peerDevices)
        {
            // Base remplie avant le contrôle des adresses : on refiltre.
            foreach (var mac in MacAddressHelper.Sanitize(deviceToWake.MacAddresses))
            {
                if (_agentSessionCache.TryGetConnectionId(peerDevice.ID, out var connectionId))
                {
                    _logger.LogInformation(
                        "Sending wake command for device {deviceName} ({deviceId}) to " +
                        "peer device {peerDeviceName} ({peerDeviceId}).  " +
                        "Sender: {username}.",
                        deviceToWake.DeviceName,
                        deviceToWake.ID,
                        peerDevice.DeviceName,
                        peerDevice.ID,
                        User.UserName);
                    await _agentHubContext.Clients.Client(connectionId).WakeDevice(mac);
                }
            }
        }
    }
}
