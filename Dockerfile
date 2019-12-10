FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /src
COPY ["IotHubSync.Service/IotHubSync.Service.csproj", "/IotHubSync.Service/"]
COPY ["IotHubSync.Logic/IotHubSync.Logic.csproj", "/IotHubSync.Logic/"]
RUN dotnet restore "/IotHubSync.Service/IotHubSync.Service.csproj"
COPY . .
WORKDIR "/src/IotHubSync.Service"
RUN dotnet build "IotHubSync.Service.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "IotHubSync.Service.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "IotHubSync.Service.dll"]