using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Remotely.Server.Components.Pages;
using Remotely.Server.Enums;
using Remotely.Server.Services;
using Remotely.Shared.Entities;

namespace Remotely.Server.Components.Scripts;

[Authorize]
public partial class SavedScripts : AuthComponentBase
{
    [CascadingParameter]
    private ScriptsPage ParentPage { get; set; } = null!;

    private SavedScript _selectedScript = new() { Name = string.Empty };
    private string _alertMessage = string.Empty;
    private string _alertOptionsShowClass = string.Empty;
    private string _environmentVarsShowClass = string.Empty;

    [Inject]
    public IDataService DataService { get; set; } = null!;

    [Inject]
    public IToastService ToastService { get; set; } = null!;

    [Inject]
    public IJsInterop JsInterop { get; set; } = null!;

    [Inject]
    public IModalService ModalService { get; set; } = null!;

    [Inject]
    public required ILogger<SavedScripts> Logger { get; set; }

    private bool CanModifyScript
    {
        get
        {
            if (User is null)
            {
                return false;
            }

            return 
                _selectedScript.Id == Guid.Empty ||
                _selectedScript.CreatorId == User.Id || User.IsAdministrator;
        }
    }

    private bool CanDeleteScript
    {
        get
        {
            if (User is null)
            {
                return false;
            }

            return 
                !string.IsNullOrWhiteSpace(_selectedScript.CreatorId) &&
                (_selectedScript.CreatorId == User.Id || User.IsAdministrator);
        }
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            JsInterop.AutoHeight();
        }
        base.OnAfterRender(firstRender);
    }

    private async Task OnValidSubmit(EditContext context)
    {
        EnsureUserSet();

        if (_selectedScript is null)
        {
            return;
        }

        if (!CanModifyScript)
        {
            ToastService.ShowToast("Impossible de modifier le script d'un autre utilisateur.", classString: "bg-warning");
            return;
        }
        
        await DataService.AddOrUpdateSavedScript(_selectedScript, User.Id);
        await ParentPage.RefreshScripts();
        ToastService.ShowToast("Script enregistré.");
        _alertMessage = "Script enregistré.";
    }

    private void CreateNew()
    {
        _selectedScript = new()
        {
            Name = string.Empty
        };
    }

    private async Task DeleteSelectedScript()
    {
        try
        {
            if (!CanDeleteScript)
            {
                ToastService.ShowToast("Impossible de supprimer le script d'un autre utilisateur.", classString: "bg-warning");
                return;
            }

            var result = await JsInterop.Confirm($"Supprimer le script {_selectedScript.Name} ?");
            if (result)
            {
                await DataService.DeleteSavedScript(_selectedScript.Id);
                ToastService.ShowToast("Script supprimé.");
                _alertMessage = "Script supprimé.";
                await ParentPage.RefreshScripts();
                _selectedScript = new()
                {
                    Name = string.Empty
                };
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while deleting script.");
            ToastService.ShowToast2("Échec de la suppression du script", ToastType.Error);
        }

    }

    private async Task ScriptSelected(ScriptTreeNode viewModel)
    {
        EnsureUserSet();
        if (viewModel.Script is not null)
        {
            var result = await DataService.GetSavedScript(User.Id, viewModel.Script.Id);
            if (result.IsSuccess)
            {
                _selectedScript = result.Value;
                return;
            }
        }

        _selectedScript = new()
        {
            Name = string.Empty
        };
    }

    private void ToggleEnvironmentVarsShown()
    {
        if (string.IsNullOrWhiteSpace(_environmentVarsShowClass))
        {
            _environmentVarsShowClass = "show";
        }
        else
        {
            _environmentVarsShowClass = string.Empty;
        }
    }
    private void ToggleAlertOptionsShown()
    {
        if (string.IsNullOrWhiteSpace(_alertOptionsShowClass))
        {
            _alertOptionsShowClass = "show";
        }
        else
        {
            _alertOptionsShowClass = string.Empty;
        }
    }

    private void ShowAlertErrorHelp()
    {
        ModalService.ShowModal("Alertes d'erreur des scripts", new[]
        {
            "Un script est en erreur dès qu'il écrit quelque chose sur la sortie d'erreur. " +
            "Pour CMD, WinPS et Bash, un code de sortie différent de 0 compte aussi comme une erreur. " +
            "(Le shell PowerShell Core intégré n'utilise pas de code de sortie.)",

            "Pour PowerShell Core, tout texte écrit sur la sortie Warning ou Error déclenche une alerte. " +
            "Pour en déclencher une selon votre propre logique, utilisez " +
            "'Write-Error' ou 'Write-Warning' dans le script.",

            "Pour Windows PowerShell, utilisez 'Write-Error'.",

            "Pour Bash et CMD, sortez avec un code différent de 0."
        });
    }
}
