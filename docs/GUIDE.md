# Amarre — guide complet

[← Retour au README](../README.md)

Ce guide couvre tout le cycle : installation du serveur, déploiement des agents, mises à jour, retour arrière et dépannage.

---

## Sommaire

1. [Ce que le fork ajoute](#1-ce-que-le-fork-ajoute)
2. [Prérequis](#2-prérequis)
3. [Installer le serveur (première fois)](#3-installer-le-serveur-première-fois)
4. [Premier démarrage](#4-premier-démarrage)
5. [Publier et déployer les agents](#5-publier-et-déployer-les-agents)
6. [Installer l'agent sur un poste client](#6-installer-lagent-sur-un-poste-client)
7. [Mettre à jour le serveur](#7-mettre-à-jour-le-serveur)
8. [Mettre à jour les agents](#8-mettre-à-jour-les-agents)
9. [Retour arrière](#9-retour-arrière)
10. [Modifier le code](#10-modifier-le-code)
11. [Dépannage](#11-dépannage)
12. [Référence : configuration](#12-référence--configuration)

---

## 1. Ce que le fork ajoute

- Interface web et agents Windows en français.
- .NET 10 LTS, dépendances de sécurité à jour (SQLitePCLRaw 3.50.3).
- Viewer : mode de qualité d'image (Auto / Performance / Qualité), schéma et miniatures des écrans en multi-écran.
- Bouton « Mettre à jour l'agent » sur chaque appareil, page **Sauvegarde** (export/restauration JSON).
- Agents Linux ARM64 (Raspberry Pi…) en plus de Windows, Linux x64 et macOS.
- **Copier-coller direct** pendant la prise en main :
  - **Ctrl+V** dans le viewer colle le texte copié sur ton PC dans l'application distante ;
  - **Ctrl+C** sur le poste distant envoie le texte dans ton presse-papier ;
  - texte uniquement (les fichiers passent par le transfert de fichiers) ;
  - le presse-papier local n'est jamais lu en tâche de fond ;
  - le bouton du bandeau (« taper le presse-papier ») reste disponible en secours.
- **Clavier AZERTY** : les caractères AltGr (`@ # { } [ ] | \ €`…) et le pavé numérique sont tapés en Unicode sur le poste distant (correctif viewer, v1.5 et v1.6).
- Agents publiés automatiquement pour **Windows x64 et x86, Linux x64** (prise en main, en français) et **macOS x64/arm64** (agent seul : scripts et terminal, Remotely n'a pas d'appli de prise en main sur Mac).
- **Noms hérités de Remotely** : le conteneur `remotely`, les dossiers `/srv/remotely`, la base `Remotely.db`, les variables `Remotely_…`, le service et les fichiers des agents (`Remotely_Service`, `Remotely-Win-x64.zip`…) gardent leur nom. Les renommer casserait les installations existantes et la mise à jour automatique des agents.

## 2. Prérequis

| Où | Quoi |
|---|---|
| Serveur | Linux avec Docker, un reverse proxy HTTPS (BunkerWeb, Nginx Proxy Manager, Caddy…) |
| Poste de build Windows | SDK .NET 10, Git, Node.js (pour recompiler le viewer TypeScript) |

Le serveur n'a pas besoin de .NET : la compilation se fait dans un conteneur SDK.

> **HTTPS obligatoire** : le copier-coller utilise l'API Clipboard du navigateur, qui ne fonctionne qu'en HTTPS.

## 3. Installer le serveur (première fois)

Deux méthodes pour obtenir l'image. Elles donnent le même conteneur, avec les mêmes dossiers et la même configuration :

| | A. Image GitHub (recommandé) | B. Build à la main |
|---|---|---|
| Image | `ghcr.io/jeremychn/amarre:X.Y`, compilée par GitHub Actions à chaque tag `vX.Y` | `amarre:X.Y`, compilée sur le serveur |
| Il faut | Docker | Docker + le dépôt cloné (le SDK .NET tourne dans un conteneur) |
| Quand | Cas normal | Commit pas encore tagué, test, GitHub Actions indisponible |

Faire **3.1 ou 3.2**, puis 3.3 à 3.5 dans les deux cas.

### 3.1 Méthode A : image depuis GitHub (ghcr.io)

1. **Récupérer l'image** : la version à prendre est le dernier tag `vX.Y` de la page **Releases** du dépôt (l'image porte le numéro sans le `v`, par exemple `1.7`).
   ```bash
   docker pull ghcr.io/jeremychn/amarre:X.Y
   ```

### 3.2 Méthode B : build à la main sur le serveur

```bash
git clone https://github.com/jeremychn/amarre.git ~/amarre
cd ~/amarre

rm -rf Server/bin/publish
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet publish Server/Server.csproj -c Release -o Server/bin/publish \
  -p:TypeScriptCompileBlocked=true

docker build -f Server/Dockerfile -t amarre:X.Y .
```

- `-p:TypeScriptCompileBlocked=true` est **obligatoire** : l'image SDK n'a pas Node. Les `.js` du viewer sont déjà compilés dans le dépôt.
- Vérifier après le publish : `ls Server/bin/publish/Remotely_Server.dll`
- Incrémenter le tag à chaque version (`1.1`, `1.2`…) et **garder les anciennes images** pour le retour arrière.

### 3.3 Préparer les dossiers de données

```bash
DATA=/srv/remotely                              # à adapter
IMG=ghcr.io/jeremychn/amarre:X.Y           # méthode B : amarre:X.Y
mkdir -p $DATA/content $DATA/keys

# Scripts Install-* : copiés depuis l'image (valable pour les deux méthodes)
docker create --name remotely-tmp $IMG
docker cp remotely-tmp:/app/wwwroot/Content/. $DATA/content/
docker rm remotely-tmp
```

| Dossier hôte | Dans le conteneur | Contenu |
|---|---|---|
| `$DATA` | `/app/AppData` | Base SQLite, logs, enregistrements |
| `$DATA/content` | `/app/wwwroot/Content` | Agents et installeurs servis aux postes |
| `$DATA/keys` | `/root/.aspnet/DataProtection-Keys` | Clés de session (sinon déconnexion à chaque redémarrage) |

Les agents s'ajoutent dans `content` à l'étape [§5](#5-publier-et-déployer-les-agents). Ce dossier masque celui de l'image : les scripts `Install-*` doivent y être aussi (copiés ci-dessus, et inclus dans `Content.zip`).

### 3.4 Lancer le conteneur

Exemple `docker-compose.yml` :

```yaml
services:
  remotely:
    image: ghcr.io/jeremychn/amarre:X.Y   # méthode B : amarre:X.Y
    container_name: remotely
    restart: unless-stopped
    ports:
      - "5000:5000"
    volumes:
      - /srv/remotely:/app/AppData                      # = $DATA
      - /srv/remotely/content:/app/wwwroot/Content
      - /srv/remotely/keys:/root/.aspnet/DataProtection-Keys
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_HTTP_PORTS=5000
      - TZ=Europe/Paris
      - Remotely_ApplicationOptions__DbProvider=SQLite
      - Remotely_ConnectionStrings__SQLite=Data Source=/app/AppData/Remotely.db
      # Passerelle du réseau Docker du conteneur : docker network inspect <réseau> (exemple : 172.28.0.1)
      - Remotely_ApplicationOptions__DockerGatewayIp=172.28.0.1
```

```bash
docker compose up -d
docker logs -f remotely
```

On peut aussi créer le conteneur depuis Portainer avec les mêmes volumes et variables :

- méthode A : registre **ghcr.io**, image `jeremychn/amarre:X.Y` ;
- méthode B : image `amarre:X.Y`, **Always pull the image** décoché (l'image n'existe que sur le serveur).

**Choisir un seul outil** (compose ou Portainer) et s'y tenir, sinon l'autre devient périmé.

### 3.5 Reverse proxy


Publier `http://<serveur>:5000` en HTTPS. Le proxy doit transmettre `X-Forwarded-Proto`, `X-Forwarded-Host`, `X-Forwarded-For` et les **WebSockets** (SignalR).

Le serveur n'accepte ces en-têtes que depuis loopback et la passerelle Docker (`DockerGatewayIp`). Si le proxy est sur une autre machine, ajouter son IP dans `KnownProxies` (page Server Config). En dernier recours : `Force Client HTTPS`.

## 4. Premier démarrage

1. Ouvrir le site en HTTPS et cliquer sur **Register**.
2. Ce premier compte est administrateur du serveur et de l'organisation.
3. L'inscription se ferme ensuite automatiquement (`MaxOrganizationCount` = 1).
4. Dans **Server Config**, régler au minimum `Require2FA`, `DataRetentionInDays`, `RemoteControlNotifyUser`, et le SMTP si besoin (voir [§12](#12-référence--configuration)).

## 5. Publier et déployer les agents

Les agents ne sont **ni dans le dépôt ni dans l'image** (exclus par `.gitignore`) : ils sont compilés par GitHub Actions et servis depuis le dossier `content` du serveur.

### 5.1 Ce que produit la release

Le job `agents` de `.github/workflows/release.yml` tourne à chaque version publiée (`VERSION` ou tag `vX.Y`), **seulement si le code des agents a changé** depuis le tag précédent (`Agent`, `Desktop.*`, `Shared`, `Directory.Build.props`). Sinon la release sort « (agents inchangés) », sans fichier. Lancement manuel (Actions → Release → **Run workflow**) : les agents sont toujours recompilés, release `agents-<version>` (relancé sur le même commit, il remplace le `Content.zip` de cette release). Il ne publie **pas** d'image serveur : `latest` ne bouge qu'avec une version publiée.

Version des agents = celle de la release qui les recompile (`X.Y`, comme le serveur) : un serveur 1.17 peut servir des agents 1.15 si leur code n'a pas changé depuis. En lancement manuel, version du fichier `VERSION`. La release contient un seul fichier, **`Content.zip`**, à l'image du dossier `content` :

| Fichier | Rôle |
|---|---|
| `Remotely-Win-x64.zip`, `Remotely-Win-x86.zip` | Agent Windows installé (service + appli Desktop) |
| `Remotely-Linux.zip`, `Remotely-Linux-arm64.zip` | Agent Linux x64 / ARM64 (Raspberry Pi…) installé |
| `Remotely-MacOS-x64.zip`, `Remotely-MacOS-arm64.zip` | Agent macOS (sans prise en main) |
| `Win-x64/`, `Win-x86/Remotely_Desktop.exe` | Client Windows portable (assistance ponctuelle) |
| `Linux-x64/Remotely_Desktop` | Client Linux portable |
| `Install-*.ps1`, `Install-*.sh` | Installeurs (le serveur y injecte son URL et l'organisation) |
| `AgentVersion.txt` | Version des agents servis, lue par le serveur pour le témoin « agent obsolète » |

Agents **non signés**. macOS non testé (un Mac arm64 peut refuser un binaire non signé).

### 5.2 Déployer sur le serveur

Script `Utilities/maj-agents.sh` : télécharge `Content.zip` et le décompresse dans `content`, après sauvegarde dans `content.bak-<date>`. Aucun redémarrage du conteneur.

Une seule fois, en root :

```bash
apt install -y unzip curl python3
```

Récupérer le script en clonant le dépôt :

```bash
git clone https://github.com/jeremychn/amarre.git ~/amarre
```

Ou sans cloner le dépôt :

```bash
curl -fsSL https://raw.githubusercontent.com/jeremychn/amarre/main/Utilities/maj-agents.sh \
  -o ~/maj-agents.sh && chmod +x ~/maj-agents.sh
```

À chaque release (depuis `~/amarre`, ou avec `~/maj-agents.sh`) :

```bash
cd ~/amarre && git pull
./Utilities/maj-agents.sh                  # tout, dernière release
GARDER_WIN64=1 ./Utilities/maj-agents.sh   # tout sauf les agents Windows x64
./Utilities/maj-agents.sh v1.7             # une release précise
DEST=/autre/content ./Utilities/maj-agents.sh   # autre dossier
```

Sans argument : la dernière release qui contient `Content.zip`. **`GARDER_WIN64=1`** évite de changer `Remotely-Win-x64.zip`, donc de déclencher la mise à jour de tous les postes Windows quand seules les autres plateformes changent.

### 5.3 Build local (sans GitHub)

Sur le poste de build Windows, à la racine du dépôt (mêmes commandes que la release, ici Windows x64 seul) :

```powershell
git pull
$V = (Get-Content .\VERSION -Raw) -replace '[^0-9.]', ''
$P = ".\Agent\bin\publish\win-x64"
Remove-Item $P -Recurse -Force -ErrorAction SilentlyContinue

dotnet publish /p:Version=$V /p:FileVersion=$V --runtime win-x64 --self-contained -c Release -o $P .\Agent
dotnet publish /p:Version=$V /p:FileVersion=$V -p:PublishProfile=packaged-win-x64 -c Release .\Desktop.Win
dotnet publish /p:Version=$V /p:FileVersion=$V -p:PublishProfile=desktop-win-x64 -c Release .\Desktop.Win
Compress-Archive -Path "$P\*" -DestinationPath .\Server\wwwroot\Content\Remotely-Win-x64.zip -Force
[IO.File]::WriteAllText("$PWD\Server\wwwroot\Content\AgentVersion.txt", $V)
```

Autres plateformes : `--runtime win-x86 | linux-x64 | osx-x64 | osx-arm64` pour l'agent, profils `packaged-win-x86` / `desktop-win-x86` (`Desktop.Win`) et `packaged-linux-x64` / `desktop-linux-x64` (`Desktop.Linux`). Tout sort dans `Server\wwwroot\Content`.

Copie sur le serveur :

```powershell
scp .\Server\wwwroot\Content\Remotely-Win-x64.zip root@<serveur>:/srv/remotely/content/
scp .\Server\wwwroot\Content\Win-x64\Remotely_Desktop.exe root@<serveur>:/srv/remotely/content/Win-x64/
scp .\Server\wwwroot\Content\AgentVersion.txt root@<serveur>:/srv/remotely/content/
```

`scp` est dans `C:\Program Files\Git\usr\bin\` s'il n'est pas dans le PATH.

### 5.4 Instance aux agents signés

Une instance qui sert ses propres agents signés ne passe **pas** par `maj-agents.sh` : il remplacerait les agents signés par ceux, non signés, de la release. À chaque release qui contient `Content.zip` :

1. **Serveur d'abord** ([§7](#7-mettre-à-jour-le-serveur)).
2. **Signer**, sur le poste Windows qui a le certificat de signature de code (magasin de l'utilisateur, `Cert:\CurrentUser\My`). Télécharger `Content.zip` depuis la page de la release, puis, dans le même dossier :

   ```powershell
   Expand-Archive .\Content.zip -DestinationPath .\content
   cd .\content
   powershell -File <dépôt>\Utilities\Sign-Remotely.ps1
   Move-Item .\out\Remotely-Win-x64.zip . -Force
   Move-Item .\out\Win-x64\Remotely_Desktop.exe .\Win-x64\ -Force
   Remove-Item .\out -Recurse
   cd ..
   ```

   `Sign-Remotely.ps1` fait la signature seul : il prend le certificat de signature de code le plus récent, décompresse `Remotely-Win-x64.zip`, signe les exe/dll qui ne le sont pas (le runtime .NET l'est déjà), recompresse le zip, signe `Win-x64\Remotely_Desktop.exe` et horodate le tout. Il s'arrête si une signature échoue.

   Restent non signés, c'est voulu : `Install-Remotely.ps1` (le serveur le modifie au téléchargement) et les agents x86, Linux et macOS.

3. **Déposer** sur le serveur. Depuis le poste :

   ```powershell
   scp -r .\content <serveur>:/tmp/content-signe
   ```

   Puis sur le serveur, en root (sauvegarde, puis remplacement) :

   ```bash
   C=/srv/remotely/content            # dossier monté sur /app/wwwroot/Content
   cp -a $C $C.bak-$(date +%Y%m%d-%H%M)
   cp -a /tmp/content-signe/. $C/ && rm -rf /tmp/content-signe
   ```

4. **Vérifier** : les postes se mettent à jour seuls dans les 6 h ([§8](#8-mettre-à-jour-les-agents)). Pour contrôler tout de suite, forcer un poste (**Mettre à jour l'agent**) : il revient sur la nouvelle version, sans alerte SmartScreen, et Propriétés → **Signatures numériques** de l'exécutable affiche le certificat.

Retour arrière : remettre `content.bak-<date>`.

## 6. Installer l'agent sur un poste client

- **Installation permanente** : sur le site, page **/deploy**, télécharger l'installeur de la plateforme (Windows x64/x86, Linux, macOS) et le lancer en administrateur (ou `sudo` pour les `.sh`). L'appareil apparaît dans la liste.
- **Assistance ponctuelle** : envoyer le client portable (`Remotely_Desktop.exe` sous Windows, `Remotely_Desktop` sous Linux) ; l'utilisateur le lance et communique le code de session.

## 7. Mettre à jour le serveur

Mêmes deux méthodes qu'à l'installation ([§3](#3-installer-le-serveur-première-fois)).

### 7.1 Méthode A : image depuis GitHub (recommandé)

**Publier** : changer le fichier `VERSION` (`X.Y` ou `X.Y.Z`) et pousser sur `main` :

```bash
echo 1.10 > VERSION
git commit -am "version 1.10"
git push
```

Si le tag `v1.10` n'existe pas encore, le workflow `.github/workflows/release.yml` (onglet **Actions**, ~20 min) :

- lance les contrôles de `build.yml` (zéro warning, zéro paquet vulnérable, tests, démarrage du serveur) : rien n'est publié s'ils échouent ;
- analyse l'image serveur avec Grype : une faille corrigeable de sévérité moyenne ou plus bloque la publication ;
- pousse l'image serveur `ghcr.io/jeremychn/amarre:1.10` (et `latest`) ;
- joint les agents à la release (`Content.zip`) seulement s'ils ont changé ([§5.1](#51-ce-que-produit-la-release)) ;
- crée la release et le tag `v1.10`.

Un push sans changement de version ne publie rien. Pousser un tag à la main (`git tag v1.10 && git push origin v1.10`) donne le même résultat.

**Déployer** sur le serveur (registre déjà connecté, voir [§3.1](#31-méthode-a--image-depuis-github-ghcrio)) :

```bash
docker pull ghcr.io/jeremychn/amarre:1.10
```

Puis basculer le conteneur :

- **Portainer** : Containers → `remotely` → **Duplicate/Edit** → registre **ghcr.io**, image `jeremychn/amarre:1.10` → **Deploy the container** → remplacer.
- **Compose** : changer le tag dans `docker-compose.yml`, puis `docker compose up -d`.

Si la release contient `Content.zip`, déployer aussi les agents avec `maj-agents.sh` ([§5.2](#52-déployer-sur-le-serveur)), ou en les signant d'abord sur une instance aux agents signés ([§5.4](#54-instance-aux-agents-signés)).

`latest` est possible dans le compose, mais il faut `docker compose pull` (ou « Always pull » dans Portainer) pour la récupérer, et le retour arrière demande de remettre un numéro de version. Préférer une version fixe en production.

### 7.2 Méthode B : build à la main sur le serveur

Pour un commit pas encore tagué, ou sans GitHub Actions :

```bash
cd ~/amarre
git pull origin main
git log --oneline -1                                   # vérifier le commit
rm -rf Server/bin/publish
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet publish Server/Server.csproj -c Release -o Server/bin/publish \
  -p:TypeScriptCompileBlocked=true
docker build -f Server/Dockerfile -t amarre:X.Y .
```

Puis basculer le conteneur :

- **Portainer** : Containers → `remotely` → **Duplicate/Edit** → image `amarre:X.Y`, décocher **Always pull the image** (image locale) → **Deploy the container** → remplacer.
- **Compose** : changer le tag dans `docker-compose.yml`, puis `docker compose up -d`.

### 7.3 Après la bascule (les deux méthodes)

- **Ctrl+F5** dans le navigateur pour recharger le JS du viewer.
- `docker ps --filter name=remotely` doit afficher la nouvelle image et `(healthy)`.

## 8. Mettre à jour les agents

1. Publier et déployer les agents ([§5.2](#52-déployer-sur-le-serveur), ou [§5.4](#54-instance-aux-agents-signés) pour une instance aux agents signés).
2. Les postes se mettent à jour **seuls** : au démarrage du service, à chaque reconnexion et toutes les 6 h, l'agent compare l'ETag de son zip (`Remotely-Win-x64.zip`, `Remotely-Linux.zip`…) et se réinstalle s'il a changé. C'est le changement du zip qui déclenche la mise à jour, pas le numéro de version.
   Témoin orange « agent obsolète » sur la carte : la version de l'agent diffère de celle des agents servis (`AgentVersion.txt` du dossier `content`). Sans ce fichier, aucun poste n'est signalé.
3. Pour forcer sur un poste : menu de la carte de l'appareil → **Mettre à jour l'agent**, ou redémarrer le service Remotely. Sur Windows, dans un PowerShell administrateur :
   ```powershell
   Restart-Service Remotely_Service
   ```
   À distance depuis un poste admin : `Invoke-Command -ComputerName <poste> { Restart-Service Remotely_Service }`. Linux : `sudo systemctl restart remotely-agent`. macOS : `sudo launchctl kickstart -k system/com.translucency.remotely-agent`.

Ordre : serveur d'abord, agents ensuite (le serveur accepte les anciens agents). Si une modification touche à la fois l'agent et le viewer, déployer les deux : un ancien agent avec un nouveau viewer peut mal se comporter (ex. Ctrl+V qui colle l'ancien contenu).

## 9. Retour arrière

- **Serveur** : rebasculer le conteneur sur l'image précédente (Duplicate/Edit ou tag dans le compose). Données, clés et agents sont sur l'hôte : rien d'autre à faire. Avec la méthode A, toutes les versions restent sur ghcr.io : `docker pull ghcr.io/jeremychn/amarre:<ancienne version>` si elle n'est plus sur le serveur.
- **Agents** : remettre la sauvegarde `content.bak-<date>` créée par `maj-agents.sh` (ou `./Utilities/maj-agents.sh <ancien tag>`) ; les postes se réinstallent avec.

## 10. Modifier le code

- **Viewer** (`Server/wwwroot/src/*.ts`) : les `.js` sont versionnés. Après modification :
  ```bash
  npx -y -p typescript@5.6.2 tsc -p Server/wwwroot/src/tsconfig.json
  ```
  puis commiter `.ts`, `.js` et `.js.map`. Mise en prod : [§7](#7-mettre-à-jour-le-serveur).
- **Agent / Desktop** (`Agent`, `Desktop.*`, `Shared`) : monter `VERSION`, la release recompile les agents ; les déployer ([§5](#5-publier-et-déployer-les-agents)).
- **Serveur** (`Server`, `Shared`) : [§7](#7-mettre-à-jour-le-serveur).
- **Pull request et push sur `main`** : la CI (`.github/workflows/build.yml`) refuse le moindre warning, paquet vulnérable ou faille corrigeable de sévérité moyenne ou plus dans l'image (Grype), lance les tests, démarre le serveur (migration SQLite) et teste inscription + connexion (`Utilities/test-login.sh`). Fusionner seulement au vert.

## 11. Dépannage

| Symptôme | Piste |
|---|---|
| `dotnet publish` : « node could not be run » | Ajouter `-p:TypeScriptCompileBlocked=true` |
| Installation de l'agent en 404 | Le zip ou le script `Install-*` n'est pas dans `$DATA/content` |
| `maj-agents.sh` : « Release introuvable » | Tag mal saisi (`v1.15`, pas `1.15`) |
| `maj-agents.sh` : « Aucune release avec Content.zip » | Lancer la workflow à la main (Actions → Release → Run workflow) |
| `@`, `#`, `{` ou pavé numérique sans effet ou faux sur le poste distant | Serveur antérieur à v1.6, ou ancien JS en cache (Ctrl+F5) |
| Poste qui ne revient plus, journal serveur « Jeton d'agent absent ou faux » | Depuis v1.16, un poste passé en agent 1.16+ doit présenter son jeton. Agent réinstallé (jeton perdu) : supprimer l'appareil dans l'interface, il revient au prochain démarrage du service |
| Tout le monde déconnecté après redémarrage | Volume `keys` absent |
| Mauvaise URL ou http dans les installeurs | En-têtes `X-Forwarded-*`, `KnownProxies`, `DockerGatewayIp`, ou `Force Client HTTPS` |
| Ctrl+V ne fait rien | Site pas en HTTPS, ou ancien JS en cache (Ctrl+F5) |
| Ctrl+V colle l'ancien contenu | Agent pas à jour |
| Log agent « Clipboard is null » | Agent antérieur au correctif presse-papier : le mettre à jour |
| Ctrl+C distant n'arrive pas (Firefox) | Cliquer ou appuyer sur une touche dans le viewer : le texte est écrit à la prochaine action |
| `SQLite Error 13: database or disk is full`, disque plein par vagues alors que `du` ne trouve rien | Serveur antérieur à v1.13 : fichiers temporaires de SQLite supprimés mais encore ouverts (`ls -l /proc/$(docker inspect -f '{{.State.Pid}}' remotely)/fd \| grep deleted`). `docker restart remotely` libère l'espace ; passer en v1.13 corrige |
| Un poste se connecte/déconnecte en boucle | DeviceID en double : régénérer le DeviceID du poste |
| Conteneur de test : connexion impossible, aucun appareil | `Remotely_ConnectionStrings__SQLite` absente : le serveur a créé une base vide dans l'image au lieu d'utiliser `/app/AppData/Remotely.db` |
| Fichiers `Server/wwwroot/lib/*.js` modifiés après un build | Régénérés par libman au build : `git checkout -- Server/wwwroot/lib` |

**Logs**

- Postes Windows : `%ProgramData%\Remotely\Logs\Remotely_Agent` et `Remotely_Desktop`
- Serveur : `$DATA/logs` et `docker logs remotely`
- Tracer toutes les requêtes HTTP (proxy, SignalR) : `UseHttpLogging` dans Server Config, puis redémarrer le conteneur.

## 12. Référence : configuration

Base de données, chaînes de connexion et port : variables d'environnement du conteneur. Le reste se règle dans **Server Config** :

| Paramètre | Effet |
|---|---|
| `Require2FA` | 2FA obligatoire |
| `DataRetentionInDays` | Conservation des logs et données, 90 jours par défaut (-1 = illimité, déconseillé) |
| `EnforceAttendedAccess` | L'utilisateur doit accepter chaque prise en main |
| `RemoteControlNotifyUser` | Notification au début d'une session sans surveillance |
| `RemoteControlRequiresAuthentication` | Le viewer exige d'être connecté |
| `EnableRemoteControlRecording` | Enregistre les sessions dans `/app/AppData/recordings` |
| `BannedDevices` | ID, noms ou IP à bannir (désinstallation immédiate) |
| `KnownProxies` | IP des reverse proxies autorisés à envoyer les `X-Forwarded-*` |
| `ForceClientHttps` | Force https dans les installeurs et clients |
| `MaxOrganizationCount` | 1 par défaut ; -1 pour plusieurs organisations |
| `UseHttpLogging` | Journalise toutes les requêtes HTTP |
| `Smtp*` | Envoi des e-mails (inscription, mot de passe) |

**API** : documentation Swagger (`/swagger`) servie seulement en développement (`ASPNETCORE_ENVIRONMENT=Development`), jamais en production. Authentification par en-tête `X-Api-Key: <ID>:<secret>` (jeton créé dans Compte → Accès API).

**Console de scripts** : `/` change de shell, Haut/Bas pour l'historique, Ctrl+Q vide la sortie.
