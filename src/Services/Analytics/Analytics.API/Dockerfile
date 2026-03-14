FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore (layer caching)
COPY src/BuildingBlocks/BuildingBlocks.Common/BuildingBlocks.Common.csproj BuildingBlocks/BuildingBlocks.Common/
COPY src/Services/Analytics/Analytics.API/Analytics.API.csproj Services/Analytics/Analytics.API/
RUN dotnet restore Services/Analytics/Analytics.API/Analytics.API.csproj

# Copy source and build
COPY src/BuildingBlocks/BuildingBlocks.Common/ BuildingBlocks/BuildingBlocks.Common/
COPY src/Services/Analytics/Analytics.API/ Services/Analytics/Analytics.API/
RUN dotnet publish Services/Analytics/Analytics.API/Analytics.API.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Analytics.API.dll"]
