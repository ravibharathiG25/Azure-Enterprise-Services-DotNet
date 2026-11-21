# Base runtime image for Azure App Service Linux
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081
ENV ASPNETCORE_URLS=http://+:8080

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["AzureServices.Core/AzureServices.Core.csproj", "AzureServices.Core/"]
COPY ["AzureServices.Infrastructure/AzureServices.Infrastructure.csproj", "AzureServices.Infrastructure/"]
COPY ["AzureServices.AppService.Api/AzureServices.AppService.Api.csproj", "AzureServices.AppService.Api/"]

RUN dotnet restore "AzureServices.AppService.Api/AzureServices.AppService.Api.csproj"
COPY . .
WORKDIR "/src/AzureServices.AppService.Api"
RUN dotnet build "AzureServices.AppService.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "AzureServices.AppService.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final production stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AzureServices.AppService.Api.dll"]
