# Local dev: shelf ↔ modgud

> Internal context, architecture and gotchas live in **Atlas → `shelf/`**
> (`dev-environment`, `auth-architecture`, `deployment-runbook`). This file is just the
> runnable quickstart next to the scripts.

`cocoar-postgres` (:5432) must be running.

```sh
# 0. Once per fresh setup: create modgud's DB.
docker exec cocoar-postgres psql -U postgres -c "CREATE DATABASE modgud_shelfdev;"

# 1. Infra: modgud (:8092) + Mailpit (:8025).
docker compose -f docker-compose.dev.yml up -d

# 2. Bootstrap a modgud admin (once per fresh modgud_shelfdev DB).
docker exec shelf-dev-modgud dotnet Modgud.Api.dll recover bootstrap-admin \
  --email admin@shelf.local --username admin --password 'Passw0rd!Admin' --realm system

# 3. Seed the shelf App / client / test users (idempotent), then restart for rate limits.
node dev/seed-modgud.mjs
docker restart shelf-dev-modgud

# 4. Backend (:8080) + client build.
cd src/Cocoar.Shelf.Client && pnpm run build
cd ../Cocoar.Shelf && dotnet run
```

Log in as **`bwi@doob.at`** (admin) or `tester@shelf.local` — OTP codes land in
**Mailpit → http://localhost:8025**.

Integration tests need no modgud: `dotnet test ./src` (Testcontainers PostgreSQL + test seam).
