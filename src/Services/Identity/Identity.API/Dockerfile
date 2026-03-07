FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore (layer caching)
COPY src/Services/Identity/Identity.API/Identity.API.csproj Services/Identity/Identity.API/
RUN dotnet restore Services/Identity/Identity.API/Identity.API.csproj

# Copy source and build
COPY src/Services/Identity/Identity.API/ Services/Identity/Identity.API/
RUN dotnet publish Services/Identity/Identity.API/Identity.API.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Identity.API.dll"]
