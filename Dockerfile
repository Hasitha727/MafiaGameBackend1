# Stage 1: Build ReactJS + Bootstrap + Installable PWA Frontend
FROM node:22-slim AS frontend-build
WORKDIR /app/client
COPY client/package.json ./
RUN npm install --no-audit --no-fund
COPY client/index.html client/vite.config.js ./
COPY client/public ./public
COPY client/src ./src
RUN npm run build

# Stage 2: Build ASP.NET Core 10 Web API + SignalR Backend
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /app

COPY MafiaGameBackend.slnx ./
COPY src/MafiaGame.Domain/*.csproj ./src/MafiaGame.Domain/
COPY src/MafiaGame.Application/*.csproj ./src/MafiaGame.Application/
COPY src/MafiaGame.Infrastructure/*.csproj ./src/MafiaGame.Infrastructure/
COPY src/MafiaGame.API/*.csproj ./src/MafiaGame.API/
COPY tests/MafiaGame.Tests/*.csproj ./tests/MafiaGame.Tests/

RUN dotnet restore MafiaGameBackend.slnx

COPY . .
# Copy fresh production React PWA build into ASP.NET Core wwwroot
COPY --from=frontend-build /app/src/MafiaGame.API/wwwroot ./src/MafiaGame.API/wwwroot

RUN dotnet publish src/MafiaGame.API/MafiaGame.API.csproj -c Release -o /out

# Stage 3: Production Runtime Image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=backend-build /out .

ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

ENTRYPOINT ["dotnet", "MafiaGame.API.dll"]
