FROM mcr.microsoft.com/dotnet/aspnet:10.0

# Rattache l'image ghcr au dépôt (page du paquet, visibilité)
LABEL org.opencontainers.image.source=https://github.com/jeremychn/amarre \
      org.opencontainers.image.description="Amarre : prise en main et scripts à distance, auto-hébergés" \
      org.opencontainers.image.licenses=GPL-3.0-only

EXPOSE ${ASPNETCORE_HTTP_PORTS}

# upgrade : correctifs de sécurité publiés depuis l'image de base (analyse Grype de la CI)
RUN apt-get update && apt-get -y upgrade && apt-get -y install --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY Server/bin/publish /app

WORKDIR /app

ENTRYPOINT ["dotnet", "Remotely_Server.dll"]

HEALTHCHECK \
  CMD curl -f http://localhost:${ASPNETCORE_HTTP_PORTS}/api/healthcheck || exit 1