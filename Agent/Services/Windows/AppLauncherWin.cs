using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Remotely.Agent.Interfaces;
using Remotely.Shared.Helpers;
using Remotely.Shared.Models;
using Remotely.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Threading.Tasks;
using Remotely.Desktop.Native.Windows;

namespace Remotely.Agent.Services.Windows;

[SupportedOSPlatform("windows")]
public class AppLauncherWin : IAppLauncher
{
    private readonly ConnectionInfo _connectionInfo;
    private readonly ILogger<AppLauncherWin> _logger;
    private readonly string _rcBinaryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Desktop", EnvironmentHelper.DesktopExecutableFileName);

    public AppLauncherWin(IConfigService configService, ILogger<AppLauncherWin> logger)
    {
        _connectionInfo = configService.GetConnectionInfo();
        _logger = logger;
    }

    public async Task<int> LaunchChatService(string pipeName, string userConnectionId, string requesterName, string orgName, string orgId, HubConnection hubConnection)
    {
        try
        {
            if (!File.Exists(_rcBinaryPath))
            {
                await hubConnection.SendAsync("DisplayMessage", "Chat executable not found on target device.", "Executable not found on device.", "bg-danger", userConnectionId);
            }


            // Start Desktop app.
            await hubConnection.SendAsync("DisplayMessage", $"Starting chat service.", "Starting chat service.", "bg-success", userConnectionId);
            var args = GetDesktopArguments("Chat", requesterName, orgName, orgId);

            var processId = StartDesktopApp(args, targetSessionId: -1, pipeName: pipeName);
            if (processId > 0)
            {
                return processId;
            }

            await hubConnection.SendAsync("DisplayMessage",
                "Chat service failed to start on target device.",
                "Failed to start chat service.",
                "bg-danger",
                userConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while launching chat.");
            await hubConnection.SendAsync("DisplayMessage",
                "Chat service failed to start on target device.",
                "Failed to start chat service.",
                "bg-danger",
                userConnectionId);
        }
        return -1;
    }

    public async Task LaunchRemoteControl(int targetSessionId, string sessionId, string accessKey, string userConnectionId, string requesterName, string orgName, string orgId, HubConnection hubConnection)
    {
        try
        {
            if (!File.Exists(_rcBinaryPath))
            {
                await hubConnection.SendAsync("DisplayMessage",
                    "Remote control executable not found on target device.",
                    "Executable not found on device.",
                    "bg-danger",
                    userConnectionId);
                return;
            }


            // Start Desktop app.
            await hubConnection.SendAsync("DisplayMessage",
                "Starting remote control",
                "Starting remote control",
                "bg-success",
                userConnectionId);

            var args = GetDesktopArguments("Unattended", requesterName, orgName, orgId);
            args.AddRange(["--session-id", sessionId]);

            if (StartDesktopApp(args, targetSessionId, accessKey) <= 0)
            {
                await hubConnection.SendAsync("DisplayMessage",
                    "Remote control failed to start on target device.",
                    "Failed to start remote control.",
                    "bg-danger",
                    userConnectionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while launching remote control.");
            await hubConnection.SendAsync("DisplayMessage",
                "Remote control failed to start on target device.",
                "Failed to start remote control.",
                "bg-danger",
                userConnectionId);
        }
    }
    public async Task RestartScreenCaster(string[] viewerIds, string sessionId, string accessKey, string userConnectionId, string requesterName, string orgName, string orgId, HubConnection hubConnection, int targetSessionID = -1)
    {
        try
        {
            // Start Desktop app.                 
            _logger.LogInformation("Restarting screen caster.");

            var args = GetDesktopArguments("Unattended", requesterName, orgName, orgId);
            args.AddRange([
                "--relaunch", "true",
                "--session-id", sessionId,
                "--viewers", string.Join(",", viewerIds)]);

            if (WindowsIdentity.GetCurrent().IsSystem)
            {
                // Give a little time for session changing, etc.
                await Task.Delay(1000);
            }

            if (StartDesktopApp(args, targetSessionID, accessKey) <= 0)
            {
                _logger.LogWarning("Failed to relaunch screen caster.");
                await hubConnection.SendAsync("SendConnectionFailedToViewers", viewerIds);
                await hubConnection.SendAsync("DisplayMessage",
                    "Remote control failed to start on target device.",
                    "Failed to start remote control.",
                    "bg-danger",
                    userConnectionId);
            }
        }
        catch (Exception ex)
        {
            await hubConnection.SendAsync("SendConnectionFailedToViewers", viewerIds);
            _logger.LogError(ex, "Error while restarting screen caster.");
            throw;
        }
    }

    private List<string> GetDesktopArguments(string mode, string requesterName, string orgName, string orgId) =>
    [
        "--mode", mode,
        "--host", _connectionInfo.Host ?? string.Empty,
        "--requester-name", requesterName ?? string.Empty,
        "--org-name", orgName ?? string.Empty,
        "--org-id", orgId ?? string.Empty,
    ];

    // Les valeurs (nom du technicien, de l'organisation...) sont passées argument par argument :
    // un guillemet dans l'une d'elles ne peut pas ajouter d'option à la ligne de commande.
    // La clé d'accès passe par une variable d'environnement du seul processus enfant, pas par la
    // ligne de commande (liste des processus, journaux d'audit) : voir DesktopAccessKey.
    // Renvoie l'ID du processus, ou -1 si le lancement a échoué.
    private int StartDesktopApp(List<string> arguments, int targetSessionId, string? accessKey = null, string? pipeName = null)
    {
        var environment = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(accessKey))
        {
            environment[DesktopAccessKey.EnvironmentVariable] = accessKey;
        }
        if (!string.IsNullOrEmpty(pipeName))
        {
            environment[DesktopAccessKey.PipeNameEnvironmentVariable] = pipeName;
        }

        if (WindowsIdentity.GetCurrent().IsSystem)
        {
            var started = Win32Interop.CreateInteractiveSystemProcess(
                WindowsCommandLine.Build(_rcBinaryPath, arguments),
                targetSessionId: targetSessionId,
                hiddenWindow: false,
                out var procInfo,
                environment);
            return started ? procInfo.dwProcessId : -1;
        }

        var psi = new ProcessStartInfo(_rcBinaryPath);
        foreach (var argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }
        foreach (var (name, value) in environment)
        {
            psi.Environment[name] = value;
        }
        return Process.Start(psi)?.Id ?? -1;
    }
}