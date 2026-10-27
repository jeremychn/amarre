import { AudioButton, ChangeScreenButton, ScreenSelectMenu, ClipboardTransferButton, ClipboardTransferMenu, TypeClipboardButton, ConnectButton, CtrlAltDelButton, DisconnectButton, FileTransferButton, FileTransferInput, FitToScreenButton, ScreenViewer, BlockInputButton, InviteButton, KeyboardButton, TouchKeyboardInput, MenuFrame, MenuButton, ScreenViewerWrapper, WindowsSessionSelect, FileTransferMenu, FileUploadButtton, FileDownloadButton, ViewOnlyButton, FullScreenButton, RequesterNameInput, SessionIDInput, ConnectForm, CloseAllPopupMenus, ExtrasMenu, ExtrasMenuButton, WindowsSessionMenuButton, WindowsSessionMenu, MetricsButton, MetricsFrame, SetStatusMessage, BetaPillPullDown, QualityModeButton, QualityModeMenu, QualityModeLabel, QualityAutoButton, QualityPerformanceButton, QualityHighButton, } from "./UI.js";
import { Sound } from "./Sound.js";
import { ViewerApp } from "./App.js";
import { UploadFiles } from "./FileTransferService.js";
import { RemoteControlMode } from "./Enums/RemoteControlMode.js";
import { RemoteControlQualityMode } from "./Enums/RemoteControlQualityMode.js";
import { GetDistanceBetween } from "./Utilities.js";
import { ShowToast } from "./UI.js";
var isDragging;
var currentPointerDevice;
var currentTouchCount;
var isPinchZooming;
var startPinchPoint1;
var startPinchPoint2;
var lastPinchDistance;
var longPressStarted;
var longPressStartOffsetX;
var longPressStartOffsetY;
var lastPinchCenterX;
var lastPinchCenterY;
var isScrolling;
var lastScrollTime;
var lastScrollTouchY1;
var lastScrollTouchY2;
var lastPointerMove = Date.now();
var pointerMoveTimeout = -1;
export function ApplyInputHandlers() {
    AudioButton.addEventListener("click", async (ev) => {
        AudioButton.classList.toggle("toggled");
        var toggleOn = AudioButton.classList.contains("toggled");
        if (toggleOn) {
            Sound.Init();
        }
        await ViewerApp.MessageSender.SendToggleAudio(toggleOn);
    });
    ChangeScreenButton.addEventListener("click", (ev) => {
        ev.stopPropagation();
        CloseAllPopupMenus(ScreenSelectMenu.id);
        // This could be put into a re-usable "openPopup" function that takes
        // "target element" and "placement" as inputs, but all this is
        // temporary, so I don't think it's worth the time.
        const x = ChangeScreenButton.getBoundingClientRect().left;
        const left = `${x.toFixed(0)}px`;
        const y = ChangeScreenButton.getBoundingClientRect().bottom;
        const top = `${y.toFixed(0)}px`;
        ScreenSelectMenu.style.left = left;
        ScreenSelectMenu.style.top = top;
        ScreenSelectMenu.classList.toggle("open");
        window.addEventListener("click", () => {
            CloseAllPopupMenus(null);
        }, { once: true });
    });
    ClipboardTransferButton.addEventListener("click", (ev) => {
        ev.stopPropagation();
        CloseAllPopupMenus(ClipboardTransferMenu.id);
        const x = ClipboardTransferButton.getBoundingClientRect().left;
        const left = `${x.toFixed(0)}px`;
        const y = ClipboardTransferButton.getBoundingClientRect().bottom;
        const top = `${y.toFixed(0)}px`;
        ClipboardTransferMenu.style.left = left;
        ClipboardTransferMenu.style.top = top;
        ClipboardTransferMenu.classList.toggle("open");
        window.addEventListener("click", () => {
            CloseAllPopupMenus(null);
        }, { once: true });
    });
    ViewOnlyButton.addEventListener("click", () => {
        ViewOnlyButton.classList.toggle("toggled");
        ViewerApp.ViewOnlyMode = ViewOnlyButton.classList.contains("toggled");
    });
    TypeClipboardButton.addEventListener("click", (ev) => {
        var _a;
        if (!location.protocol.includes("https") &&
            !location.origin.includes("localhost")) {
            alert("Le presse-papiers ne fonctionne qu'en contexte sécurisé (HTTPS ou localhost).");
            return;
        }
        if (!((_a = navigator.clipboard) === null || _a === void 0 ? void 0 : _a.readText)) {
            alert("Ce navigateur ne permet pas l'accès au presse-papiers.");
            return;
        }
        if (ViewerApp.ViewOnlyMode) {
            alert("Mode affichage seul activé.");
            return;
        }
        navigator.clipboard.readText().then(async (text) => {
            await ViewerApp.MessageSender.SendTextTransfer(text, true);
            ShowToast("Presse-papiers envoyé.");
        }, (reason) => {
            alert("Lecture du presse-papiers impossible. Vérifiez les autorisations du navigateur.");
            console.log("Unable to read clipboard.  Reason: " + reason);
        });
    });
    ConnectButton.addEventListener("click", () => {
        if (!ConnectForm.checkValidity()) {
            return;
        }
        ViewerApp.ConnectToClient();
    });
    CtrlAltDelButton.addEventListener("click", async () => {
        if (ViewerApp.ViewOnlyMode) {
            alert("Mode affichage seul activé.");
            return;
        }
        CloseAllPopupMenus(null);
        await ViewerApp.MessageSender.SendCtrlAltDel();
    });
    DisconnectButton.addEventListener("click", (ev) => {
        ConnectButton.removeAttribute("disabled");
        ConnectButton.innerText = "Se connecter";
        SetStatusMessage("Connexion fermée.");
        ViewerApp.ViewerHubConnection.Connection.stop();
        if (location.search.includes("fromApi=true")) {
            window.close();
        }
    });
    [SessionIDInput, RequesterNameInput].forEach((x) => {
        x.addEventListener("keypress", (ev) => {
            if (!SessionIDInput.value || !RequesterNameInput.value) {
                return;
            }
            if (ev.key.toLowerCase() == "enter") {
                ViewerApp.ConnectToClient();
            }
        });
        x.addEventListener("input", () => {
            if (!SessionIDInput.value || !RequesterNameInput.value) {
                ConnectButton.setAttribute("disabled", "disabled");
            }
            else {
                ConnectButton.removeAttribute("disabled");
            }
        });
    });
    ExtrasMenuButton.addEventListener("click", (ev) => {
        ev.stopPropagation();
        CloseAllPopupMenus(ExtrasMenu.id);
        const x = document.body.clientWidth -
            ExtrasMenuButton.getBoundingClientRect().right;
        const right = `${x.toFixed(0)}px`;
        const y = ExtrasMenuButton.getBoundingClientRect().bottom;
        const top = `${y.toFixed(0)}px`;
        ExtrasMenu.style.right = right;
        ExtrasMenu.style.top = top;
        ExtrasMenu.classList.toggle("open");
        window.addEventListener("click", () => {
            CloseAllPopupMenus(null);
        }, { once: true });
    });
    FileTransferButton.addEventListener("click", (ev) => {
        ev.stopPropagation();
        const x = document.body.clientWidth -
            FileTransferButton.getBoundingClientRect().right;
        const right = `${x.toFixed(0)}px`;
        const y = FileTransferButton.getBoundingClientRect().bottom;
        const top = `${y.toFixed(0)}px`;
        FileTransferMenu.style.right = right;
        FileTransferMenu.style.top = top;
        FileTransferMenu.classList.toggle("open");
        const buttonZindex = Number.parseInt(getComputedStyle(FileTransferButton.parentElement).zIndex);
        FileTransferMenu.style.zIndex = `${buttonZindex + 1}`;
        window.addEventListener("click", () => {
            CloseAllPopupMenus(null);
        }, { once: true });
    });
    FileUploadButtton.addEventListener("click", (ev) => {
        FileTransferInput.click();
    });
    FileDownloadButton.addEventListener("click", async (ev) => {
        if (ViewerApp.ViewOnlyMode) {
            alert("Mode affichage seul activé.");
            return;
        }
        await ViewerApp.MessageSender.SendOpenFileTransferWindow();
    });
    FileTransferInput.addEventListener("change", (ev) => {
        UploadFiles(FileTransferInput.files);
    });
    FitToScreenButton.addEventListener("click", (ev) => {
        FitToScreenButton.classList.toggle("toggled");
        if (FitToScreenButton.classList.contains("toggled")) {
            ScreenViewer.classList.add("fit");
        }
        else {
            ScreenViewer.classList.remove("fit");
        }
    });
    FullScreenButton.addEventListener("click", () => {
        FullScreenButton.classList.toggle("toggled");
        if (FullScreenButton.classList.contains("toggled")) {
            document.body.requestFullscreen();
        }
        else {
            document.exitFullscreen();
        }
    });
    BlockInputButton.addEventListener("click", async (ev) => {
        if (ViewerApp.ViewOnlyMode) {
            alert("Mode affichage seul activé.");
            return;
        }
        BlockInputButton.classList.toggle("toggled");
        if (BlockInputButton.classList.contains("toggled")) {
            await ViewerApp.MessageSender.SendToggleBlockInput(true);
        }
        else {
            await ViewerApp.MessageSender.SendToggleBlockInput(false);
        }
    });
    InviteButton.addEventListener("click", (ev) => {
        var url = "";
        if (ViewerApp.Mode == RemoteControlMode.Attended) {
            url = `${location.origin}${location.pathname}?sessionId=${ViewerApp.SessionId}`;
        }
        else {
            url = `${location.origin}${location.pathname}?mode=Unattended&sessionId=${ViewerApp.SessionId}&accessKey=${ViewerApp.AccessKey}`;
        }
        ViewerApp.ClipboardWatcher.SetClipboardText(url);
        ShowToast("Lien copié dans le presse-papiers.");
    });
    KeyboardButton.addEventListener("click", (ev) => {
        CloseAllPopupMenus(null);
        KeyboardButton.classList.toggle("toggled");
        if (KeyboardButton.classList.contains("toggled")) {
            TouchKeyboardInput.focus();
        }
        else {
            TouchKeyboardInput.blur();
        }
    });
    MenuButton.addEventListener("click", (ev) => {
        MenuFrame.classList.toggle("open");
        MenuButton.classList.toggle("open");
        if (MenuFrame.classList.contains("open")) {
            BetaPillPullDown.classList.add("d-none");
        }
        else {
            BetaPillPullDown.classList.remove("d-none");
        }
        CloseAllPopupMenus(null);
    });
    MetricsButton.addEventListener("click", () => {
        MetricsFrame.classList.toggle("d-none");
    });
    QualityModeButton.addEventListener("click", (ev) => {
        ev.stopPropagation();
        CloseAllPopupMenus(QualityModeMenu.id);
        const x = document.body.clientWidth -
            QualityModeButton.getBoundingClientRect().right;
        const right = `${x.toFixed(0)}px`;
        const y = QualityModeButton.getBoundingClientRect().bottom;
        const top = `${y.toFixed(0)}px`;
        QualityModeMenu.style.right = right;
        QualityModeMenu.style.top = top;
        QualityModeMenu.classList.toggle("open");
        window.addEventListener("click", () => {
            CloseAllPopupMenus(null);
        }, { once: true });
    });
    const setQualityMode = async (mode, label) => {
        [QualityAutoButton, QualityPerformanceButton, QualityHighButton].forEach(btn => {
            btn.classList.remove("toggled");
        });
        switch (mode) {
            case RemoteControlQualityMode.Auto:
                QualityAutoButton.classList.add("toggled");
                break;
            case RemoteControlQualityMode.Performance:
                QualityPerformanceButton.classList.add("toggled");
                break;
            case RemoteControlQualityMode.Quality:
                QualityHighButton.classList.add("toggled");
                break;
        }
        QualityModeLabel.innerText = label;
        await ViewerApp.MessageSender.SendQualityMode(mode);
        ShowToast(`Mode de qualité : ${label}`);
    };
    QualityAutoButton.addEventListener("click", async () => {
        await setQualityMode(RemoteControlQualityMode.Auto, "Auto");
    });
    QualityPerformanceButton.addEventListener("click", async () => {
        await setQualityMode(RemoteControlQualityMode.Performance, "Performance");
    });
    QualityHighButton.addEventListener("click", async () => {
        await setQualityMode(RemoteControlQualityMode.Quality, "Qualité");
    });
    ScreenViewer.addEventListener("pointerup", async (ev) => {
        if (ViewerApp.ViewOnlyMode) {
            return;
        }
        const percentX = ev.offsetX / ScreenViewer.clientWidth;
        const percentY = ev.offsetY / ScreenViewer.clientHeight;
        if (longPressStarted && !isDragging) {
            await ViewerApp.MessageSender.SendMouseDown(2, percentX, percentY);
            await ViewerApp.MessageSender.SendMouseUp(2, percentX, percentY);
        }
        if (longPressStarted && isDragging) {
            // If we're dragging, we need to send a mouse up event.
            await ViewerApp.MessageSender.SendMouseUp(0, percentX, percentY);
        }
        resetTouchState();
    });
    ScreenViewer.addEventListener("pointercancel", ev => {
        resetTouchState();
    });
    ScreenViewer.addEventListener("pointerout", ev => {
        resetTouchState();
    });
    ScreenViewer.addEventListener("pointerleave", ev => {
        resetTouchState();
    });
    ScreenViewer.addEventListener("touchmove", async function (e) {
        currentTouchCount = e.touches.length;
        if (longPressStarted || isDragging) {
            e.preventDefault();
        }
        if (e.touches.length == 1 && longPressStarted && !isDragging) {
            e.stopPropagation();
            if (ViewerApp.ViewOnlyMode) {
                return;
            }
            const rect = ScreenViewer.getBoundingClientRect();
            const offsetX = e.touches[0].pageX - rect.left;
            const offsetY = e.touches[0].pageY - rect.top;
            const moveDistance = GetDistanceBetween(longPressStartOffsetX, longPressStartOffsetY, offsetX, offsetY);
            if (moveDistance > 5) {
                isDragging = true;
                // Move the mouse to the initial touch point to start the drag operation.
                const percentX = longPressStartOffsetX / ScreenViewer.clientWidth;
                const percentY = longPressStartOffsetY / ScreenViewer.clientHeight;
                await ViewerApp.MessageSender.SendMouseMove(percentX, percentY);
                await ViewerApp.MessageSender.SendMouseDown(0, percentX, percentY);
            }
            return;
        }
        if (isDragging) {
            e.stopPropagation();
            if (ViewerApp.ViewOnlyMode) {
                return;
            }
            var screenViewerLeft = ScreenViewer.getBoundingClientRect().left;
            var screenViewerTop = ScreenViewer.getBoundingClientRect().top;
            var pagePercentX = (e.touches[0].pageX - screenViewerLeft) / ScreenViewer.clientWidth;
            var pagePercentY = (e.touches[0].pageY - screenViewerTop) / ScreenViewer.clientHeight;
            ViewerApp.MessageSender.SendMouseMove(pagePercentX, pagePercentY);
            return;
        }
        if (e.touches.length == 2) {
            let touchMove1 = lastScrollTouchY1 - e.touches[0].pageY;
            let touchMove2 = lastScrollTouchY2 - e.touches[1].pageY;
            if (!isPinchZooming && (isScrolling || touchMove1 * touchMove2 > 0)) {
                // Both touch points are moving in the same direction.  We're doing a scroll.
                if (!isScrolling) {
                    // If this is the start of scrolling, move the mouse to our touch point so
                    // the scroll wheel action will target the intended element on screen.
                    var screenViewerLeft = ScreenViewer.getBoundingClientRect().left;
                    var screenViewerTop = ScreenViewer.getBoundingClientRect().top;
                    var pagePercentX = (e.touches[0].pageX - screenViewerLeft) / ScreenViewer.clientWidth;
                    var pagePercentY = (e.touches[0].pageY - screenViewerTop) / ScreenViewer.clientHeight;
                    await ViewerApp.MessageSender.SendMouseMove(pagePercentX, pagePercentY);
                }
                isScrolling = true;
                if (Date.now() - lastScrollTime < 100) {
                    return;
                }
                lastScrollTime = Date.now();
                let yMove = Math.max(-1, Math.min(touchMove1, 1));
                await ViewerApp.MessageSender.SendMouseWheel(0, yMove);
                lastScrollTouchY1 = e.touches[0].pageY;
                return;
            }
            var pinchPoint1 = {
                X: e.touches[0].pageX,
                Y: e.touches[0].pageY,
                IsEmpty: false,
            };
            var pinchPoint2 = {
                X: e.touches[1].pageX,
                Y: e.touches[1].pageY,
                IsEmpty: false,
            };
            var pinchDistance = GetDistanceBetween(pinchPoint1.X, pinchPoint1.Y, pinchPoint2.X, pinchPoint2.Y);
            var pinchCenterX = (pinchPoint1.X + pinchPoint2.X) / 2;
            var pinchCenterY = (pinchPoint1.Y + pinchPoint2.Y) / 2;
            ScreenViewerWrapper.scrollBy(lastPinchCenterX - pinchCenterX, lastPinchCenterY - pinchCenterY);
            lastPinchCenterX = pinchCenterX;
            lastPinchCenterY = pinchCenterY;
            if (Math.abs(pinchDistance - lastPinchDistance) > 5) {
                isPinchZooming = true;
                if (FitToScreenButton.classList.contains("toggled")) {
                    FitToScreenButton.click();
                }
                var currentWidth = ScreenViewer.clientWidth;
                var currentHeight = ScreenViewer.clientHeight;
                var clientAdjustedScrollLeftPercent = (ScreenViewerWrapper.scrollLeft +
                    ScreenViewerWrapper.clientWidth * 0.5) /
                    ScreenViewerWrapper.scrollWidth;
                var clientAdjustedScrollTopPercent = (ScreenViewerWrapper.scrollTop +
                    ScreenViewerWrapper.clientHeight * 0.5) /
                    ScreenViewerWrapper.scrollHeight;
                var currentWidthPercent = Number(ScreenViewer.style.width.slice(0, -1));
                var newWidthPercent = Math.max(100, currentWidthPercent +
                    (pinchDistance - lastPinchDistance) * (currentWidthPercent / 100));
                newWidthPercent = Math.min(5000, newWidthPercent);
                ScreenViewer.style.width = String(newWidthPercent) + "%";
                var heightChange = ScreenViewer.clientHeight - currentHeight;
                var widthChange = ScreenViewer.clientWidth - currentWidth;
                var pinchAdjustX = pinchCenterX / window.innerWidth - 0.5;
                var pinchAdjustY = pinchCenterY / window.innerHeight - 0.5;
                var scrollByX = widthChange *
                    (clientAdjustedScrollLeftPercent +
                        (pinchAdjustX * ScreenViewerWrapper.clientWidth) /
                            ScreenViewerWrapper.scrollWidth);
                var scrollByY = heightChange *
                    (clientAdjustedScrollTopPercent +
                        (pinchAdjustY * ScreenViewerWrapper.clientHeight) /
                            ScreenViewerWrapper.scrollHeight);
                ScreenViewerWrapper.scrollBy(scrollByX, scrollByY);
                lastPinchDistance = pinchDistance;
            }
            return;
        }
    });
    ScreenViewer.addEventListener("pointerdown", function (e) {
        currentPointerDevice = e.pointerType;
    });
    ScreenViewer.addEventListener("pointerenter", function (e) {
        currentPointerDevice = e.pointerType;
    });
    ScreenViewer.addEventListener("pointermove", function (e) {
        currentPointerDevice = e.pointerType;
    });
    ScreenViewer.addEventListener("mousemove", async function (e) {
        if (ViewerApp.ViewOnlyMode) {
            return;
        }
        if (pointerMoveTimeout > -1) {
            clearTimeout(pointerMoveTimeout);
        }
        var percentX = e.offsetX / ScreenViewer.clientWidth;
        var percentY = e.offsetY / ScreenViewer.clientHeight;
        const now = Date.now();
        // Throttle mouse movements so we're not sending too many messages rapidly.
        // The timeout ensures that the last movement still gets sent.
        if (now - lastPointerMove < 50) {
            pointerMoveTimeout = setTimeout(async () => {
                await ViewerApp.MessageSender.SendMouseMove(percentX, percentY);
            }, 60);
            return;
        }
        lastPointerMove = now;
        await ViewerApp.MessageSender.SendMouseMove(percentX, percentY);
    });
    ScreenViewer.addEventListener("mousedown", async function (e) {
        if (ViewerApp.ViewOnlyMode) {
            return;
        }
        var percentX = e.offsetX / ScreenViewer.clientWidth;
        var percentY = e.offsetY / ScreenViewer.clientHeight;
        await ViewerApp.MessageSender.SendMouseDown(e.button, percentX, percentY);
    });
    ScreenViewer.addEventListener("mouseup", async function (e) {
        if (ViewerApp.ViewOnlyMode) {
            return;
        }
        var percentX = e.offsetX / ScreenViewer.clientWidth;
        var percentY = e.offsetY / ScreenViewer.clientHeight;
        await ViewerApp.MessageSender.SendMouseUp(e.button, percentX, percentY);
    });
    ScreenViewer.addEventListener("touchstart", function (e) {
        currentTouchCount = e.touches.length;
        if (currentTouchCount == 2) {
            lastScrollTouchY1 = e.touches[0].pageY;
            lastScrollTouchY2 = e.touches[1].pageY;
            startPinchPoint1 = {
                X: e.touches[0].pageX,
                Y: e.touches[0].pageY,
                IsEmpty: false,
            };
            startPinchPoint2 = {
                X: e.touches[1].pageX,
                Y: e.touches[1].pageY,
                IsEmpty: false,
            };
            lastPinchDistance = GetDistanceBetween(startPinchPoint1.X, startPinchPoint1.Y, startPinchPoint2.X, startPinchPoint2.Y);
            lastPinchCenterX = (startPinchPoint1.X + startPinchPoint2.X) / 2;
            lastPinchCenterY = (startPinchPoint1.Y + startPinchPoint2.Y) / 2;
        }
        isDragging = false;
        var focusedInput = document.querySelector("input:focus");
        if (focusedInput) {
            focusedInput.blur();
        }
    });
    ScreenViewer.addEventListener("touchend", async function (e) {
        currentTouchCount = e.touches.length;
        if (currentTouchCount == 0) {
            isPinchZooming = false;
            isScrolling = false;
            lastScrollTouchY1 = null;
            lastScrollTouchY2 = null;
            startPinchPoint1 = null;
            startPinchPoint2 = null;
        }
        var percentX = (e.changedTouches[0].pageX - ScreenViewer.getBoundingClientRect().left) /
            ScreenViewer.clientWidth;
        var percentY = (e.changedTouches[0].pageY - ScreenViewer.getBoundingClientRect().top) /
            ScreenViewer.clientHeight;
        if (longPressStarted && !isDragging && !ViewerApp.ViewOnlyMode) {
            await ViewerApp.MessageSender.SendMouseDown(2, percentX, percentY);
            await ViewerApp.MessageSender.SendMouseUp(2, percentX, percentY);
        }
        if (isDragging && !ViewerApp.ViewOnlyMode) {
            await ViewerApp.MessageSender.SendMouseUp(0, percentX, percentY);
        }
        longPressStarted = false;
        isDragging = false;
    });
    ScreenViewer.addEventListener("contextmenu", async (ev) => {
        ev.preventDefault();
        if (ViewerApp.ViewOnlyMode) {
            return;
        }
        if (currentPointerDevice == "touch") {
            // We're either starting a right-click or left-button drag.
            // Either way, we'll move the cursor to the initial touch point.
            const percentX = (ev.pageX - ScreenViewer.getBoundingClientRect().left) /
                ScreenViewer.clientWidth;
            const percentY = (ev.pageY - ScreenViewer.getBoundingClientRect().top) /
                ScreenViewer.clientHeight;
            await ViewerApp.MessageSender.SendMouseMove(percentX, percentY);
            longPressStarted = true;
            longPressStartOffsetX = ev.offsetX;
            longPressStartOffsetY = ev.offsetY;
        }
    });
    ScreenViewer.addEventListener("wheel", async function (e) {
        e.preventDefault();
        if (ViewerApp.ViewOnlyMode) {
            return;
        }
        await ViewerApp.MessageSender.SendMouseWheel(e.deltaX, e.deltaY);
    });
    TouchKeyboardInput.addEventListener("keydown", async (ev) => {
        if (ev.key === "Enter" || ev.key === "Backspace") {
            await ViewerApp.MessageSender.SendKeyPress(ev.key);
        }
    });
    TouchKeyboardInput.addEventListener("input", async (ev) => {
        if (ViewerApp.ViewOnlyMode) {
            return;
        }
        const text = TouchKeyboardInput.value;
        TouchKeyboardInput.value = "";
        await ViewerApp.MessageSender.SendTextTransfer(text, true);
    });
    TouchKeyboardInput.addEventListener("blur", () => {
        if (KeyboardButton.classList.contains("toggled")) {
            TouchKeyboardInput.focus();
        }
    });
    WindowsSessionMenuButton.addEventListener("click", (ev) => {
        ev.stopPropagation();
        CloseAllPopupMenus(WindowsSessionMenu.id);
        const x = document.body.clientWidth -
            WindowsSessionMenuButton.getBoundingClientRect().right;
        const right = `${x.toFixed(0)}px`;
        const y = WindowsSessionMenuButton.getBoundingClientRect().bottom;
        const top = `${y.toFixed(0)}px`;
        WindowsSessionMenu.style.right = right;
        WindowsSessionMenu.style.top = top;
        WindowsSessionMenu.classList.toggle("open");
        window.addEventListener("click", () => {
            CloseAllPopupMenus(null);
        }, { once: true });
    });
    WindowsSessionSelect.addEventListener("click", (ev) => {
        ev.stopPropagation();
    });
    WindowsSessionSelect.addEventListener("focus", async () => {
        await ViewerApp.MessageSender.GetWindowsSessions();
    });
    WindowsSessionSelect.addEventListener("change", async () => {
        SetStatusMessage("Changement de session…");
        ShowToast("Changement de session…");
        await ViewerApp.MessageSender.ChangeWindowsSession(Number(WindowsSessionSelect.selectedOptions[0].value));
    });
    // Copier-coller direct : Ctrl+V local -> presse-papier distant -> Ctrl+V distant.
    // On laisse le navigateur déclencher l'événement "paste" (aucune autorisation
    // requise), on pousse le texte dans le presse-papier du poste distant, puis on
    // rejoue la touche V (Ctrl est déjà enfoncé côté distant).
    let pasteKey = null;
    let pasteFallbackTimer = null;
    const altGrChars = new Set();
    const isEditableFocused = () => !!(document.querySelector("input:focus") ||
        document.querySelector("textarea:focus"));
    const sendPasteKey = async () => {
        if (pasteFallbackTimer) {
            window.clearTimeout(pasteFallbackTimer);
            pasteFallbackTimer = null;
        }
        const key = pasteKey;
        if (!key) {
            return;
        }
        pasteKey = null;
        await ViewerApp.MessageSender.SendKeyDown(key);
        await ViewerApp.MessageSender.SendKeyUp(key);
    };
    window.addEventListener("paste", async function (e) {
        var _a;
        if (isEditableFocused() || ViewerApp.ViewOnlyMode || !pasteKey) {
            return;
        }
        e.preventDefault();
        const text = (_a = e.clipboardData) === null || _a === void 0 ? void 0 : _a.getData("text/plain");
        if (!text) {
            await sendPasteKey();
            return;
        }
        // Nécessite un agent >= 2026.09.29 (presse-papier Win32 dans ClipboardService).
        ViewerApp.ClipboardWatcher.LastClipboardText = text;
        await ViewerApp.MessageSender.SendTextTransfer(text, false);
        // Laisse le temps à l'agent de remplir le presse-papier Windows.
        await new Promise(r => window.setTimeout(r, 150));
        await sendPasteKey();
    });
    window.addEventListener("keydown", async function (e) {
        if (isEditableFocused()) {
            return;
        }
        if (ViewerApp.ViewOnlyMode) {
            return;
        }
        if ((e.ctrlKey || e.metaKey) && !e.altKey && e.key.toLowerCase() == "v") {
            // Pas de preventDefault : sinon le navigateur ne déclenche pas "paste".
            if (!e.repeat) {
                pasteKey = e.key;
                // Filet de sécurité si le navigateur ne déclenche pas "paste".
                pasteFallbackTimer = window.setTimeout(sendPasteKey, 400);
            }
            return;
        }
        if (!e.ctrlKey || !e.shiftKey || e.key.toLowerCase() != "i") {
            e.preventDefault();
        }
        // AltGr (AZERTY : @ # { [ | ` \ ^ ] } €) arrive côté distant comme Ctrl + Alt,
        // que l'agent ne sait pas rejouer (VkKeyScan renvoie les modificateurs dans
        // l'octet haut). On relâche Ctrl côté distant et on tape le caractère en Unicode.
        if (e.key === "AltGraph") {
            return;
        }
        // Idem pour le pavé numérique : "1" deviendrait "&" sur un poste AZERTY.
        if (e.key.length === 1 && (e.getModifierState("AltGraph") || e.code.startsWith("Numpad"))) {
            altGrChars.add(e.key);
            ViewerApp.ClipboardWatcher.FlushPendingText();
            await ViewerApp.MessageSender.SendKeyUp("Control");
            await ViewerApp.MessageSender.SendTextTransfer(e.key, true);
            return;
        }
        ViewerApp.ClipboardWatcher.FlushPendingText();
        await ViewerApp.MessageSender.SendKeyDown(e.key);
    });
    window.addEventListener("keyup", async function (e) {
        if (isEditableFocused()) {
            return;
        }
        e.preventDefault();
        if (ViewerApp.ViewOnlyMode) {
            return;
        }
        if (pasteKey && e.key.toLowerCase() == "v") {
            // La touche sera envoyée (down + up) une fois le texte collé.
            return;
        }
        if (e.key === "AltGraph") {
            return;
        }
        if (altGrChars.delete(e.key)) {
            // Caractère AltGr déjà tapé en Unicode au keydown.
            return;
        }
        await ViewerApp.MessageSender.SendKeyUp(e.key);
    });
    window.addEventListener("mousedown", () => {
        ViewerApp.ClipboardWatcher.FlushPendingText();
    });
    window.addEventListener("blur", async () => {
        if (ViewerApp.ViewOnlyMode) {
            return;
        }
        await ViewerApp.MessageSender.SendSetKeyStatesUp();
    });
    window.addEventListener("touchstart", () => {
        KeyboardButton.classList.remove("d-none");
    });
    window.ondragover = function (e) {
        e.preventDefault();
        e.dataTransfer.dropEffect = "copy";
    };
    window.ondrop = function (e) {
        e.preventDefault();
        if (e.dataTransfer.files.length < 1) {
            return;
        }
        UploadFiles(e.dataTransfer.files);
    };
}
function resetTouchState() {
    longPressStarted = false;
    isDragging = false;
    isPinchZooming = false;
    isScrolling = false;
}
//# sourceMappingURL=InputEventHandlers.js.map