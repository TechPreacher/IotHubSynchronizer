FROM mcr.microsoft.com/dotnet/core/aspnet:2.2-stretch-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/core/sdk:2.2-stretch AS build
WORKDIR /src
COPY ["IotHubSync/IotHubSync.Service/IotHubSync.Service.csproj", "IotHubSync/IotHubSync.Service/"]
COPY ["IotHubSync/IotHubSync.Logic/IotHubSync.Logic.csproj", "IotHubSync/IotHubSync.Logic/"]
RUN dotnet restore "IotHubSync/IotHubSync.Service/IotHubSync.Service.csproj"
COPY . .
WORKDIR "/src/IotHubSync/IotHubSync.Service"
RUN dotnet build "IotHubSync.Service.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "IotHubSync.Service.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "IotHubSync.Service.dll"]