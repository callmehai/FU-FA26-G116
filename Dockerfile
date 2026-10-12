# One image holding the backend and the frontend build, the same shape as `npm run build` +
# `dotnet run` locally: ASP.NET Core serves the SPA from wwwroot, so there is still one origin
# and no CORS. Used by deploy/docker-compose.yml — see deploy/README.md.

# 1. Frontend. vite.config.ts writes to ../backend/TrustCabinet.Web/wwwroot, so keep that layout.
FROM node:22-alpine AS frontend
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# 2. Backend.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY global.json TrustCabinet.sln ./
COPY backend/ backend/
RUN dotnet publish backend/TrustCabinet.Web -c Release -o /app
COPY --from=frontend /src/backend/TrustCabinet.Web/wwwroot /app/wwwroot

# 3. Runtime.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=backend /app ./
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "TrustCabinet.Web.dll"]
