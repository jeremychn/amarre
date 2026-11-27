# Image de base épinglée (version et digest) : Dependabot (docker) monte les deux ensemble
FROM mcr.microsoft.com/dotnet/aspnet:10.0.15@sha256:aac50ceef10391e78a899e3d2ccd00bbafa2dcb56ccf4586cbccf00edb84fc21

# Rattache l'image ghcr au dépôt (page du paquet, visibilité)
LABEL org.opencontainers.image.source=https://github.com/jeremychn/amarre \
      org.opencontainers.image.description="Amarre : prise en main et scripts à distance, auto-hébergés" \
      org.opencontainers.image.licenses=GPL-3.0-only

EXPOSE ${ASPNETCORE_HTTP_PORTS}

# upgrade : correctifs de sécurité publiés depuis l'image de base (analyse Grype de la CI)
RUN apt-get update && apt-get -y upgrade && apt-get -y install --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --chown=app:app Server/bin/publish /app

# Clés DataProtection dans le volume AppData (indépendant du HOME) : conservées au passage en non-root.
ENV Remotely_ApplicationOptions__DataProtectionKeyDir=/app/AppData/DataProtection-Keys

# Dossiers écrits à l'exécution : possédés par l'utilisateur non-root. Un volume neuf monté dessus hérite de
# cette possession ; un volume existant (données déjà présentes) reste à son propriétaire -> chown unique côté hôte.
RUN mkdir -p /app/AppData/DataProtection-Keys /app/wwwroot/Content \
    && chown -R app:app /app/AppData /app/wwwroot/Content

WORKDIR /app

# Sécurité : l'application ne tourne pas en root (utilisateur « app », UID 1654, fourni par l'image .NET).
USER app

ENTRYPOINT ["dotnet", "Remotely_Server.dll"]

HEALTHCHECK \
  CMD curl -f http://localhost:${ASPNETCORE_HTTP_PORTS}/api/healthcheck || exit 1