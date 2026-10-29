# Historique des modifications

Ce dépôt repart d'un historique git vierge (27/10/2026, publication sous le nom Amarre). Il est basé sur Remotely dans son état du 17/12/2024, sous licence GPL v3. Ce document résume les modifications apportées depuis, dans l'ordre.

## Juin 2026 — Traduction, sécurité, performances

- **Traduction française de l'interface web** : navigation, connexion, inscription, accueil, page À propos.
- **Traduction de l'application Desktop** : invite d'accès, chat, transfert de fichiers, indicateur de session, boîtes de dialogue.
- **Durcissement** : en-têtes HTTP défensifs, Swagger limité à l'environnement de développement, verrouillage des comptes contre la force brute.
- **Allègement** : suppression d'environ 7,9 Mo d'assets FontAwesome inutilisés (JS, sprites, CSS non référencés).
- **Dépendances** : correction des vulnérabilités signalées par Dependabot (MailKit 4.17, MessagePack 2.5.302, Azure.Identity 1.19, paquets ASP.NET 8.0.11, SignalR 8.0.19, épinglage de SQLitePCLRaw).

## Juillet 2026 — Build

- **Build débloqué** : bibliothèques front (libman) récupérées via jsDelivr.
- **Sécurité** : SQLitePCLRaw 3.50.3 (corrige une faille High).
- README : retrait des badges de CI.

## Septembre 2026 — Sécurité, copier-coller, publication

- **SignalR 8.0.31** pour l'agent, le Desktop et le serveur (corrige la faille High GHSA-f8h2-vmm9-qhj6).
- **Copier-coller direct pendant la prise en main** :
  - viewer : Ctrl+V pousse le texte local dans le presse-papier du poste distant puis rejoue Ctrl+V ; Ctrl+C sur le poste distant remplit le presse-papier local ;
  - le presse-papier local n'est plus lu en tâche de fond ;
  - agent : le presse-papier passe par l'API Win32 sous Windows (Avalonia n'a pas de fenêtre pendant une session, d'où l'erreur « Clipboard is null »).
- **Script de signature** des agents Windows (`Utilities/Sign-Remotely.ps1`).
- **Documentation** : README réécrit en guide pas à pas (installation, agents, mises à jour, retour arrière, dépannage) et procédure de build (`BUILD-FR.md`).
- **Nettoyage** : suppression des éléments propres au projet d'origine (modèles GitHub, anciennes CI, Dockerfile qui téléchargeaient ses versions publiées).
- **Publication automatique** : un tag `vX.Y` construit l'image serveur (ghcr.io) et les agents Windows x64 (release GitHub).
- **Clavier AZERTY** : caractères AltGr et pavé numérique tapés en Unicode par le viewer (`VkKeyScan` côté agent renvoyait une touche invalide ou le mauvais caractère).
- **Agents toutes plateformes** : la release produit Windows x64/x86, Linux x64 et macOS x64/arm64 dans un `Content.zip`, et ne recompile les agents que si leur code a changé depuis le tag précédent.
- **`Utilities/maj-agents.sh`** : télécharge `Content.zip` d'une release et le décompresse dans le volume `content` du serveur (option `GARDER_WIN64=1`).
- **.NET 10 LTS** : tous les projets, paquets Microsoft en `10.0.*`, PowerShell SDK 7.6, image `aspnet:10.0`, CI et doc.
- **Viewer** :
  - mode de qualité d'image Auto / Performance / Qualité (menu Extras) ;
  - multi-écran : schéma de la disposition des écrans, miniatures de chaque écran, animation au changement.
- **Agents** : l'ETag est enregistré après une mise à jour (plus de réinstallation en boucle) ; bouton « Mettre à jour l'agent » sur la carte d'un appareil.
- **Linux ARM64** (Raspberry Pi…) : installeur, mise à jour auto et appli de prise de main, compilés par la release.
- **Page Sauvegarde** : export JSON des appareils (alias, tags, notes, groupe) et des groupes, restauration depuis ce fichier.
- **Build propre** : zéro warning et aucun paquet vulnérable (Avalonia 11.3, Serilog 10, Swashbuckle 10, MSTest 4…) ; la CI de pull request le vérifie (`-warnaserror`) et lance les tests.
- **Démarrage .NET 10** : schéma Identity figé sur la base existante (pas de table passkeys) et convertisseur de dates SQLite/PostgreSQL limité aux types avec clé (plantage `IdentityPasskeyData` au démarrage).
- **CI** : chaque pull request démarre réellement le serveur et teste inscription + connexion (`Utilities/test-login.sh`).

## Octobre 2026 — Publication par fichier VERSION

- **Publication** : monter le fichier `VERSION` sur `main` suffit. La release relance d'abord les contrôles de `build.yml` (zéro warning, zéro paquet vulnérable, tests, démarrage du serveur), puis publie l'image ghcr, les agents s'ils ont changé, et pose le tag `vX.Y`. Plus besoin de droits pour pousser un tag.
- **Analyse des failles de l'image** : à chaque pull request et chaque push sur `main`, l'image serveur est construite et analysée par Grype ; une faille corrigeable de sévérité moyenne ou plus bloque la publication. L'image applique les correctifs de sécurité Debian au build (`apt-get upgrade`). (Trivy en v1.8, remplacé.)
- **Correctifs issus d'une revue de sécurité du code** :
  - viewer : les messages et noms de fichiers venant du poste distant sont affichés comme du texte (`textContent`), plus interprétés comme du HTML ;
  - seul un administrateur serveur peut réinitialiser le mot de passe, changer le rôle ou supprimer un compte administrateur serveur (page Organisation et API) ;
  - supprimer une invitation ne supprime plus que les comptes non activés de sa propre organisation ;
  - emails de demande de support et d'erreur de script : les champs saisis ou remontés par le poste sont encodés avant d'être insérés dans le HTML.
- **Postes clonés (DeviceID en double)** : un poste cloné avec l'agent installé gardait le DeviceID de l'original ; les deux se disputaient la même fiche (poste qui passe en ligne et hors ligne, prise en main vers le mauvais poste). L'agent note l'UUID matériel du poste (SMBIOS) à côté de son DeviceID et en tire un nouveau s'il démarre sur un autre matériel. Le serveur refuse un second agent récent sur un DeviceID déjà connecté depuis un autre poste et lui fait tirer un nouvel ID ; les anciens agents sont acceptés comme avant, le doublon est journalisé.
- **Connexion des agents** : la déconnexion d'une ancienne connexion ne marque plus hors ligne un poste reconnecté sous le même DeviceID. Le serveur journalise la cause de chaque déconnexion d'agent.
- **À propos** : affiche la version du serveur (fichier `VERSION`) au lieu de `1.0.0.0`.
- **Dépendances** : MessagePack 2.5.303 (avis du 15/10/2026). Les contrôles de `build.yml` tournent aussi chaque lundi, pour qu'un nouvel avis bloque avant la prochaine publication.

## Versions

| Version | Date | Contenu |
|---|---|---|
| Image 1.1 | 28/09/2026 | SignalR 8.0.31 ; agents 2026.09.28.1300 |
| Image 1.2 | 29/09/2026 | Copier-coller (viewer seul) |
| v1.4 | 29/09/2026 | Copier-coller complet (viewer + agent) ; agents 2026.09.29.1449 ; publication automatique |
| v1.5 | 29/09/2026 | Viewer : caractères AltGr (@ # { } [ ] \| \ €) tapés en Unicode sur poste AZERTY ; agents inchangés |
| v1.6 | 29/09/2026 | Viewer : pavé numérique tapé en Unicode (chiffres corrects sur poste AZERTY) ; agents inchangés |
| v1.7 | 29/09/2026 | .NET 10 ; mode qualité, multi-écran, sauvegarde JSON, bouton « Mettre à jour l'agent », Linux ARM64, correctif ETag ; paquets à jour, zéro warning ; agents recompilés |
| v1.8 | 05/10/2026 | Publication par fichier `VERSION` ; analyse Trivy de l'image avant publication ; correctifs de sécurité Debian dans l'image ; agents inchangés |
| v1.9 | 15/10/2026 | Correctifs de sécurité issus de la revue du code : viewer en `textContent`, compte admin du serveur protégé des admins d'organisation, `DeleteInvite` limité à l'organisation, e-mails encodés ; `System.Security.Cryptography.Pkcs` et `.Xml` forcés en 10.0.13 (avis du 13/10/2026) ; analyse Grype de l'image (remplace Trivy) ; agents recompilés v2026.10.15.1721 |
| v1.10 | 15/10/2026 | Hub `/hubs/viewer` : l'obligation de connexion du contrôle à distance est appliquée au hub (avant, seulement à la page) ; OTP des liens API transmis au hub ; agents inchangés |
| v1.11 | 19/10/2026 | Postes clonés : UUID matériel, nouvel ID pour le clone, doublon refusé ou journalisé ; déconnexion qui ne marque plus hors ligne le poste reconnecté ; cause des déconnexions journalisée ; « À propos » affiche la version ; MessagePack 2.5.303 ; contrôles hebdomadaires ; agents recompilés |
| v1.12 | 19/10/2026 | Journaux : fin du bruit « Device has not been set », exceptions de fin de session et de fermeture du viewer ; agent : réenregistrement avant la vérification de mise à jour après reconnexion, scripts en attente vérifiés à la connexion ; accueil : la recherche n'accepte plus les postes qui passent en ligne hors filtre ; agents recompilés |
| v1.13 | 20/10/2026 | Scripts en attente : requêtes séparées, sans les résultats des autres postes (la jointure unique remplissait le disque de fichiers temporaires SQLite, « database or disk is full ») ; agents inchangés |
| v1.14 | 26/10/2026 | Scripts : recherche de script (dossier/nom) et de poste dans les onglets ; case des postes de l'onglet Exécuter corrigée ; accueil : version de l'agent sous le nom de l'appareil ; agents inchangés |
| v1.15 | 26/10/2026 | Agents numérotés comme la release qui les recompile (X.Y) au lieu de la date ; témoin « agent obsolète » rétabli : version du poste différente des agents servis (`AgentVersion.txt` dans `Content.zip`) ; agents inchangés (publiés à part : agents-1.15) |
| v1.16 | 27/10/2026 | Projet publié sous le nom Amarre (nouveau dépôt public, nouveau logo) ; agents : jeton exigé à la connexion (un agent réinstallé doit être supprimé de l'interface pour revenir), heartbeat réservé à l'appareil enregistré ; interface entièrement traduite (appareils, scripts, organisation, config serveur, lecteur, compte, téléchargements, crédits, erreurs) ; accueil redessiné ; agents recompilés |
| v1.17 | 27/10/2026 | Client d'assistance : titre par défaut « Assistance à distance » (organisations existantes : le changer dans **Personnalisation**) ; pied de page du viewer : « Amarre, basé sur Remotely » ; `signtool.exe` retiré du dépôt (`Publish.ps1` utilise celui du Windows SDK) ; scripts de développement obsolètes supprimés ; README : section sécurité ; agents recompilés |
| v1.18 | 27/10/2026 | Journaux : l'adresse du destinataire n'est plus écrite à chaque e-mail envoyé (relevé par CodeQL, activé sur le dépôt avec Dependabot et la détection de secrets) ; agents inchangés |
| v1.19 | 27/10/2026 | Correctifs issus d'un audit de sécurité : identifiant d'organisation vérifié avant d'être écrit dans le script d'installation (injection de code par un lien) ; refus par défaut si une vérification d'autorisation de l'API lève une exception ; journaux du serveur et organisation par défaut réservés aux administrateurs du serveur dans l'API ; jetons temporaires limités à leur organisation (fichiers partagés, scripts enregistrés, résultats) ; agent et serveur se prouvent le jeton du poste par défi HMAC, le jeton ne circule plus ; commandes sensibles de l'agent bloquées tant que le serveur n'est pas vérifié ; envoi de fichiers `POST api/FileSharing`, inopérant, retiré. Déployer le serveur avant les agents ; agents recompilés |
| v1.20 | 27/10/2026 | Accueil : actions groupées sur les postes cochés (réveiller, mettre à jour l'agent, changer de groupe, désinstaller l'agent, supprimer ; confirmation avec le nombre de postes pour les deux dernières) ; réveil groupé : tous les postes en ligne d'un groupe relaient la commande (seul le premier le faisait), et le réveil du groupe « Aucun » fonctionne ; sécurité : un résultat de script ne se rattache qu'à une exécution qui vise son poste ; agents inchangés |
| v1.21 | 29/10/2026 | Mise à jour d'agent : l'installeur garde le jeton du poste quand le serveur ne change pas (il le vidait, et le serveur ignorait le nouveau jeton : le poste se bloquait à la reconnexion suivante) ; une connexion d'ancien agent admise sans jeton peut en poser un nouveau. Déployer le serveur, puis `./Utilities/maj-agents.sh v1.21` (les scripts d'installation sont dans Content.zip), avant que les agents obsolètes se mettent à jour ; agents recompilés |
