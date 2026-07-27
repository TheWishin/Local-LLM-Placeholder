# Anonymisierungs-Gateway als Container – für den zentralen Betrieb auf einem
# haus-internen Server. Build-Kontext ist das Repo-Wurzelverzeichnis:
#   docker build -f src/DataAnonymizer.Proxy/Dockerfile -t dataanonymizer-gateway .
#   docker run -p 8080:8080 dataanonymizer-gateway

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Zuerst nur die Projektdateien kopieren – bessere Layer-Zwischenspeicherung beim Restore.
COPY src/DataAnonymizer.Core/DataAnonymizer.Core.csproj src/DataAnonymizer.Core/
COPY src/DataAnonymizer.Proxy/DataAnonymizer.Proxy.csproj src/DataAnonymizer.Proxy/
RUN dotnet restore src/DataAnonymizer.Proxy/DataAnonymizer.Proxy.csproj

COPY src/ src/
RUN dotnet publish src/DataAnonymizer.Proxy/DataAnonymizer.Proxy.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# Im Container immer auf allen Adressen lauschen (Port-Mapping übernimmt Docker).
ENV ASPNETCORE_URLS=http://+:8080
ENV ANONYMIZER_UPSTREAM=https://api.anthropic.com
ENV ANONYMIZER_LANGUAGE=En

EXPOSE 8080
ENTRYPOINT ["dotnet", "DataAnonymizer.Proxy.dll"]
