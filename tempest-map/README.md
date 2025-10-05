# Tempest Tactical Map

A Next.js 15 + Tailwind CSS 4 control-room experience for the ARC Tempest Unturned plugin.

## Getting started

```bash
pnpm install
cp .env.example .env.local
pnpm dev
```

The development server runs on <http://localhost:3000> by default. Configure the environment variables listed in `.env.example` so the API routes can reach your MySQL instance.

## Available scripts

- `pnpm dev` – run the development server with hot reload.
- `pnpm build` – compile for production.
- `pnpm start` – launch the production build.
- `pnpm lint` – run ESLint checks.

## Architecture highlights

- **App Router** with server components for fast initial loads and streaming updates.
- **MySQL2** for zero-ORM access to the telemetry tables populated by the Tempest plugin.
- **Tailwind CSS 4** design system with neon-accented tactical visuals.
- Graceful fallbacks when the database is unavailable (mock telemetry keeps the UI alive).

Deploy the site to any Node-friendly platform (Vercel, Fly.io, bare metal) and point the in-game `/tmap` command at the `/map` route.
