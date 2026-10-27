using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Remotely.Server.Components.ModalContents;
using Remotely.Server.Services;
using Remotely.Shared.Entities;
using Remotely.Shared.ViewModels;
using System.Text;
using System.Text.Encodings.Web;

namespace Remotely.Server.Components.Pages;

public partial class ManageOrganization : AuthComponentBase
{
    private readonly List<DeviceGroup> _deviceGroups = new();
    private readonly List<InviteLink> _invites = new();
    private readonly List<RemotelyUser> _orgUsers = new();
    private bool _inviteAsAdmin;
    private string _inviteEmail = string.Empty;
    private bool _isLoading = true;
    private string _newDeviceGroupName = string.Empty;
    private Organization? _organization;
    private string _selectedDeviceGroupId = string.Empty;

    [Inject]
    private IDataService DataService { get; set; } = null!;

    [Inject]
    private IEmailSenderEx EmailSender { get; set; } = null!;

    [Inject]
    private IJsInterop JsInterop { get; set; } = null!;

    [Inject]
    private IModalService ModalService { get; set; } = null!;

    [Inject]
    private NavigationManager NavManager { get; set; } = null!;

    [Inject]
    private IToastService ToastService { get; set; } = null!;
    [Inject]
    private UserManager<RemotelyUser> UserManager { get; set; } = null!;


    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await RefreshData();

        _isLoading = false;
    }

    private async Task CreateNewDeviceGroup()
    {
        EnsureUserSet();

        if (!User.IsAdministrator)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_newDeviceGroupName))
        {
            return;
        }

        var deviceGroup = new DeviceGroup()
        {
            Name = _newDeviceGroupName
        };

        var result = await DataService.AddDeviceGroup(User.OrganizationID, deviceGroup);
        if (!result.IsSuccess)
        {
            ToastService.ShowToast(result.Reason, classString: "bg-danger");
            return;
        }

        ToastService.ShowToast("Groupe créé.");
        _deviceGroups.Add(deviceGroup);
        _newDeviceGroupName = string.Empty;
    }

    private void DefaultOrgCheckChanged(ChangeEventArgs args)
    {
        if (_organization is null)
        {
            return;
        }

        EnsureUserSet();

        if (!User.IsServerAdmin)
        {
            return;
        }

        if (args.Value is not bool isDefault)
        {
            return;
        }

        DataService.SetIsDefaultOrganization(_organization.ID, isDefault);
        ToastService.ShowToast("Organisation par défaut définie.");
    }

    private async Task DeleteInvite(InviteLink invite)
    {
        EnsureUserSet();

        if (!User.IsAdministrator)
        {
            return;
        }

        var result = await JsInterop.Confirm("Supprimer cette invitation ?");
        if (!result)
        {
            return;
        }

        await DataService.DeleteInvite(User.OrganizationID, invite.ID);
        _invites.RemoveAll(x => x.ID == invite.ID);
        ToastService.ShowToast("Invitation supprimée.");
    }

    private async Task DeleteSelectedDeviceGroup()
    {
        EnsureUserSet();

        if (!User.IsAdministrator)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedDeviceGroupId))
        {
            return;
        }

        var result = await JsInterop.Confirm("Supprimer ce groupe ?");
        if (!result)
        {
            return;
        }

        await DataService.DeleteDeviceGroup(User.OrganizationID, _selectedDeviceGroupId);
        _deviceGroups.RemoveAll(x => x.ID == _selectedDeviceGroupId);
        _selectedDeviceGroupId = string.Empty;
    }

    private async Task DeleteUser(RemotelyUser user)
    {
        EnsureUserSet();

        if (!User.IsAdministrator)
        {
            return;
        }

        if (User.Id == user.Id)
        {
            ToastService.ShowToast("Impossible de supprimer votre propre compte.", classString: "bg-warning");
            return;
        }

        if (!CanManage(user))
        {
            return;
        }

        var result = await JsInterop.Confirm("Supprimer cet utilisateur ?");
        if (!result)
        {
            return;
        }

        await DataService.DeleteUser(User.OrganizationID, user.Id);
        _orgUsers.RemoveAll(x => x.Id == user.Id);
        ToastService.ShowToast("Utilisateur supprimé.");
    }

    private async Task EditDeviceGroups(RemotelyUser user)
    {
        void editDeviceGroupsModal(RenderTreeBuilder builder)
        {
            var deviceGroups = DataService.GetDeviceGroupsForOrganization(user.OrganizationID);

            builder.OpenComponent<EditDeviceGroup>(0);
            builder.AddAttribute(1, EditDeviceGroup.EditUserPropName, user);
            builder.AddAttribute(2, EditDeviceGroup.DeviceGroupsPropName, deviceGroups);
            builder.CloseComponent();
        }
        await ModalService.ShowModal("Groupes d'appareils", editDeviceGroupsModal);
    }

    private async Task EvaluateInviteInputKeypress(KeyboardEventArgs args)
    {
        if (args.Key.Equals("Enter", StringComparison.OrdinalIgnoreCase))
        {
            await SendInvite();
        }
    }

    private async Task EvaluateNewDeviceGroupKeyPress(KeyboardEventArgs args)
    {
        if (args.Key.Equals("Enter", StringComparison.OrdinalIgnoreCase))
        {
            await CreateNewDeviceGroup();
        }
    }
    private void OrganizationNameChanged(ChangeEventArgs args)
    {
        if (_organization is null)
        {
            return;
        }

        EnsureUserSet();

        if (!User.IsAdministrator)
        {
            return;
        }

        if (args.Value is not string newName)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        if (newName.Length > 25)
        {
            ToastService.ShowToast("25 caractères maximum.",
                classString: "bg-warning");
            return;
        }

        DataService.UpdateOrganizationName(_organization.ID, newName);
        _organization.OrganizationName = newName;
        ToastService.ShowToast("Nom de l'organisation modifié.");
    }

    private async Task RefreshData()
    {
        EnsureUserSet();

        var orgResult = await DataService.GetOrganizationByUserName(UserName);
        if (!orgResult.IsSuccess)
        {
            ToastService.ShowToast2(orgResult.Reason, Enums.ToastType.Warning);
            return;
        }

        _organization = orgResult.Value;
        _orgUsers.Clear();
        _invites.Clear();
        _deviceGroups.Clear();

        _invites.AddRange(DataService.GetAllInviteLinks(User.OrganizationID).OrderBy(x => x.InvitedUser));
        _deviceGroups.AddRange(DataService.GetDeviceGroups(UserName).OrderBy(x => x.Name));
        var orgUsers = await DataService.GetAllUsersInOrganization(User.OrganizationID);
        _orgUsers.AddRange(orgUsers.OrderBy(x => x.UserName));
    }
    // A server admin account can only be changed by a server admin.
    private bool CanManage(RemotelyUser target)
    {
        EnsureUserSet();

        if (!target.IsServerAdmin || User.IsServerAdmin)
        {
            return true;
        }
        ToastService.ShowToast("Seul un administrateur du serveur peut modifier le compte d'un administrateur du serveur.", classString: "bg-warning");
        return false;
    }

    private async Task ResetPassword(RemotelyUser user)
    {
        EnsureUserSet();

        if (!User.IsAdministrator)
        {
            return;
        }

        if (!CanManage(user))
        {
            return;
        }

        var code = await UserManager.GeneratePasswordResetTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var resetUrl = $"{NavManager.BaseUri}Account/ResetPassword?code={code}";

        await ModalService.ShowModal("Réinitialisation du mot de passe", builder =>
        {
            builder.AddMarkupContent(0, $@"<div class=""mb-3"">Lien de réinitialisation :</div>
                    <input readonly value=""{resetUrl}"" class=""form-control"" /> 
                    <div class=""mt-3"">Transmettez ce lien à l'utilisateur. Il doit être complètement déconnecté pour qu'il fonctionne.</div>");
        });
    }

    private async Task SendInvite()
    {
        EnsureUserSet();

        if (!User.IsAdministrator)
        {
            return;
        }

        if (!DataService.DoesUserExist(_inviteEmail))
        {
            var result = await DataService.CreateUser(_inviteEmail, _inviteAsAdmin, User.OrganizationID);
            if (result.IsSuccess)
            {
                var userResult = await DataService.GetUserByName(_inviteEmail);
                if (!userResult.IsSuccess)
                {
                    ToastService.ShowToast2(userResult.Reason, Enums.ToastType.Warning);
                    return;
                }

                var user = userResult.Value;

                await UserManager.ConfirmEmailAsync(user, await UserManager.GenerateEmailConfirmationTokenAsync(user));

                _orgUsers.Add(user);

                _inviteAsAdmin = false;
                _inviteEmail = string.Empty;
                ToastService.ShowToast("Compte créé.");
                return;
            }
            else
            {
                ToastService.ShowToast("Échec de la création du compte.", classString: "bg-danger");
                return;
            }
        }
        else
        {

            var invite = new InviteViewModel()
            {
                InvitedUser = _inviteEmail,
                IsAdmin = _inviteAsAdmin
            };
            var newInvite = await DataService.AddInvite(User.OrganizationID, invite);

            if (!newInvite.IsSuccess)
            {
                ToastService.ShowToast($"Échec de la création de l'invitation. {newInvite.Reason}", classString: "bg-danger");
                return;
            }

            var inviteURL = $"{NavManager.BaseUri}Invite/{newInvite.Value.ID}";
            var emailResult = await EmailSender.SendEmailAsync(invite.InvitedUser, "Invitation à rejoindre une organisation Remotely",
                    $@"<img src='{NavManager.BaseUri}images/Remotely_Logo.png'/>
                            <br><br>
                            Bonjour,
                            <br><br>
                            Vous êtes invité à rejoindre une organisation dans Remotely.
                            <br><br>
                            Pour la rejoindre, <a href='{HtmlEncoder.Default.Encode(inviteURL)}'>cliquez ici</a>.",
                    User.OrganizationID);
            if (emailResult)
            {
                ToastService.ShowToast("Invitation envoyée.");

                _inviteAsAdmin = false;
                _inviteEmail = string.Empty;
                _invites.Add(newInvite.Value);
            }
            else
            {
                ToastService.ShowToast("Échec de l'envoi de l'invitation.", classString: "bg-danger");
            }
        }
    }

    private async Task SetUserIsAdmin(ChangeEventArgs args, RemotelyUser orgUser)
    {
        EnsureUserSet();

        if (!User.IsAdministrator)
        {
            return;
        }

        if (args.Value is not bool isAdmin)
        {
            return;
        }

        if (!CanManage(orgUser))
        {
            return;
        }

        await DataService.ChangeUserIsAdmin(User.OrganizationID, orgUser.Id, isAdmin);
        ToastService.ShowToast("Droits d'administrateur modifiés.");
    }

    private void ShowDefaultOrgHelp()
    {
        ModalService.ShowModal("Organisation par défaut", new[]
        {
            "Réservé aux administrateurs du serveur : fait de cette organisation celle du serveur par défaut. " +
                "Quand l'application d'assistance ne peut pas déterminer l'organisation, elle prend la personnalisation de celle-ci."
        });
    }

    private void ShowDeviceGroupHelp()
    {
        ModalService.ShowModal("Groupes d'appareils", new[]
       {
            "Les groupes servent à restreindre les droits des utilisateurs et à filtrer les appareils " +
            "sur l'accueil.",
            "Tout le monde accède aux appareils hors groupe. Seuls les administrateurs " +
            "et les membres d'un groupe accèdent aux appareils de ce groupe."
        });
    }

    private void ShowInvitesHelp()
    {
        ModalService.ShowModal("Invitations", new[]
       {
            "Les invitations en attente s'affichent ici ; supprimez-en une pour la révoquer.",

            "Si l'utilisateur n'existe pas, l'invitation crée son compte dans cette organisation. " +
            "Un lien de réinitialisation du mot de passe se génère depuis la liste des utilisateurs.",

            "La case Administrateur donne au nouvel utilisateur les droits d'administration de cette organisation."
        });
    }

    private void ShowUsersHelp()
    {
        ModalService.ShowModal("Utilisateurs", new[]
        {
            "Les utilisateurs de l'organisation se gèrent ici.",
            "Les administrateurs ont accès à cet écran et à tous les appareils."
        });
    }
}
