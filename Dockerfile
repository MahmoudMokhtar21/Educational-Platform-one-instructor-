# syntax=docker/dockerfile:1

# ============================================================
# Build stage — .NET 8 SDK publishes the backend + bundled SPA
# (wwwroot is part of the Web SDK project and ships with publish)
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only the project file first so the NuGet restore layer is cached
# until package references change.
COPY ["Educatinal_Platform.csproj", "./"]
RUN dotnet restore "Educatinal_Platform.csproj"

# Copy the rest of the source (see .dockerignore for exclusions) and publish.
COPY . .
RUN dotnet publish "Educatinal_Platform.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

# ============================================================
# Runtime stage — production ASP.NET Core runtime only
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 10000

COPY --from=build /app/publish .

# Render injects a dynamic $PORT per deploy; bind all interfaces and
# fall back to 10000 (Render's default HTTP port) when it is not set.
ENTRYPOINT ["sh", "-c", "dotnet Educatinal_Platform.dll --urls http://0.0.0.0:${PORT:-10000}"]
