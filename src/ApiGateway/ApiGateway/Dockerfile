FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/ApiGateway/ApiGateway/ApiGateway.csproj ApiGateway/ApiGateway/
RUN dotnet restore ApiGateway/ApiGateway/ApiGateway.csproj

COPY src/ApiGateway/ApiGateway/ ApiGateway/ApiGateway/
RUN dotnet publish ApiGateway/ApiGateway/ApiGateway.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "ApiGateway.dll"]
