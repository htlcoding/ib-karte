# Baut die C#-API. Build-Kontext ist das Wurzelverzeichnis des Repos (wegen seed/).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/src/IbKarte.Api/IbKarte.Api.csproj backend/src/IbKarte.Api/
RUN dotnet restore backend/src/IbKarte.Api/IbKarte.Api.csproj
COPY backend/ backend/
RUN dotnet publish backend/src/IbKarte.Api/IbKarte.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
COPY seed/ /seed/
ENV ASPNETCORE_URLS=http://+:8080 \
    Import__SeedVerzeichnis=/seed \
    Import__Datei=/daten/netzwerk.json
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "IbKarte.Api.dll"]
