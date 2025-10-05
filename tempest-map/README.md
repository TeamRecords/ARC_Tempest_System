# Tempest Tactical Map

A Next.js 15 + Tailwind CSS 4 control-room experience for the ARC Tempest Unturned plugin.

## Getting started

```bash
npm install
cp .env.example .env.local
npm run dev
```

The development server runs on <http://localhost:3000> by default. Configure the following environment variables to connect both the ingest API and the tactical UI to your MySQL instance:

```
TEMPEST_MAP_DB_HOST=127.0.0.1
TEMPEST_MAP_DB_PORT=3306
TEMPEST_MAP_DB_USER=tempest
TEMPEST_MAP_DB_PASSWORD=ChangeMe!
TEMPEST_MAP_DB_NAME=tempest_map
LIVE_SYNC_SERVER_KEY=ChangeMe!
TEMPEST_USE_MOCK_DB=false
```

The Rocket plugin will stream telemetry to `POST /api/unturned/live` with the `X-Server-Key` header set to `LIVE_SYNC_SERVER_KEY`. The Next.js `GET /api/live` endpoint (and the `/map` page) query the same database to power the Leaflet dashboard.

For local development without a MySQL server, set `TEMPEST_USE_MOCK_DB=true` to bypass all database access and serve the built-in mock telemetry while keeping the ingest API disabled. This prevents noisy connection errors when the database is intentionally unavailable. Fatal connection errors now automatically disable database access until the process restarts, so the mock telemetry continues to power the UI without repeated console spam.

## Available scripts

- `npm run dev` – run the development server with hot reload.
- `npm run build` – compile for production.
- `npm run start` – launch the production build.
- `npm run lint` – run ESLint checks.

## Architecture highlights

- **App Router** with server components for fast initial loads and streaming updates.
- **MySQL-backed Codex API** for ingesting live telemetry directly from the Tempest plugin.
- **Leaflet + React Leaflet** to render the live tactical picture with smooth refreshes.
- **Tailwind CSS 4** design system with neon-accented tactical visuals.
- Graceful fallbacks when the database is unavailable (mock telemetry keeps the UI alive).

Deploy the site to any Node-friendly platform (Vercel, Fly.io, bare metal) and point the in-game `/tmap` command at the `/map`
route.
