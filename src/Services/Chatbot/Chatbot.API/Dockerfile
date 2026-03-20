FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore (layer caching)
COPY src/Services/Chatbot/Chatbot.API/Chatbot.API.csproj Services/Chatbot/Chatbot.API/
RUN dotnet restore Services/Chatbot/Chatbot.API/Chatbot.API.csproj

# Copy source and build
COPY src/Services/Chatbot/Chatbot.API/ Services/Chatbot/Chatbot.API/
RUN dotnet publish Services/Chatbot/Chatbot.API/Chatbot.API.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Chatbot.API.dll"]
