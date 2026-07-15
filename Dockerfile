# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release

ENV HUSKY=0

RUN apt-get update && \
    apt-get install -y --no-install-recommends curl ffmpeg && \
    curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && \
    apt-get install -y --no-install-recommends nodejs && \
    npm install -g yarn && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /src

# Copy solution-level files for NuGet restore caching
COPY Directory.Packages.props .
COPY MARS.Projects/MARS.Server/MARS.Server.csproj MARS.Projects/MARS.Server/

RUN dotnet restore MARS.Projects/MARS.Server/MARS.Server.csproj \
    -p:UseLocalYoutubeReExplode=false \
    -p:RunTestsOnPublish=false \
    -p:SkipHusky=true

# Copy full source
COPY . .

# Publish (SPA build is handled by MSBuild target BuildClientApp)
WORKDIR /src/MARS.Projects/MARS.Server
RUN dotnet publish MARS.Server.csproj \
    -c ${BUILD_CONFIGURATION} \
    -o /app/publish \
    -p:UseLocalYoutubeReExplode=false \
    -p:RunTestsOnPublish=false \
    -p:SkipHusky=true \
    -p:SkipBuildClient=true

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

RUN apt-get update && \
    apt-get install -y --no-install-recommends ffmpeg && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 9155

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:9155
ENV DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT ["dotnet", "MARS.Server.dll"]
