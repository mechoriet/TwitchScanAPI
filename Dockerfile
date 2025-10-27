FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["TwitchScanAPI/TwitchScanAPI.csproj", "TwitchScanAPI/"]
RUN dotnet restore "TwitchScanAPI/TwitchScanAPI.csproj"
COPY . .
WORKDIR "/src/TwitchScanAPI"
RUN dotnet build "TwitchScanAPI.csproj" -c $BUILD_CONFIGURATION -o /app/build

RUN dotnet publish "TwitchScanAPI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime


# Install security updates and curl for health checks
RUN apk update && apk upgrade && \
    apk add --no-cache curl && \
    rm -rf /var/cache/apk/* \
    
WORKDIR /app
COPY --from=build /app/publish .

# Core runtime settings
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true
ENV DOTNET_EnableDiagnostics=0

# Memory and JIT optimizations
ENV DOTNET_TieredPGO=1
ENV DOTNET_ReadyToRun=1
ENV DOTNET_TC_QuickJit=1
ENV DOTNET_TC_QuickJitForLoops=1
ENV DOTNET_AggressiveOptimization=1

# Security hardening
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
# Health check
#HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
#    CMD wget --no-verbose --tries=1 --spider http://localhost:5001/health || exit 1
    
ENTRYPOINT ["dotnet", "TwitchScanAPI.dll"]
