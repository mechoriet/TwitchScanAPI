FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["TwitchScanAPI/TwitchScanAPI.csproj", "TwitchScanAPI/"]
RUN dotnet restore "TwitchScanAPI/TwitchScanAPI.csproj"
COPY . .
WORKDIR "/src/TwitchScanAPI"
RUN dotnet build "TwitchScanAPI.csproj" -c $BUILD_CONFIGURATION -o /app/build --runtime linux-musl-x64

RUN dotnet publish "TwitchScanAPI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime


# Security updates + required native deps
RUN apk update && apk upgrade && \
    apk add --no-cache \
        curl \
        libssl3 \
        libstdc++ \
        zlib \
        icu-libs && \
    rm -rf /var/cache/apk/*
    
WORKDIR /app
COPY --from=build /app/publish .

ENV DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_TieredPGO=1 \
    DOTNET_ReadyToRun=1 \
    DOTNET_TC_QuickJitForLoops=1

# Security hardening
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
    
ENTRYPOINT ["dotnet", "TwitchScanAPI.dll"]
