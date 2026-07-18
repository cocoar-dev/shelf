# ── Stage 1: Build backend ──────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /src
COPY src/ ./
RUN dotnet restore Cocoar.Shelf/Cocoar.Shelf.csproj
RUN dotnet publish Cocoar.Shelf/Cocoar.Shelf.csproj -c Release -o /app --no-restore

# ── Stage 2: Build Admin UI ─────────────────────────────────────────
FROM node:22-slim AS frontend-build
ENV CI=true
RUN corepack enable && corepack prepare pnpm@10.13.1 --activate
WORKDIR /src
COPY src/Cocoar.Shelf.Client/ ./
RUN pnpm install --frozen-lockfile
# vite.config points outDir at ../Cocoar.Shelf/wwwroot (local dev); in the
# isolated stage we redirect it — type checking already gated in CI.
RUN npx vite build --outDir /out

# ── Stage 3: Runtime ────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# Npgsql probes for GSSAPI/Kerberos when opening a connection; without the
# library present it logs a benign "Cannot load libgssapi_krb5.so.2" error
# before falling back to password auth (which is what we use). Install it so
# the startup logs stay clean.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=backend-build /app ./
COPY --from=frontend-build /out ./wwwroot/

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# Deliberately no `USER app`: Shelf writes into the host-mounted /data/docs and
# /data/config volumes (uploads, product JSONs), whose host ownership predates
# this image. Revisit together with a volume chown when hardening.
ENTRYPOINT ["dotnet", "Cocoar.Shelf.dll"]
