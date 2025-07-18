# Use the official .NET 8 runtime as the base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443
EXPOSE 8080
EXPOSE 8081

# Use the official .NET 8 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["src/GpsTelemetryServer/GpsTelemetryServer.csproj", "src/GpsTelemetryServer/"]
COPY ["src/Core/Core.csproj", "src/Core/"]
COPY ["src/Infrastructure/Infrastructure.csproj", "src/Infrastructure/"]
COPY ["src/Plugins/PluginFramework/PluginFramework.csproj", "src/Plugins/PluginFramework/"]
COPY ["src/Plugins/NmeaPlugin/NmeaPlugin.csproj", "src/Plugins/NmeaPlugin/"]

# Restore dependencies
RUN dotnet restore "src/GpsTelemetryServer/GpsTelemetryServer.csproj"

# Copy all source code
COPY . .
WORKDIR "/src/src/GpsTelemetryServer"

# Build the application
RUN dotnet build "GpsTelemetryServer.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "GpsTelemetryServer.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Create the final runtime image
FROM base AS final
WORKDIR /app

# Create logs directory
RUN mkdir -p /app/logs

# Create plugins directory
RUN mkdir -p /app/plugins

# Copy published application
COPY --from=publish /app/publish .

# Copy plugins to the runtime image
COPY --from=publish /app/publish/plugins /app/plugins

# Set environment variables for production
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_gcServer=1
ENV DOTNET_gcConcurrent=1
ENV TELEMETRY_TelemetryServer__TcpPort=8080
ENV TELEMETRY_TelemetryServer__UdpPort=8081

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost/health || exit 1

ENTRYPOINT ["dotnet", "GpsTelemetryServer.dll"]