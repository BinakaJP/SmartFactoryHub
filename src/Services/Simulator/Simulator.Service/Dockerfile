FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/BuildingBlocks/BuildingBlocks.Common/BuildingBlocks.Common.csproj BuildingBlocks/BuildingBlocks.Common/
COPY src/Services/Simulator/Simulator.Service/Simulator.Service.csproj Services/Simulator/Simulator.Service/
RUN dotnet restore Services/Simulator/Simulator.Service/Simulator.Service.csproj

COPY src/BuildingBlocks/BuildingBlocks.Common/ BuildingBlocks/BuildingBlocks.Common/
COPY src/Services/Simulator/Simulator.Service/ Services/Simulator/Simulator.Service/
RUN dotnet publish Services/Simulator/Simulator.Service/Simulator.Service.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Simulator.Service.dll"]
