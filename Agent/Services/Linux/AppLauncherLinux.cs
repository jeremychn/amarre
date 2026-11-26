using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Remotely.Agent.Interfaces;
using Remotely.Shared.Extensions;
using Remotely.Shared.Helpers;
using Remotely.Shared.Models;
using Remotely.Shared.Primitives;
using Remotely.Shared.Services;
using Remotely.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Remotely.Agent.Services.Linux;


public class AppLauncherLinux : IAppLauncher
{
    private readonly ConnectionInfo _connectionInfo;
    private readonly ILogger<AppLauncherLinux> _logger;
    private readonly IProcessInvoker _processInvoker;

    private readonly string _rcBinaryPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, 
        "Desktop", 
        EnvironmentHelper.DesktopExecutableFileName);

    public AppLauncherLinux(
        IConfigService configService,
        IProcessInvoker processInvoker,
        ILogger<AppLauncherLinux> logger)
    {
        _processInvoker = processInvoker;
        _connectionInfo = configService.GetConnectionInfo();
        _logger = logger;
    }


    public async Task<int> LaunchChatService(string pipeName, string userConnectionId, string requesterName, string orgName, string orgId, HubConnection hubConnection)
    {
        try
        {
            if (!File.Exists(_rcBinaryPath))
            {
                await hubConnection.SendAsync("DisplayMessage",
                    "Programme de chat introuvable sur le poste.",
                    "Programme introuvable sur le poste.",
                    "bg-danger",
                    userConnectionId);
            }


            // Start Desktop app.
            await hubConnection.SendAsync("DisplayMessage", "Démarrage du chat.", "Starting chat service.", "bg-success", userConnectionId);
            var args = GetDesktopArguments("Chat", requesterName, orgName, orgId);
            // Nom du tube sur l'entrée standard, comme la clé d'accès.
            args.AddRange(["--pipe-name", DesktopAccessKey.FromStandardInput]);
            return StartLinuxDesktopApp(args, pipeName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while starting chat.");
            await hubConnection.SendAsync("DisplayMessage", "Le chat n'a pas pu démarrer sur le poste.", "Échec du démarrage du chat.", "bg-danger", userConnectionId);
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
                    "Programme de prise en main introuvable sur le poste.",
                    "Programme introuvable sur le poste.",
                    "bg-danger",
                    userConnectionId);
                return;
            }


            // Start Desktop app.
            await hubConnection.SendAsync("DisplayMessage", "Démarrage de la prise en main.", "Démarrage de la prise en main.", "bg-success", userConnectionId);
            var args = GetDesktopArguments("Unattended", requesterName, orgName, orgId);
            args.AddRange(["--session-id", sessionId, "--access-key", DesktopAccessKey.FromStandardInput]);
            StartLinuxDesktopApp(args, accessKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while launching remote control.");
            await hubConnection.SendAsync("DisplayMessage", "La prise en main n'a pas pu démarrer sur le poste.", "Échec du démarrage de la prise en main.", "bg-danger", userConnectionId);
        }
    }

    public async Task RestartScreenCaster(string[] viewerIds, string sessionId, string accessKey, string userConnectionId, string requesterName, string orgName, string orgId, HubConnection hubConnection, int targetSessionID = -1)
    {
        try
        {
            var args = GetDesktopArguments("Unattended", requesterName, orgName, orgId);
            args.AddRange(["--session-id", sessionId, "--access-key", DesktopAccessKey.FromStandardInput]);
            StartLinuxDesktopApp(args, accessKey);
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

    // Les valeurs (nom du technicien, de l'organisation...) sont passées argument par argument
    // (ArgumentList) : un guillemet ou une espace dans l'une d'elles ne peut pas ajouter d'option
    // à sudo ni au client de bureau.
    // La clé d'accès part sur l'entrée standard (« --access-key - »), pas dans les arguments :
    // /proc/<pid>/cmdline est lisible par tout utilisateur du poste, et sudo ne transmet pas
    // les variables d'environnement.
    // standardInput : clé d'accès, ou nom du tube du chat.
    private int StartLinuxDesktopApp(List<string> desktopArgs, string? accessKey = null)
    {
        var xdisplay = ":0";
        var xauthority = string.Empty;
        
        var xResult = TryGetXAuth("Xorg");

        if (!xResult.IsSuccess)
        {
            // If running Wayland, this still ends up still being unusable.
            // This X server will only provide a black screen with any apps
            // launched within the display it's using, but the display won't
            // show anything being rendered by the Wayland compositor.  It's
            // better than simply crashing, though, so I'll leave it here
            // until Wayland support is added.
            xResult = TryGetXAuth("Xwayland");
        }

        if (xResult.IsSuccess)
        {
            xdisplay = xResult.Value.XDisplay;
            xauthority = xResult.Value.XAuthority;
        }
        else
        {
            _logger.LogError("Failed to get X server auth.");
        }
        
        var whoString = _processInvoker.InvokeProcessOutput("who", "")?.Trim();
        var username = "";

        if (!string.IsNullOrWhiteSpace(whoString))
        {
            try
            {
                var whoLines = whoString.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                var whoLine = whoLines.FirstOrDefault(x =>
                    Regex.IsMatch(
                        x.Split(" ", StringSplitOptions.RemoveEmptyEntries).Last(),
                        @"\(:[\d]*\)"));

                if (!string.IsNullOrWhiteSpace(whoLine))
                {
                    var whoSplit = whoLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    username = whoSplit[0];
                    xdisplay = whoSplit.Last().TrimStart('(').TrimEnd(')');
                    xauthority = $"/home/{username}/.Xauthority";

                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting current X11 user.");
            }
        }

        var psi = new ProcessStartInfo("sudo");
        if (!string.IsNullOrWhiteSpace(username))
        {
            psi.ArgumentList.Add("-u");
            psi.ArgumentList.Add(username);
        }
        // Fin des options de sudo : la suite est la commande et ses arguments.
        psi.ArgumentList.Add("--");
        psi.ArgumentList.Add(_rcBinaryPath);
        foreach (var argument in desktopArgs)
        {
            psi.ArgumentList.Add(argument);
        }

        psi.Environment.Add("DISPLAY", xdisplay);
        psi.Environment.Add("XAUTHORITY", xauthority);
        // Pas d'arguments dans le journal : ils contiennent la clé d'accès de la session.
        _logger.LogInformation(
            "Attempting to launch screen caster with username {username}, xauthority {xauthority}, and display {display}.",
            username,
            xauthority,
            xdisplay);
        psi.RedirectStandardInput = accessKey is not null;
        var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to launch desktop app.");
        if (accessKey is not null)
        {
            process.StandardInput.WriteLine(accessKey);
            process.StandardInput.Close();
        }
        return process.Id;
    }

    private Result<XAuthInfo> TryGetXAuth(string xServerProcess)
    {
        try
        {
            var xdisplay = ":0";
            var xauthority = string.Empty;

            var xprocess = _processInvoker
                .InvokeProcessOutput("ps", $"-C {xServerProcess} -f")
                ?.Split(Environment.NewLine)
                ?.FirstOrDefault(x => x.Contains(" -auth "));

            if (string.IsNullOrWhiteSpace(xprocess))
            {
                _logger.LogInformation("{xServerProcess} process not found.", xServerProcess);
                return Result.Fail<XAuthInfo>($"{xServerProcess} process not found.");
            }

            _logger.LogInformation("Resolved X server process: {xprocess}", xprocess);

            var xprocSplit = xprocess
                .Split(" ", StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            var xProcIndex = xprocSplit.IndexWhere(x => x.EndsWith(xServerProcess));
            if (xProcIndex > -1 && xprocSplit[xProcIndex + 1].StartsWith(":"))
            {
                xdisplay = xprocSplit[xProcIndex + 1];
            }

            var authIndex = xprocSplit.IndexOf("-auth");
            if (authIndex > -1)
            {
                xauthority = xprocSplit[authIndex + 1];
            }
            return Result.Ok(new XAuthInfo(xdisplay, xauthority));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while geting X auth for {xServerProcess}.", xServerProcess);
            return Result.Fail<XAuthInfo>($"Error while geting X auth for {xServerProcess}.");
        }
    }
    private record XAuthInfo(string XDisplay, string XAuthority);
}
