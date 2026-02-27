FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/BuildingBlocks/BuildingBlocks.Common/BuildingBlocks.Common.csproj BuildingBlocks/BuildingBlocks.Common/
COPY src/Services/Metrics/Metrics.API/Metrics.API.csproj Services/Metrics/Metrics.API/
RUN dotnet restore Services/Metrics/Metrics.API/Metrics.API.csproj

COPY src/BuildingBlocks/BuildingBlocks.Common/ BuildingBlocks/BuildingBlocks.Common/
COPY src/Services/Metrics/Metrics.API/ Services/Metrics/Metrics.API/
RUN dotnet publish Services/Metrics/Metrics.API/Metrics.API.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Metrics.API.dll"]
