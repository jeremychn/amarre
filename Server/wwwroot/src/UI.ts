import { ViewerApp } from "./App.js";
import { ConvertUInt8ArrayToBase64 } from "./Utilities.js";
import { WindowsSession } from "./Models/WindowsSession.js";
import { WindowsSessionType } from "./Enums/WindowsSessionType.js";
import { RemoteControlMode } from "./Enums/RemoteControlMode.js";
import { SessionMetricsDto, DisplayLayoutDto } from "./Interfaces/Dtos.js";

export var AudioButton = document.getElementById("audioButton") as HTMLButtonElement;
export var MenuButton = document.getElementById("menuButton") as HTMLButtonElement;
export var MenuFrame = document.getElementById("menuFrame") as HTMLDivElement;
export var SessionIDInput = document.getElementById("sessionIDInput") as HTMLInputElement;
export var ConnectButton = document.getElementById("connectButton") as HTMLButtonElement;
export var RequesterNameInput = document.getElementById("nameInput") as HTMLInputElement;
export var StatusMessage = document.getElementById("statusMessage") as HTMLDivElement;
export var ScreenViewer = document.getElementById("screenViewer") as HTMLCanvasElement;
export var ScreenViewerWrapper = document.getElementById("screenViewerWrapper") as HTMLDivElement;
export var Screen2DContext = ScreenViewer ? ScreenViewer.getContext("2d") : null;
export var PopupMenus = document.querySelectorAll(".popup-menu");
export var ConnectBox = document.getElementById("connectBox") as HTMLDivElement;
export var ConnectHeader = document.getElementById("connectHeader") as HTMLHeadingElement;
export var ConnectForm = document.getElementById("connectForm") as HTMLFormElement;
export var ScreenSelectMenu = document.getElementById("screenSelectMenu") as HTMLDivElement;
export var ChangeScreenButton = document.getElementById("changeScreenButton") as HTMLButtonElement;
export var FitToScreenButton = document.getElementById("fitToScreenButton") as HTMLButtonElement;
export var BlockInputButton = document.getElementById("blockInputButton") as HTMLButtonElement;
export var DisconnectButton = document.getElementById("disconnectButton") as HTMLButtonElement;
export var FileTransferInput = document.getElementById("fileTransferInput") as HTMLInputElement;
export var FileTransferProgress = document.getElementById("fileTransferProgress") as HTMLProgressElement;
export var FileTransferNameSpan = document.getElementById("fileTransferNameSpan") as HTMLSpanElement;
export var KeyboardButton = document.getElementById("keyboardButton") as HTMLButtonElement;
export var InviteButton = document.getElementById("inviteButton") as HTMLButtonElement;
export var FileTransferButton = document.getElementById("fileTransferButton") as HTMLButtonElement;
export var FileTransferMenu = document.getElementById("fileTransferMenu") as HTMLDivElement;
export var FileUploadButtton = document.getElementById("fileUploadButton") as HTMLButtonElement;
export var FileDownloadButton = document.getElementById("fileDownloadButton") as HTMLButtonElement;
export var CtrlAltDelButton = document.getElementById("ctrlAltDelButton") as HTMLButtonElement;
export var TouchKeyboardInput = document.getElementById("touchKeyboardInput") as HTMLTextAreaElement;
export var ClipboardTransferMenu = document.getElementById("clipboardTransferMenu") as HTMLDivElement;
export var ClipboardTransferButton = document.getElementById("clipboardTransferButton") as HTMLButtonElement;
export var TypeClipboardButton = document.getElementById("typeClipboardButton") as HTMLButtonElement;
export var ConnectionP2PIcon = document.getElementById("connectionP2PIcon") as HTMLElement;
export var ConnectionRelayedIcon = document.getElementById("connectionRelayedIcon") as HTMLElement;
export var WindowsSessionSelect = document.getElementById("windowsSessionSelect") as HTMLSelectElement;
export var ViewOnlyButton = document.getElementById("viewOnlyButton") as HTMLButtonElement;
export var FullScreenButton = document.getElementById("fullScreenButton") as HTMLButtonElement;
export var ToastsWrapper = document.getElementById("toastsWrapper") as HTMLDivElement;
export var MbpsDiv = document.getElementById("mbpsDiv") as HTMLDivElement;
export var FpsDiv = document.getElementById("fpsDiv") as HTMLDivElement;
export var LatencyDiv = document.getElementById("latencyDiv") as HTMLDivElement;
export var GpuDiv = document.getElementById("gpuAcceleratedDiv") as HTMLDivElement;
export var WorkAreaGrid = document.getElementById("workAreaGrid") as HTMLDivElement;
export var BackgroundLayers = document.getElementById("backgroundLayers") as HTMLDivElement;
export var ExtrasMenu = document.getElementById("extrasMenu") as HTMLDivElement;
export var ExtrasMenuButton = document.getElementById("extrasMenuButton") as HTMLButtonElement;
export var WindowsSessionMenu = document.getElementById("windowsSessionMenu") as HTMLDivElement;
export var WindowsSessionMenuButton = document.getElementById("windowsSessionMenuButton") as HTMLButtonElement;
export var MetricsButton = document.getElementById("metricsButton") as HTMLButtonElement;
export var MetricsFrame = document.getElementById("metricsFrame") as HTMLDivElement;
export var BetaPillPullDown = document.getElementById("betaPillPullDown") as HTMLDivElement;
export var QualityModeButton = document.getElementById("qualityModeButton") as HTMLButtonElement;
export var QualityModeMenu = document.getElementById("qualityModeMenu") as HTMLDivElement;
export var QualityModeLabel = document.getElementById("qualityModeLabel") as HTMLSpanElement;
export var QualityAutoButton = document.getElementById("qualityAutoButton") as HTMLButtonElement;
export var QualityPerformanceButton = document.getElementById("qualityPerformanceButton") as HTMLButtonElement;
export var QualityHighButton = document.getElementById("qualityHighButton") as HTMLButtonElement;

const _thumbnailMap = new Map<string, string>();

export function UpdateThumbnail(displayName: string, imageBytes: Uint8Array): void {
    var base64 = ConvertUInt8ArrayToBase64(imageBytes);
    _thumbnailMap.set(displayName, `data:image/jpeg;base64,${base64}`);

    var img = document.querySelector(`[data-display="${CSS.escape(displayName)}"] img`) as HTMLImageElement;
    if (img) {
        img.src = _thumbnailMap.get(displayName);
    }
}

export function CloseAllPopupMenus(exceptMenuId: string) {
    PopupMenus.forEach(x => {
        if (x.id != exceptMenuId) {
            x.classList.remove("open");
        }
    })
}

export function Prompt(promptMessage: string): Promise<string> {
    return new Promise((resolve, reject) => {
        var modalDiv = document.createElement("div");
        modalDiv.classList.add("modal-prompt");

        var messageDiv = document.createElement("div");
        messageDiv.textContent = promptMessage;

        var responseInput = document.createElement("input");

        var buttonsDiv = document.createElement("div");
        buttonsDiv.classList.add("buttons-footer");

        var cancelButton = document.createElement("button");
        cancelButton.innerHTML = "Cancel";

        var okButton = document.createElement("button");
        okButton.innerHTML = "OK";

        buttonsDiv.appendChild(okButton);
        buttonsDiv.appendChild(cancelButton);
        modalDiv.appendChild(messageDiv);
        modalDiv.appendChild(responseInput);
        modalDiv.appendChild(buttonsDiv);

        document.body.appendChild(modalDiv);

        okButton.onclick = () => {
            modalDiv.remove();
            resolve(responseInput.value);
        }

        cancelButton.onclick = () => {
            modalDiv.remove();
            resolve(null);
        }
    });
}


export function SetScreenSize(width: number, height: number) {
    ScreenViewer.width = width;
    ScreenViewer.height = height;
    Screen2DContext.clearRect(0, 0, width, height);
}

export function SetStatusMessage(message: string) {
    StatusMessage.innerText = message;
}

export function ShowToast(message: string) {
    var messageDiv = document.createElement("div");
    messageDiv.classList.add("toast-message");
    messageDiv.textContent = message;
    ToastsWrapper.appendChild(messageDiv);
    window.setTimeout(() => {
        messageDiv.remove();
    }, 5000);
}


export function ToggleConnectUI(shown: boolean) {
    if (shown) {
        ConnectButton.innerText = "Se connecter";
        Screen2DContext.clearRect(0, 0, ScreenViewer.width, ScreenViewer.height);
        ScreenViewerWrapper.setAttribute("hidden", "hidden");
        if (ViewerApp.Mode == RemoteControlMode.Attended) {
            ConnectBox.style.removeProperty("display");
            ConnectHeader.style.removeProperty("display");
        }
        BlockInputButton.classList.remove("toggled");
        AudioButton.classList.remove("toggled");
        WorkAreaGrid.style.display = "none";
        BackgroundLayers.classList.remove("d-none");
        CloseAllPopupMenus(null);
    }
    else {
        ConnectBox.style.display = "none";
        ConnectHeader.style.display = "none";
        ScreenViewerWrapper.removeAttribute("hidden");
        StatusMessage.innerHTML = "";
        WorkAreaGrid.style.removeProperty("display");
        BackgroundLayers.classList.add("d-none");
    }

    ConnectButton.disabled = !ViewerApp.RequesterName || !ViewerApp.SessionId;
}

export function UpdateCursor(imageBytes: Uint8Array, hotSpotX: number, hotSpotY: number, cssOverride: string) {
    if (cssOverride) {
        ScreenViewer.style.cursor = cssOverride;
    }
    else if (imageBytes.byteLength == 0) {
        ScreenViewer.style.cursor = "default";
    }
    else {
        var base64 = ConvertUInt8ArrayToBase64(imageBytes);
        ScreenViewer.style.cursor = `url('data:image/png;base64,${base64}') ${hotSpotX} ${hotSpotY}, default`;
    }
}

export function UpdateDisplays(selectedDisplay: string, displayNames: string[], displayLayouts?: DisplayLayoutDto[]) {
    ScreenSelectMenu.innerHTML = "";

    if (displayLayouts && displayLayouts.length > 1) {
        var layoutContainer = RenderMonitorLayout(selectedDisplay, displayLayouts);
        ScreenSelectMenu.appendChild(layoutContainer);
    }

    for (let i = 0; i < displayNames.length; i++) {
        var button = document.createElement("button");
        button.setAttribute("data-display", displayNames[i]);

        var img = document.createElement("img");
        img.className = "monitor-thumbnail";
        img.alt = `Écran ${i + 1} (${displayNames[i]})`;
        var cachedSrc = _thumbnailMap.get(displayNames[i]);
        if (cachedSrc) {
            img.src = cachedSrc;
        }

        var label = document.createElement("span");
        label.innerText = `Écran ${i + 1}`;

        if (displayNames[i] == selectedDisplay) {
            button.classList.add("toggled");
        }
        button.appendChild(img);
        button.appendChild(label);
        ScreenSelectMenu.appendChild(button);
        button.onclick = (ev: MouseEvent) => {
            ViewerApp.MessageSender.SendSelectScreen(displayNames[i], `Écran ${i + 1}`);
            ScreenSelectMenu.classList.toggle("open");
            ScreenSelectMenu.querySelectorAll("button").forEach(btn => {
                btn.classList.remove("toggled");
            });
            (ev.currentTarget as HTMLButtonElement).classList.add("toggled");
        };
    }
}

export function RenderMonitorLayout(selectedDisplay: string, layouts: DisplayLayoutDto[]): HTMLElement {
    var container = document.createElement("div");
    container.className = "monitor-layout-diagram";

    var minX = Math.min(...layouts.map(l => l.X));
    var minY = Math.min(...layouts.map(l => l.Y));
    var maxX = Math.max(...layouts.map(l => l.X + l.Width));
    var maxY = Math.max(...layouts.map(l => l.Y + l.Height));
    var totalW = maxX - minX || 1;
    var totalH = maxY - minY || 1;

    const svgW = 200;
    const svgH = Math.max(40, Math.round(svgW * totalH / totalW));
    const padding = 4;

    var svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
    svg.setAttribute("width", String(svgW + padding * 2));
    svg.setAttribute("height", String(svgH + padding * 2));
    svg.setAttribute("viewBox", `0 0 ${svgW + padding * 2} ${svgH + padding * 2}`);
    svg.style.display = "block";

    for (let li = 0; li < layouts.length; li++) {
        var layout = layouts[li];
        var rx = padding + Math.round((layout.X - minX) / totalW * svgW);
        var ry = padding + Math.round((layout.Y - minY) / totalH * svgH);
        var rw = Math.max(1, Math.round(layout.Width / totalW * svgW) - 2);
        var rh = Math.max(1, Math.round(layout.Height / totalH * svgH) - 2);

        var rect = document.createElementNS("http://www.w3.org/2000/svg", "rect");
        rect.setAttribute("x", String(rx));
        rect.setAttribute("y", String(ry));
        rect.setAttribute("width", String(rw));
        rect.setAttribute("height", String(rh));
        rect.setAttribute("rx", "3");
        rect.classList.add("monitor-layout-rect");
        if (layout.DisplayName === selectedDisplay) {
            rect.classList.add("monitor-layout-rect-selected");
        }

        var displayName = layout.DisplayName;
        var monitorLabel = `Écran ${li + 1}`;
        rect.addEventListener("click", () => {
            ViewerApp.MessageSender.SendSelectScreen(displayName, monitorLabel);
            ScreenSelectMenu.classList.toggle("open");
        });

        svg.appendChild(rect);
    }

    container.appendChild(svg);
    return container;
}


export function UpdateMetrics(metricsDto: SessionMetricsDto) {
    FpsDiv.innerHTML = metricsDto.Fps.toFixed(0);
    MbpsDiv.innerHTML = metricsDto.Mbps.toFixed(2);
    LatencyDiv.innerHTML = `${metricsDto.RoundTripLatency.toFixed(2)}ms`;
    GpuDiv.innerHTML = metricsDto.IsGpuAccelerated ? "Enabled" : "Unavailable";
}

export function UpdateWindowsSessions(windowsSessions: Array<WindowsSession>) {
    while (WindowsSessionSelect.options.length > 0) {
        WindowsSessionSelect.options.remove(0);
    }

    WindowsSessionSelect.options.add(document.createElement("option"));

    windowsSessions.forEach(x => {
        var sessionType = "";

        if (typeof x.Type == "number") {
            sessionType = x.Type == WindowsSessionType.Console ? "Console" : "RDP";
        }
        else {
            sessionType = x.Type;
        }

        var option = document.createElement("option");
        option.value = String(x.ID);
        option.text = `${sessionType} (ID: ${x.ID} | User: ${x.Username})`;
        option.title = `${sessionType} Session (ID: ${x.ID} | User: ${x.Username})`;
        WindowsSessionSelect.options.add(option);
    });
}
