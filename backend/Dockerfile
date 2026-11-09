# UniversityLeaders API. Build context: backend/  (docker build -f backend/Dockerfile backend)
# Pinned to the patched .NET 10.0.14 runtime / SDK 10.0.403 (security release 2026-10-25). Bump both on every .NET
# Patch Tuesday: https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json

FROM mcr.microsoft.com/dotnet/sdk:10.0.403-noble AS build
WORKDIR /src
COPY UniversityLeaders.slnx ./
COPY src/UniversityLeaders.Domain/UniversityLeaders.Domain.csproj src/UniversityLeaders.Domain/
COPY src/UniversityLeaders.Application/UniversityLeaders.Application.csproj src/UniversityLeaders.Application/
COPY src/UniversityLeaders.Infrastructure/UniversityLeaders.Infrastructure.csproj src/UniversityLeaders.Infrastructure/
COPY src/UniversityLeaders.Api/UniversityLeaders.Api.csproj src/UniversityLeaders.Api/
RUN dotnet restore src/UniversityLeaders.Api/UniversityLeaders.Api.csproj
COPY src/ src/
RUN dotnet publish src/UniversityLeaders.Api/UniversityLeaders.Api.csproj -c Release --no-restore -o /app /p:UseAppHost=false
# Migrations as a self-contained executable (run by Render's pre-deploy command; see docs/DEPLOYMENT.md).
RUN dotnet tool install dotnet-ef --version 10.0.14 --tool-path /tools \
 && /tools/dotnet-ef migrations bundle --configuration Release \
      --project src/UniversityLeaders.Infrastructure --startup-project src/UniversityLeaders.Api --output /app/efbundle

# Ubuntu (not chiseled) on purpose: the pre-deploy migration and the offline recovery command need a shell.
FROM mcr.microsoft.com/dotnet/aspnet:10.0.14-noble AS runtime
WORKDIR /app
COPY --from=build /app ./
# Development settings are never used in Production; keep them out of the image.
RUN rm -f appsettings.Development.json
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# Built-in non-root "app" user of the .NET images.
USER $APP_UID
ENTRYPOINT ["dotnet", "UniversityLeaders.Api.dll"]
