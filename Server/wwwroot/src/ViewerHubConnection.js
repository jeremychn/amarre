import * as UI from "./UI.js";
import { ViewerApp } from "./App.js";
import { RemoteControlMode } from "./Enums/RemoteControlMode.js";
import { ShowToast } from "./UI.js";
import { ChunkDto } from "./DtoChunker.js";
import { ProcessStream } from "./CaptureProcessor.js";
import { HubConnectionState } from "./Enums/HubConnectionState.js";
import { StreamingState } from "./Models/StreamingState.js";
import { GetSettings, SetSettings } from "./SettingsService.js";
const MsgPack = window["MessagePack"];
var signalR = window["signalR"];
export class ViewerHubConnection {
    constructor() {
        this.PartialCaptureFrames = [];
    }
    Connect() {
        if (this.Connection) {
            this.Connection.stop();
        }
        // Le hub vérifie lui-même l'accès : on lui transmet l'OTP de la page s'il y en a un.
        const otp = new URLSearchParams(location.search).get("otp");
        this.Connection = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/viewer" + (otp ? `?otp=${encodeURIComponent(otp)}` : ""))
            .withHubProtocol(new signalR.protocols.msgpack.MessagePackHubProtocol())
            .configureLogging(signalR.LogLevel.Information)
            .build();
        this.ApplyMessageHandlers(this.Connection);
        this.Connection.start().then(async () => {
            this.SendScreenCastRequestToDevice();
        }).catch(err => {
            console.error(err.toString());
            console.log("Connexion fermée.");
            UI.StatusMessage.textContent = `Connection error: ${err.message}`;
            UI.ToggleConnectUI(true);
        });
        this.Connection.onclose(() => {
            if (!UI.StatusMessage.innerText) {
                UI.SetStatusMessage("Connexion fermée.");
            }
            UI.ToggleConnectUI(true);
        });
        ViewerApp.ClipboardWatcher.WatchClipboard();
    }
    async ChangeWindowsSession(sessionID) {
        if (ViewerApp.Mode == RemoteControlMode.Unattended) {
            await this.Connection.invoke("ChangeWindowsSession", sessionID);
        }
    }
    async GetRemoteControlViewerOptions() {
        const settings = GetSettings();
        try {
            settings.ViewerOptions = await this.Connection.invoke("GetViewerOptions");
            SetSettings(settings);
            return settings.ViewerOptions;
        }
        catch (e) {
            console.error("Error while getting viewer options from server.", e);
            return settings.ViewerOptions;
        }
    }
    async InvokeCtrlAltDel() {
        var _a;
        if (((_a = this.Connection) === null || _a === void 0 ? void 0 : _a.state) != HubConnectionState.Connected) {
            return;
        }
        await this.Connection.invoke("InvokeCtrlAltDel");
    }
    async SendDtoToClient(dto, type) {
        var _a;
        if (((_a = this.Connection) === null || _a === void 0 ? void 0 : _a.state) != HubConnectionState.Connected) {
            return;
        }
        let chunks = ChunkDto(dto, type);
        for (var i = 0; i < chunks.length; i++) {
            const chunk = MsgPack.encode(chunks[i]);
            await this.Connection.invoke("SendDtoToClient", chunk);
        }
    }
    // Subject is an interface that comes from the SignalR library.
    // But we can't use the TypeScript library like we would in
    // React/Vue, so we have to use "any" here.  This won't be an
    // issue when we rewrite the front-end.
    async SendRecordingChunks(subject) {
        await this.Connection.send("StoreSessionRecording", subject);
    }
    async SendScreenCastRequestToDevice() {
        const viewerOptions = await this.GetRemoteControlViewerOptions();
        const result = await this.Connection.invoke("SendScreenCastRequestToDevice", ViewerApp.SessionId, ViewerApp.AccessKey, ViewerApp.RequesterName);
        if (!result.IsSuccess) {
            this.Connection.stop();
            UI.SetStatusMessage(result.Reason);
            return;
        }
        const streamingState = new StreamingState();
        ProcessStream(streamingState);
        if (viewerOptions.ShouldRecordSession) {
            ViewerApp.SessionRecorder.Start();
        }
        this.Connection.stream("GetDesktopStream")
            .subscribe({
            next: (chunk) => {
                streamingState.ReceivedChunks.push(chunk);
            },
            complete: () => {
                streamingState.StreamEnded = true;
                if (!UI.StatusMessage.innerText) {
                    UI.SetStatusMessage("Flux terminé.");
                }
                ViewerApp.SessionRecorder.Stop();
                UI.ToggleConnectUI(true);
            },
            error: (err) => {
                console.warn(err);
                streamingState.StreamEnded = true;
                if (!UI.StatusMessage.innerText) {
                    UI.SetStatusMessage("Flux terminé.");
                }
                ViewerApp.SessionRecorder.Stop();
                UI.ToggleConnectUI(true);
            },
        });
    }
    ApplyMessageHandlers(hubConnection) {
        hubConnection.on("SendDtoToViewer", async (dto) => {
            await ViewerApp.DtoMessageHandler.ParseBinaryMessage(dto);
        });
        hubConnection.on("ConnectionFailed", () => {
            UI.ConnectButton.removeAttribute("disabled");
            UI.ConnectButton.innerText = "Se connecter";
            UI.SetStatusMessage("Connexion échouée ou refusée.");
            ShowToast("Connexion échouée. Reconnectez-vous.");
            this.Connection.stop();
        });
        hubConnection.on("ReconnectFailed", () => {
            UI.ConnectButton.removeAttribute("disabled");
            UI.ConnectButton.innerText = "Se connecter";
            UI.SetStatusMessage("Reconnexion impossible.");
            ShowToast("Reconnexion impossible.");
            this.Connection.stop();
        });
        hubConnection.on("ConnectionRequestDenied", () => {
            UI.ConnectButton.innerText = "Se connecter";
            this.Connection.stop();
            UI.SetStatusMessage("Demande de connexion refusée.");
            ShowToast("Demande de connexion refusée.");
        });
        hubConnection.on("ViewerRemoved", () => {
            UI.ConnectButton.removeAttribute("disabled");
            UI.ConnectButton.innerText = "Se connecter";
            UI.SetStatusMessage("La session a été arrêtée par votre correspondant.");
            ShowToast("Session terminée");
            this.Connection.stop();
        });
        hubConnection.on("ScreenCasterDisconnected", () => {
            UI.SetStatusMessage("Le poste distant s'est déconnecté.");
            this.Connection.stop();
        });
        hubConnection.on("RelaunchedScreenCasterReady", (newSessionId, newAccessKey) => {
            const newUrl = `${location.origin}${location.pathname}` +
                `?mode=Unattended&sessionId=${newSessionId}&accessKey=${newAccessKey}&viewOnly=${ViewerApp.ViewOnlyMode}`;
            window.history.pushState(null, "", newUrl);
            ViewerApp.SessionId = newSessionId;
            ViewerApp.AccessKey = newAccessKey;
            this.SendScreenCastRequestToDevice();
        });
        hubConnection.on("Reconnecting", () => {
            UI.SetStatusMessage("Reconnexion…");
            ShowToast("Reconnexion…");
        });
        hubConnection.on("CursorChange", (cursor) => {
            UI.UpdateCursor(cursor.ImageBytes, cursor.HotSpot.X, cursor.HotSpot.Y, cursor.CssOverride);
        });
        hubConnection.on("ShowMessage", (message) => {
            ShowToast(message);
            UI.SetStatusMessage(message);
        });
        hubConnection.on("WindowsSessions", (windowsSessions) => {
            UI.UpdateWindowsSessions(windowsSessions);
        });
        hubConnection.on("PingViewer", () => "Pong");
    }
}
//# sourceMappingURL=ViewerHubConnection.js.map