using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Remotely.Server.Components.ModalContents;
using Remotely.Server.Hubs;
using Remotely.Server.Models.Messages;
using Remotely.Server.Services;
using Remotely.Server.Services.Stores;
using Remotely.Shared.Entities;
using Remotely.Shared.Enums;
using Remotely.Shared.Models;
using Remotely.Shared.Utilities;

namespace Remotely.Server.Components.Devices;

public partial class Terminal : AuthComponentBase, IDisposable
{
    private string? _inputText;

    private string? _lastCompletionInput;
    private int _lastCursorIndex;
    private ScriptingShell _shell;

    private ElementReference _terminalInput;
    private string? _terminalOpenClass;
    private ElementReference _terminalWindow;

    [Inject]
    private ISelectedCardsStore CardStore { get; init; } = null!;

    [Inject]
    private ICircuitConnection CircuitConnection { get; init; } = null!;

    [Inject]
    private IDataService DataService { get; init; } = null!;

    private string InputText
    {
        get => _inputText ?? string.Empty;
        set
        {
            _inputText = value;
            if (TryMatchShellShortcuts())
            {
                _inputText = string.Empty;
            }
        }
    }

    [Inject]
    private IJsInterop JsInterop { get; init; } = null!;

    [Inject]
    private ILogger<Terminal> Logger { get; init; } = null!;

    [Inject]
    private IModalService ModalService { get; init; } = null!;

    private EventCallback<SavedScript> RunQuickScript =>
        EventCallback.Factory.Create<SavedScript>(this, async script =>
        {
            EnsureUserSet();

            var scriptRun = new ScriptRun
            {
                OrganizationID = User.OrganizationID,
                RunAt = Time.Now,
                SavedScriptId = script.Id,
                RunOnNextConnect = false,
                Initiator = User.UserName,
                InputType = ScriptInputType.OneTimeScript,
                Devices = DataService.GetDevices(CardStore.SelectedDevices)
            };

            await DataService.AddScriptRun(scriptRun);

            await CircuitConnection.RunScript(CardStore.SelectedDevices, script.Id, scriptRun.Id, ScriptInputType.OneTimeScript, false);

            ToastService.ShowToast($"Exécution du script sur {scriptRun.Devices.Count} appareil(s).");
        });

    [Inject]
    private ITerminalStore TerminalStore { get; init; } = null!;
    [Inject]
    private IToastService ToastService { get; init; } = null!;


    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            JsInterop.PreventTabOut(_terminalInput);
        }
        return base.OnAfterRenderAsync(firstRender);
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await Register<PowerShellCompletionsMessage, string>(
            CircuitConnection.ConnectionId, 
            HandlePowerShellCompletionsMessage);
        TerminalStore.TerminalLinesChanged += TerminalStore_TerminalLinesChanged;
    }

    private void ApplyCompletion(PwshCommandCompletion completion)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_lastCompletionInput))
            {
                return;
            }

            var match = completion.CompletionMatches[completion.CurrentMatchIndex];

            var replacementText = string.Concat(
                _lastCompletionInput[..completion.ReplacementIndex],
                match.CompletionText,
                _lastCompletionInput[(completion.ReplacementIndex + completion.ReplacementLength)..]);

            InputText = replacementText;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error appying command completion.");
        }
    }

    private async Task DisplayCompletions(List<PwshCompletionResult> completionMatches)
    {
        var deviceId = CardStore.SelectedDevices.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return;
        }

        var deviceResult = await DataService.GetDevice(deviceId);

        if (!deviceResult.IsSuccess)
        {
            ToastService.ShowToast2(deviceResult.Reason, Enums.ToastType.Warning);
            return;
        }

        TerminalStore.AddTerminalLine(
            $"Complétions pour {deviceResult.Value.DeviceName}",
            className: "font-weight-bold");

        foreach (var match in completionMatches)
        {
            TerminalStore.AddTerminalLine(match.CompletionText, className: "", title: match.ToolTip);
        }
    }

    private void EvaluateInputKeypress(KeyboardEventArgs ev)
    {
        if (ev.Key.Equals("Enter", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(InputText) || ev.ShiftKey)
            {
                return;
            }

            var devices = CardStore.SelectedDevices.ToArray();
            if (devices.Length == 0)
            {
                ToastService.ShowToast("Sélectionnez au moins un appareil.", classString: "bg-warning");
                return;
            }
            CircuitConnection.ExecuteCommandOnAgent(_shell, InputText, devices);
            TerminalStore.AddTerminalHistory(InputText);
            InputText = string.Empty;
        }
    }

    private async Task EvaluateKeyDown(KeyboardEventArgs ev)
    {
        if (!ev.Key.Equals("Tab", StringComparison.OrdinalIgnoreCase) &&
            !ev.Key.Equals("Shift", StringComparison.OrdinalIgnoreCase))
        {
            _lastCompletionInput = null;
        }


        if (ev.Key.Equals("ArrowUp", StringComparison.OrdinalIgnoreCase))
        {
            InputText = TerminalStore.GetTerminalHistory(false);
        }
        else if (ev.Key.Equals("ArrowDown", StringComparison.OrdinalIgnoreCase))
        {
            InputText = TerminalStore.GetTerminalHistory(true);
        }
        else if (ev.Key.Equals("Tab", StringComparison.OrdinalIgnoreCase))
        {
            if (_shell != ScriptingShell.PSCore && _shell != ScriptingShell.WinPS)
            {
                ToastService.ShowToast("La complétion par Tab nécessite PowerShell.", classString: "bg-warning");
                return;
            }

            if (!CardStore.SelectedDevices.Any())
            {
                ToastService.ShowToast("Aucun appareil sélectionné.", classString: "bg-warning");
                return;
            }

            if (string.IsNullOrWhiteSpace(InputText))
            {
                return;
            }

            await GetNextCompletion(!ev.ShiftKey);
        }
        else if (ev.CtrlKey && ev.Key.Equals(" ", StringComparison.OrdinalIgnoreCase))
        {
            if (!CardStore.SelectedDevices.Any())
            {
                return;
            }

            await ShowAllCompletions();
        }
        else if (ev.CtrlKey && ev.Key.Equals("q", StringComparison.OrdinalIgnoreCase))
        {
            TerminalStore.TerminalLines.Clear();
            TerminalStore.InvokeLinesChanged();
        }
    }

    private async Task GetNextCompletion(bool forward)
    {
        if (string.IsNullOrWhiteSpace(_lastCompletionInput))
        {
            _lastCompletionInput = InputText;
            _lastCursorIndex = await JsInterop.GetCursorIndex(_terminalInput);
        }

        await CircuitConnection.GetPowerShellCompletions(_lastCompletionInput, _lastCursorIndex, CompletionIntent.NextResult, forward);
    }

    private async Task HandlePowerShellCompletionsMessage(object subscriber, PowerShellCompletionsMessage message)
    {
        var completion = message.Completion;
        var intent = message.Intent;

        switch (intent)
        {
            case CompletionIntent.ShowAll:
                await DisplayCompletions(completion.CompletionMatches);
                break;
            case CompletionIntent.NextResult:
                ApplyCompletion(completion);
                break;
            default:
                break;
        }
        TerminalStore.InvokeLinesChanged();
    }

    private async Task ShowAllCompletions()
    {
        if (string.IsNullOrWhiteSpace(_lastCompletionInput))
        {
            _lastCompletionInput = InputText;
            _lastCursorIndex = await JsInterop.GetCursorIndex(_terminalInput);
        }

        await CircuitConnection.GetPowerShellCompletions(_lastCompletionInput, _lastCursorIndex, CompletionIntent.ShowAll, false);
    }

    private async Task ShowQuickScripts()
    {
        EnsureUserSet();

        var quickScripts = await DataService.GetQuickScripts(User.Id);
        if (quickScripts.Count == 0)
        {
            ToastService.ShowToast("Aucun script rapide enregistré.", classString: "bg-warning");
            return;
        }

        if (!CardStore.SelectedDevices.Any())
        {
            ToastService.ShowToast("Sélectionnez au moins un appareil.", classString: "bg-warning");
            return;
        }


        void showModal(RenderTreeBuilder builder)
        {
            builder.OpenComponent<QuickScriptsSelector>(0);
            builder.AddAttribute(1, "QuickScripts", quickScripts);
            builder.AddAttribute(2, "OnRunClicked", RunQuickScript);
            builder.CloseComponent();
        }

        await ModalService.ShowModal("Scripts rapides", showModal);
    }

    private void ShowTerminalHelp()
    {
        ModalService.ShowModal("Aide du terminal",
        [
            "Les commandes saisies s'exécutent sur tous les appareils sélectionnés.",

            "La complétion est disponible pour PowerShell Core (PSCore) et Windows PowerShell (WinPS). Tab et Maj + Tab " +
            "font défiler les propositions. Ctrl + Espace les affiche toutes.",

            "Si plusieurs appareils sont sélectionnés, la complétion des chemins de fichiers et de dossiers " +
            "utilise le système de fichiers du premier.",

            "PowerShell Core est multiplateforme et disponible sur tous les systèmes. Bash est disponible " +
            "sous Windows si WSL (sous-système Windows pour Linux) est installé.",

            "Les flèches haut et bas parcourent l'historique des commandes. Ctrl + Q vide la fenêtre de sortie.",

            "Remarque : la première commande ou complétion PS Core prend quelques secondes, le temps que le service " +
            "démarre sur l'appareil distant."
        ]);
    }

    private async void TerminalStore_TerminalLinesChanged(object? sender, EventArgs e)
    {
        await InvokeAsync(StateHasChanged);
        JsInterop.ScrollToEnd(_terminalWindow);
    }
    private void ToggleTerminalOpen()
    {
        if (string.IsNullOrWhiteSpace(_terminalOpenClass))
        {
            _terminalOpenClass = "open";
        }
        else
        {
            _terminalOpenClass = string.Empty;
        }
    }
    private bool TryMatchShellShortcuts()
    {
        EnsureUserSet();

        var currentText = InputText?.Trim()?.ToLower();

        if (string.IsNullOrWhiteSpace(currentText))
        {
            return false;
        }

        if (User.UserOptions is null)
        {
            return false;
        }

        if (currentText.Equals(User.UserOptions.CommandModeShortcutPSCore.ToLower()))
        {
            _shell = ScriptingShell.PSCore;
            return true;
        }
        else if (currentText.Equals(User.UserOptions.CommandModeShortcutCMD.ToLower()))
        {
            _shell = ScriptingShell.CMD;
            return true;
        }
        else if (currentText.Equals(User.UserOptions.CommandModeShortcutWinPS.ToLower()))
        {
            _shell = ScriptingShell.WinPS;
            return true;
        }
        else if (currentText.Equals(User.UserOptions.CommandModeShortcutBash.ToLower()))
        {
            _shell = ScriptingShell.Bash;
            return true;
        }
        return false;
    }
}
