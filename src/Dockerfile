FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
# A imagem aspnet:8.0 nao traz curl nem wget; sem isso o HEALTHCHECK
# ficaria eternamente unhealthy, sem mensagem util.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*
# Usa /health/live, nao /health: se o SQL Server cair, reiniciar a API nao resolve.
HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
  CMD curl -f http://localhost:8080/health/live || exit 1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["CoreFinance.API/CoreFinance.API.csproj", "CoreFinance.API/"]
COPY ["CoreFinance.Application/CoreFinance.Application.csproj", "CoreFinance.Application/"]
COPY ["CoreFinance.Domain/CoreFinance.Domain.csproj", "CoreFinance.Domain/"]
COPY ["CoreFinance.Infra/CoreFinance.Infra.csproj", "CoreFinance.Infra/"]
RUN dotnet restore "CoreFinance.API/CoreFinance.API.csproj"
COPY . .
RUN dotnet publish "CoreFinance.API/CoreFinance.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CoreFinance.API.dll"]
