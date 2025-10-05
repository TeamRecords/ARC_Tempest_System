# Tempest Tactical Map

A Next.js 15 + Tailwind CSS 4 control-room experience for the ARC Tempest Unturned plugin.

## Getting started

```bash
npm install
cp .env.example .env.local
npm run dev
```

The development server runs on <http://localhost:3000> by default. The tactical map API connects to a local SQLite database. By
 default a database file is created at `./data/tempest-map.db` the first time the API is called and is seeded with sample
 telemetry. Adjust `TEMPEST_MAP_DB_PATH` in `.env.local` if you want to use a different location or point the site at a
 production database that is populated by the Tempest plugin.

## Available scripts

- `npm run dev` – run the development server with hot reload.
- `npm run build` – compile for production.
- `npm run start` – launch the production build.
- `npm run lint` – run ESLint checks.

## Architecture highlights

- **App Router** with server components for fast initial loads and streaming updates.
- **Better SQLite3** for zero-ORM access to the telemetry tables populated by the Tempest plugin.
- **Tailwind CSS 4** design system with neon-accented tactical visuals.
- Graceful fallbacks when the database is unavailable (mock telemetry keeps the UI alive).

Deploy the site to any Node-friendly platform (Vercel, Fly.io, bare metal) and point the in-game `/tmap` command at the `/map`
route.
