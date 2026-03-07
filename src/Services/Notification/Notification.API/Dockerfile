FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore (layer caching)
COPY src/BuildingBlocks/BuildingBlocks.Common/BuildingBlocks.Common.csproj BuildingBlocks/BuildingBlocks.Common/
COPY src/Services/Notification/Notification.API/Notification.API.csproj Services/Notification/Notification.API/
RUN dotnet restore Services/Notification/Notification.API/Notification.API.csproj

# Copy source and build
COPY src/BuildingBlocks/BuildingBlocks.Common/ BuildingBlocks/BuildingBlocks.Common/
COPY src/Services/Notification/Notification.API/ Services/Notification/Notification.API/
RUN dotnet publish Services/Notification/Notification.API/Notification.API.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Notification.API.dll"]
