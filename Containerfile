ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS restore
WORKDIR /workspace

COPY Directory.Build.props Directory.Packages.props Frontier10052.slnx ./
COPY src/Frontier10052.Content/Frontier10052.Content.csproj src/Frontier10052.Content/
COPY src/Frontier10052.Domain/Frontier10052.Domain.csproj src/Frontier10052.Domain/
COPY src/Frontier10052.Gameplay/Frontier10052.Gameplay.csproj src/Frontier10052.Gameplay/
COPY src/Frontier10052.Infrastructure/Frontier10052.Infrastructure.csproj src/Frontier10052.Infrastructure/
COPY src/Frontier10052.Simulation/Frontier10052.Simulation.csproj src/Frontier10052.Simulation/
COPY src/Frontier10052.Web/Frontier10052.Web.csproj src/Frontier10052.Web/
COPY tests/Frontier10052.Domain.Tests/Frontier10052.Domain.Tests.csproj tests/Frontier10052.Domain.Tests/
COPY tests/Frontier10052.IntegrationTests/Frontier10052.IntegrationTests.csproj tests/Frontier10052.IntegrationTests/
COPY tests/Frontier10052.Simulation.Tests/Frontier10052.Simulation.Tests.csproj tests/Frontier10052.Simulation.Tests/
RUN dotnet restore Frontier10052.slnx

FROM restore AS build
COPY . .
RUN dotnet build Frontier10052.slnx --configuration Release --no-restore

FROM build AS test
ENTRYPOINT ["dotnet", "test", "Frontier10052.slnx", "--configuration", "Release", "--no-build", "--logger", "console;verbosity=normal"]

FROM build AS publish
RUN dotnet publish src/Frontier10052.Web/Frontier10052.Web.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
ENV Frontier10052__SavesDirectory=/data
EXPOSE 8080
COPY --from=publish /app/publish .
USER root
RUN mkdir -p /data && chown "$APP_UID:$APP_UID" /data
USER $APP_UID
ENTRYPOINT ["dotnet", "Frontier10052.Web.dll"]
