import { ViewerApp } from "./App.js";
import { ShowToast } from "./UI.js";

export class ClipboardWatcher {
    LastClipboardText: string;
    NewClipboardText: string;

    // Sens local -> distant : géré par l'événement "paste" (Ctrl+V) dans
    // InputEventHandlers. On ne lit plus le presse-papier local en tâche de fond
    // (demande d'autorisation, et évitait d'envoyer au poste client tout ce que
    // le technicien copie, mots de passe compris).
    async WatchClipboard() {
        window.addEventListener("focus", () => this.FlushPendingText());
    }

    // Sens distant -> local : texte copié sur le poste distant (Ctrl+C).
    SetClipboardText(text: string) {
        if (text == this.LastClipboardText) {
            return;
        }

        this.NewClipboardText = text;
        this.FlushPendingText();
    }

    // Écrit le texte en attente dans le presse-papier local. Certains navigateurs
    // (Firefox, Safari) exigent une action récente de l'utilisateur : si ça échoue,
    // on réessaie à la prochaine touche, au prochain clic ou au retour du focus.
    async FlushPendingText() {
        const text = this.NewClipboardText;
        if (!text || !navigator.clipboard?.writeText || !document.hasFocus()) {
            return;
        }

        try {
            await navigator.clipboard.writeText(text);
            this.LastClipboardText = text;
            if (this.NewClipboardText == text) {
                this.NewClipboardText = null;
            }
            ShowToast("Presse-papier mis à jour.");
        }
        catch {
            // On garde le texte en attente.
        }
    }
}
