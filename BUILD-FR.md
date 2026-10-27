# Reconstruire l'image serveur (fork FR)

Méthode de secours : normalement l'image est publiée par GitHub (fichier `VERSION` monté sur `main`, voir README §7.1). Les agents ne sont pas dans l'image (README §5).

Le `Server/Dockerfile` copie `Server/bin/publish` : on publie d'abord avec le SDK .NET 10, puis on construit l'image.
Adapter `~/amarre` (clone du dépôt) et `/chemin/compose` (dossier du docker-compose) au serveur.

```bash
# 1. Récupérer le code
# Première fois : git clone https://github.com/jeremychn/amarre.git ~/amarre
cd ~/amarre
git pull origin main
git log --oneline -1

# 2. Publier le serveur dans un conteneur SDK .NET 10
rm -rf Server/bin/publish
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet publish Server/Server.csproj -c Release -o Server/bin/publish \
  -p:TypeScriptCompileBlocked=true   # obligatoire : pas de Node dans l'image SDK

# 3. Construire l'image avec un nouveau tag (garder l'ancien pour le retour arrière)
docker build -f Server/Dockerfile -t amarre:X.Y .

# 4. Basculer
cd /chemin/compose
sed -i 's/amarre:ANCIEN/amarre:X.Y/' docker-compose.yml
docker compose up -d
docker compose logs -f --tail 50
```

Notes :
- Les fichiers publiés sont créés par root (`chown -R` si besoin).
- Après bascule : Ctrl+F5 dans le navigateur pour recharger le JS du viewer.
- Retour arrière : remettre l'ancien tag dans le compose, puis `docker compose up -d`.
- Les `.js` du viewer (`Server/wwwroot/src`) sont versionnés : après modification d'un `.ts`, recompiler avec
  `npx -p typescript@5.6.2 tsc -p Server/wwwroot/src/tsconfig.json` et commiter les `.js`/`.js.map`.
