using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR;
using Remotely.Server.Hubs;
using Remotely.Server.Models;
using Remotely.Server.Services;
using Remotely.Shared.Entities;
using Remotely.Shared.Interfaces;
using System.Net;

namespace Remotely.Server.Components.Pages;

public partial class ServerConfig : AuthComponentBase
{
    private readonly List<RemotelyUser> _userList = new();
    private string? _alertMessage;
    private string? _bannedDeviceSelected;
    private string? _bannedDeviceToAdd;

    private string? _knownProxySelected;
    private string? _knownProxyToAdd;

    private bool _showAdminsOnly;
    private bool _showMyOrgAdminsOnly = true;
    private string? _trustedCorsOriginSelected;
    private string? _trustedCorsOriginToAdd;

    [Inject]
    public required IHubContext<AgentHub, IAgentHubClient> AgentHubContext { get; init; }

    [Inject]
    public required ICircuitManager CircuitManager { get; init; }

    [Inject]
    public required IDataService DataService { get; init; }

    [Inject]
    public required IEmailSenderEx EmailSender { get; init; }

    [Inject]
    public required IWebHostEnvironment HostEnv { get; init; }

    [Inject]
    public required ILogger<ServerConfig> Logger { get; init; }

    [Inject]
    public required IModalService ModalService { get; init; }

    [Inject]
    public required IAgentHubSessionCache ServiceSessionCache { get; init; }

    [Inject]
    public required IToastService ToastService { get; init; }

    [Inject]
    public required IUpgradeService UpgradeService { get; init; }


    private SettingsModel Input { get; set; } = new();
    private IEnumerable<string> OutdatedDevices => GetOutdatedDevices();
    private int TotalDevices => DataService.GetTotalDevices();

    private IEnumerable<RemotelyUser> UserList
    {
        get
        {
            if (User is null)
            {
                return [];
            }

            EnsureUserSet();

            return _userList.Where(x =>
                (!_showAdminsOnly || x.IsServerAdmin) &&
                (!_showMyOrgAdminsOnly || x.OrganizationID == User.OrganizationID));
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        EnsureUserSet();
        if (!User.IsServerAdmin)
        {
            return;
        }

        Input = await DataService.GetSettings();

        _userList.AddRange(DataService.GetAllUsersForServer().OrderBy(x => x.UserName));
    }

    private void AddBannedDevice()
    {
        if (string.IsNullOrWhiteSpace(_bannedDeviceToAdd))
        {
            return;
        }

        Input.BannedDevices.Add(_bannedDeviceToAdd);
        _bannedDeviceToAdd = string.Empty;
    }

    private void AddKnownProxy()
    {
        if (IPAddress.TryParse(_knownProxyToAdd, out _))
        {
            Input.KnownProxies.Add(_knownProxyToAdd);
        }
        else
        {
            ToastService.ShowToast2("Adresse IP invalide.", Enums.ToastType.Warning);
        }

        _knownProxyToAdd = string.Empty;
    }

    private void AddTrustedCorsOrigin()
    {
        if (string.IsNullOrWhiteSpace(_trustedCorsOriginToAdd))
        {
            return;
        }

        Input.TrustedCorsOrigins.Add(_trustedCorsOriginToAdd);
        _trustedCorsOriginToAdd = null;
    }

    private IEnumerable<string> GetOutdatedDevices()
    {
        try
        {
            var currentAgentVersion = UpgradeService.GetCurrentAgentVersion();

            return ServiceSessionCache.GetAllDevices()
                .Where(x => AgentVersion.IsOutdated(x.AgentVersion, currentAgentVersion))
                .Select(x => x.ID);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while getting outdated devices.");
        }

        return Enumerable.Empty<string>();
    }

    private void HandleBannedDeviceKeyDown(KeyboardEventArgs args)
    {
        if (args.Key.Equals("Enter", StringComparison.OrdinalIgnoreCase))
        {
            AddBannedDevice();
        }
    }

    private void HandleKnownProxyKeyDown(KeyboardEventArgs args)
    {
        if (args.Key.Equals("Enter", StringComparison.OrdinalIgnoreCase))
        {
            AddKnownProxy();
        }
    }

    private void HandleTrustedCorsKeyDown(KeyboardEventArgs args)
    {
        if (args.Key.Equals("Enter", StringComparison.OrdinalIgnoreCase))
        {
            AddTrustedCorsOrigin();
        }
    }
    private async Task HandleValidSubmit(EditContext context)
    {
        await Save();
    }

    private void RemoveBannedDevice()
    {
        if (string.IsNullOrWhiteSpace(_bannedDeviceSelected))
        {
            return;
        }

        Input.BannedDevices.Remove(_bannedDeviceSelected);
        _bannedDeviceSelected = null;
    }

    private void RemoveKnownProxy()
    {
        if (string.IsNullOrWhiteSpace(_knownProxySelected))
        {
            return;
        }
        Input.KnownProxies.Remove(_knownProxySelected);
        _knownProxySelected = null;
    }

    private void RemoveTrustedCorsOrigin()
    {
        if (string.IsNullOrWhiteSpace(_trustedCorsOriginSelected))
        {
            return;
        }

        Input.TrustedCorsOrigins.Remove(_trustedCorsOriginSelected);
        _trustedCorsOriginSelected = null;
    }

    private async Task Save()
    {
        
        await DataService.SaveSettings(Input);

        ToastService.ShowToast("Configuration enregistrée.");
    }

    private async Task SaveAndTestSmtpSettings()
    {
        EnsureUserSet();

        await DataService.SaveSettings(Input);

        if (string.IsNullOrWhiteSpace(User.Email))
        {
            ToastService.ShowToast2("Votre adresse e-mail n'est pas renseignée.", Enums.ToastType.Warning);
            return;
        }

        var success = await EmailSender.SendEmailAsync(User.Email, "Remotely : e-mail de test", "Les paramètres SMTP fonctionnent.", User.OrganizationID);
        if (success)
        {
            ToastService.ShowToast($"E-mail de test envoyé à {User.Email}. Vérifiez la boîte de réception (ou les indésirables).");
            _alertMessage = $"E-mail de test envoyé à {User.Email}. Vérifiez la boîte de réception (ou les indésirables).";
        }
        else
        {
            ToastService.ShowToast("Échec de l'envoi. Détails dans les journaux du serveur.", classString: "bg-danger");
            _alertMessage = "Échec de l'envoi. Détails dans les journaux du serveur.";
        }
    }

    private void SetIsServerAdmin(ChangeEventArgs ev, RemotelyUser user)
    {
        if (ev.Value is not bool isAdmin)
        {
            return;
        }

        EnsureUserSet();

        DataService.SetIsServerAdmin(user.Id, isAdmin, User.Id);
        ToastService.ShowToast("Administrateurs du serveur mis à jour.");
    }

    private void ShowOutdatedDevices()
    {
        if (OutdatedDevices.Any())
        {
            var outdatedDeviceNames = DataService
                .GetDevices(OutdatedDevices)
                .Select(x => $"{x.DeviceName}");

            var body = (new[] { "Agents obsolètes :" })
                .Concat(outdatedDeviceNames)
                .ToArray();

            ModalService.ShowModal("Agents obsolètes", body);
        }
        else
        {
            ModalService.ShowModal("Agents obsolètes", new[] { "Aucun agent obsolète en ligne." });
        }
    }

    private async Task UpdateAllDevices()
    {
        EnsureUserSet();

        if (!User.IsServerAdmin)
        {
            return;
        }

        if (!OutdatedDevices.Any())
        {
            ToastService.ShowToast("Aucun agent à mettre à jour.");
            return;
        }

        var agentConnections = ServiceSessionCache.GetConnectionIdsByDeviceIds(OutdatedDevices);

        await AgentHubContext.Clients.Clients(agentConnections).ReinstallAgent();
        ToastService.ShowToast("Commande de mise à jour envoyée.");
    }
}
