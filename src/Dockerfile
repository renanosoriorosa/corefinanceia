FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

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
