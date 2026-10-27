import { FileTransferProgress, FileTransferInput, FileTransferNameSpan } from "./UI.js";
import { ViewerApp } from "./App.js";
import { ShowToast } from "./UI.js";
const PartialDownloads = {};
export async function UploadFiles(fileList) {
    if (!FileTransferProgress.parentElement.hasAttribute("hidden")) {
        FileTransferInput.value = null;
        ShowToast("Un transfert de fichier est déjà en cours.");
        return;
    }
    ShowToast("Envoi du fichier démarré");
    FileTransferProgress.value = 0;
    FileTransferProgress.parentElement.removeAttribute("hidden");
    try {
        for (var i = 0; i < fileList.length; i++) {
            FileTransferNameSpan.textContent = fileList[i].name;
            var buffer = await fileList[i].arrayBuffer();
            await ViewerApp.MessageSender.SendFile(new Uint8Array(buffer), fileList[i].name);
        }
        ShowToast("Envoi du fichier terminé.");
    }
    catch (_a) {
        ShowToast("Échec de l'envoi du fichier.");
    }
    FileTransferInput.value = null;
    FileTransferProgress.parentElement.setAttribute("hidden", "hidden");
}
export async function ReceiveFile(file) {
    if (file.StartOfFile) {
        ShowToast(`Téléchargement de ${file.FileName}`);
    }
    var partial = PartialDownloads[file.MessageId];
    if (!partial) {
        partial = new Array();
        PartialDownloads[file.MessageId] = partial;
    }
    if (file.Buffer) {
        partial.push(file.Buffer);
    }
    if (file.EndOfFile) {
        var blob = new Blob(partial, { type: 'application/octet-stream' });
        var url = window.URL.createObjectURL(blob);
        var link = document.createElement('a');
        link.style.display = 'none';
        link.href = url;
        link.download = file.FileName;
        document.body.appendChild(link);
        link.click();
        setTimeout(() => {
            document.body.removeChild(link);
            window.URL.revokeObjectURL(url);
        }, 100);
    }
}
//# sourceMappingURL=FileTransferService.js.map