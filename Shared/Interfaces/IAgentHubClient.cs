using Remotely.Shared.Enums;

namespace Remotely.Shared.Interfaces;
public interface IAgentHubClient
{
    /// <summary>Un autre poste est déjà connecté avec ce DeviceID (poste cloné) : l'agent doit en tirer un nouveau.</summary>
    Task DeviceIdConflict();

    Task ChangeWindowsSession(
        string viewerConnectionId,
        string sessionId,
        string accessKey,
        string userConnectionId,
        string requesterName,
        string orgName,
        string orgId,
        int targetSessionId);

    Task SendChatMessage(
        string senderName, 
        string message, 
        string orgName, 
        string orgId, 
        bool disconnected, 
        string senderConnectionId);

    Task InvokeCtrlAltDel();

    Task DeleteLogs();

    Task ExecuteCommand(
        ScriptingShell shell, 
        string command, 
        string authToken, 
        string senderUsername, 
        string senderConnectionId);

    Task ExecuteCommandFromApi(ScriptingShell shell,
            string authToken,
            string requestID,
            string command,
            string senderUsername);

    Task GetLogs(string senderConnectionId);

    Task GetPowerShellCompletions(
        string inputText, 
        int currentIndex, 
        CompletionIntent intent, 
        bool? forward, 
        string senderConnectionId);

    Task ReinstallAgent();

    Task UninstallAgent();

    Task RemoteControl(
        Guid sessionId, 
        string accessKey, 
        string userConnectionId, 
        string requesterName, 
        string orgName, 
        string orgId);

    Task RestartScreenCaster(
        string[] viewerIds, 
        string sessionId, 
        string accessKey, 
        string userConnectionId, 
        string requesterName, 
        string orgName, 
        string orgId);

    Task RunScript(
        Guid savedScriptId, 
        int scriptRunId, 
        string initiator, 
        ScriptInputType scriptInputType, 
        string authToken);

    Task TransferFileFromBrowserToAgent(
        string transferId, 
        string[] fileIds, 
        string requesterId, 
        string expiringToken);

    Task TriggerHeartbeat();

    Task WakeDevice(string macAddress);
}
