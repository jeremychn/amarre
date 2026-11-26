using Remotely.Desktop.Shared.Abstractions;
using Microsoft.Extensions.Logging;
using Remotely.Shared.Models;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;

namespace Remotely.Desktop.Shared.Services;

public interface IChatHostService
{
    Task StartChat(string requesterID, string organizationName);
}
public class ChatHostService : IChatHostService
{
    private readonly IChatUiService _chatUiService;
    private readonly ILogger<ChatHostService> _logger;

    private NamedPipeServerStream? _namedPipeStream;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    public ChatHostService(IChatUiService chatUiService, ILogger<ChatHostService> logger)
    {
        _chatUiService = chatUiService;
        _logger = logger;
    }

    public async Task StartChat(string pipeName, string organizationName)
    {
        _namedPipeStream = CreatePipe(pipeName);
        _writer = new StreamWriter(_namedPipeStream);
        _reader = new StreamReader(_namedPipeStream);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            _logger.LogInformation("Waiting for chat client to connect via pipe {name}.", pipeName);
            await _namedPipeStream.WaitForConnectionAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("A chat session was attempted, but the client failed to connect in time.");
            Environment.Exit(0);
        }

        _logger.LogInformation("Chat client connected.");
        _chatUiService.ChatWindowClosed += OnChatWindowClosed;

        _chatUiService.ShowChatWindow(organizationName, _writer);

        _ = Task.Run(ReadFromStream);
    }

    // Une seule instance, créée par ce processus (échec si le nom est déjà pris) ; sous Windows,
    // réservée à ce compte et à SYSTEM (l'agent). ponytail: sous Linux, la socket reste dans /tmp avec
    // les droits par défaut (l'agent root doit pouvoir s'y connecter) ; son nom n'est plus annoncé.
    private static NamedPipeServerStream CreatePipe(string pipeName)
    {
        const PipeOptions options = PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance;
        if (!OperatingSystem.IsWindows())
        {
            return new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        }

        var security = new PipeSecurity();
        using var identity = WindowsIdentity.GetCurrent();
        foreach (var sid in new[] { identity.User!, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null) })
        {
            security.AddAccessRule(new PipeAccessRule(sid, PipeAccessRights.ReadWrite, AccessControlType.Allow));
        }
        return NamedPipeServerStreamAcl.Create(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, options, 0, 0, security);
    }

    private void OnChatWindowClosed(object? sender, EventArgs e)
    {
        try
        {
            _namedPipeStream?.Dispose();
        }
        catch { }
    }

    private async Task ReadFromStream()
    {
        while (_namedPipeStream?.IsConnected == true)
        {
            try
            {
                var messageJson = await _reader!.ReadLineAsync();
                if (!string.IsNullOrWhiteSpace(messageJson))
                {
                    var chatMessage = JsonSerializer.Deserialize<ChatMessage>(messageJson);
                    if (chatMessage is null)
                    {
                        _logger.LogWarning("Deserialized message was null.  Value: {value}", messageJson);
                        continue;
                    }
                    await _chatUiService.ReceiveChat(chatMessage);

                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while reading from chat IPC stream.");
            }
        }
    }
}
