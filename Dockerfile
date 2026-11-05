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
# A portable publish carries QuestPDF's native library (the PDF minutes) for every OS, about 85 MB. The image runs on
# Linux only: keep x64 (the server) and arm64 (a build on an Apple Silicon laptop), drop the rest.
RUN find /app/runtimes -mindepth 1 -maxdepth 1 -type d ! -name linux-x64 ! -name linux-arm64 -exec rm -rf {} + \
    && test -f /app/runtimes/linux-x64/native/libQuestPdfSkia.so
COPY --from=frontend /src/backend/TrustCabinet.Web/wwwroot /app/wwwroot

# 3. Runtime.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=backend /app ./
# Recordings are written under the content root (Recordings:StorageRoot). /app belongs to root, so the folder is made
# here for the app user; a named volume mounted on it starts with this owner.
RUN mkdir -p App_Data/recordings && chown -R $APP_UID App_Data
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "TrustCabinet.Web.dll"]
