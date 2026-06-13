# syntax=docker/dockerfile:1

# ── Stage 1: build the React (Vite) frontend ────────────────────────────────
FROM node:20-alpine AS web
WORKDIR /web
COPY src/web/package.json src/web/package-lock.json ./
RUN npm ci
COPY src/web/ ./
RUN npm run build   # emits to /web/dist (vite.config.ts outDir)

# ── Stage 2: build + publish the .NET 9 API ─────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/shared/ ./src/shared/
COPY src/api/ ./src/api/
COPY sample-contracts/ ./sample-contracts/
RUN dotnet restore src/api/ContractClause.Api.csproj
RUN dotnet publish src/api/ContractClause.Api.csproj -c Release -o /app -p:UseAppHost=false
# bundle the built SPA so the API serves it from wwwroot
COPY --from=web /web/dist /app/wwwroot

# ── Stage 3: runtime ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
# Render injects PORT; Program.cs binds to it. 8080 is the local-docker default.
ENV PORT=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ContractClause.Api.dll"]
