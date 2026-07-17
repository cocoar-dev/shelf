# Local dev: shelf ↔ modgud federation

Shelf's admin login is federated to modgud (all credentials live in the IdP; the backend
brokers the email-code flow server-to-server and mints the `shelf.auth` cookie). To exercise
it locally, run a real modgud from the compose stack.

## Runbook

`docker-compose.dev.yml` (repo root) runs a dedicated modgud (:8092) + Mailpit (:8025).
`cocoar-postgres` (:5432) must be running — the stack reuses it via host.docker.internal.

```sh
# 0. Once per fresh setup: create modgud's DB.
docker exec cocoar-postgres psql -U postgres -c "CREATE DATABASE modgud_shelfdev;"

# 1. Infra: modgud (:8092) + Mailpit (:8025).
docker compose -f docker-compose.dev.yml up -d

# 2. Bootstrap a modgud admin (once per fresh modgud_shelfdev DB).
docker exec shelf-dev-modgud dotnet Modgud.Api.dll recover bootstrap-admin \
  --email admin@shelf.local --username admin --password 'Passw0rd!Admin' --realm system

# 3. Seed the shelf App / audience / scope / shelf-web client / test users +
#    NativeGrants + relaxed OTP rate limits. Idempotent.
node dev/seed-modgud.mjs

# 4. Rate-limit changes only take effect after a modgud restart (the live limiter
#    caches its partition at boot).
docker restart shelf-dev-modgud

# 5. Backend (:8080). data/configuration.json already points Modgud at http://127.0.0.1:8092
#    (127.0.0.1, NOT localhost — .NET HttpClient's IPv6 ::1 attempt costs ~21s per call on
#    Windows otherwise).
cd src/Cocoar.Shelf && dotnet run

# 6. Frontend (vite dev server, proxies /_api to :8080).
cd src/Cocoar.Shelf.Client && pnpm dev
```

Log in as **`bwi@doob.at`** (allowlisted admin in `data/configuration.json` → `Modgud.Admins`)
or `tester@shelf.local` (plain user) — the OTP code lands in **Mailpit → http://localhost:8025**.

> ⚠️ This stack's Mailpit binds :8025/:1025 — the same ports as amzettel's dev stack / a
> shared mailpit container. Stop the other one first, or drop the mailpit service here.

Teardown: `docker compose -f docker-compose.dev.yml down` keeps the `modgud_shelfdev` DB
(re-seed-free restart); `docker exec cocoar-postgres psql -U postgres -c "DROP DATABASE modgud_shelfdev;"`
resets it fully.

## OTP cooldown

modgud suppresses OTP re-sends per user for ~2 minutes regardless of the relaxed realm rate
limits. If "no OTP arrived in Mailpit", wait ~2 min and re-request.

## Integration tests

`dotnet test ./src` needs no modgud and no manual setup: the suite starts its own PostgreSQL
via Testcontainers and signs in through the test seam (`/_api/test/signin`, mapped only when
`Shelf__TestAuth=true`, which the fixture sets).
