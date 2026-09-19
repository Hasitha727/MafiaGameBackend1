# Multi-stage Dockerfile for ASP.NET Core 10 Web API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy solution and csproj files
COPY MafiaGameBackend.slnx ./
COPY src/MafiaGame.Domain/*.csproj ./src/MafiaGame.Domain/
COPY src/MafiaGame.Application/*.csproj ./src/MafiaGame.Application/
COPY src/MafiaGame.Infrastructure/*.csproj ./src/MafiaGame.Infrastructure/
COPY src/MafiaGame.API/*.csproj ./src/MafiaGame.API/
COPY tests/MafiaGame.Tests/*.csproj ./tests/MafiaGame.Tests/

# Restore dependencies
RUN dotnet restore MafiaGameBackend.slnx

# Copy source code and build
COPY . .
RUN dotnet publish src/MafiaGame.API/MafiaGame.API.csproj -c Release -o /out

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .

ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

ENTRYPOINT ["dotnet", "MafiaGame.API.dll"]
