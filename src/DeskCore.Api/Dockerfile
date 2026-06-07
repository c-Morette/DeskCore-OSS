# syntax=docker/dockerfile:1
# Build context = raiz da solução (definido no docker-compose).

# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore em camada cacheável: copia metadados + csproj antes do código.
COPY global.json Directory.Build.props ./
COPY src/DeskCore.Domain/DeskCore.Domain.csproj                 src/DeskCore.Domain/
COPY src/DeskCore.Shared/DeskCore.Shared.csproj                 src/DeskCore.Shared/
COPY src/DeskCore.Application/DeskCore.Application.csproj        src/DeskCore.Application/
COPY src/DeskCore.Infrastructure/DeskCore.Infrastructure.csproj src/DeskCore.Infrastructure/
COPY src/DeskCore.Api/DeskCore.Api.csproj                       src/DeskCore.Api/
RUN dotnet restore src/DeskCore.Api/DeskCore.Api.csproj

# Código + publish (sem --no-restore; ver nota no Dockerfile da Web sobre static web assets).
COPY src/ src/
RUN dotnet publish src/DeskCore.Api/DeskCore.Api.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime ----
# aspnet padrão (Debian) -> traz ICU, necessário para CultureInfo("pt-BR").
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Pontos de montagem (volumes do compose).
RUN mkdir -p /app/uploads /app/keys /app/logs

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENTRYPOINT ["dotnet", "DeskCore.Api.dll"]
