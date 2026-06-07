# syntax=docker/dockerfile:1
# Build context = raiz da solução (definido no docker-compose).

# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore em camada cacheável: copia metadados + csproj antes do código.
COPY global.json Directory.Build.props ./
COPY src/DeskCore.Domain/DeskCore.Domain.csproj src/DeskCore.Domain/
COPY src/DeskCore.Shared/DeskCore.Shared.csproj src/DeskCore.Shared/
COPY src/DeskCore.Web/DeskCore.Web.csproj       src/DeskCore.Web/
RUN dotnet restore src/DeskCore.Web/DeskCore.Web.csproj

# Código + publish.
# NOTA: o publish NÃO usa --no-restore de propósito. Com restore feito só a partir dos
# .csproj (otimização de cache), o publish --no-restore perde os static web assets do
# framework (ex.: _framework/blazor.web.js, do pacote microsoft.aspnetcore.app.internal.assets),
# o que quebra a interatividade do Blazor. Com o código já presente, o publish refaz o
# restore (rápido, pacotes já em cache da camada anterior) e compõe os assets corretamente.
COPY src/ src/
RUN dotnet publish src/DeskCore.Web/DeskCore.Web.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime ----
# aspnet padrão (Debian) -> traz ICU, necessário para CultureInfo("pt-BR").
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENTRYPOINT ["dotnet", "DeskCore.Web.dll"]
