import Link from "next/link";

export default function DocsLandingPage() {
  return (
    <section className="mx-auto flex w-full max-w-4xl flex-1 flex-col gap-8 px-6 py-12">
      <div className="space-y-4">
        <span className="rounded-full border border-brand-400/30 bg-brand-500/10 px-4 py-1 text-[0.6rem] uppercase tracking-[0.35em] text-brand-100">
          Deployment Manual
        </span>
        <h1 className="text-3xl font-semibold text-white">Tempest Tactical Map Integration</h1>
        <p className="text-sm text-slate-300">
          Configure the ARC Tempest plugin and Next.js tactical map to unlock real-time situational awareness for your
          Unturned survivors. The steps below assume access to the game server, a MySQL instance and a deployment target for
          the web UI.
        </p>
      </div>
      <ol className="space-y-6 text-sm text-slate-200">
        <li className="rounded-3xl border border-white/10 bg-slate-950/60 p-6 shadow-lg shadow-black/40">
          <h2 className="text-base font-semibold text-white">1 · Provision the database</h2>
          <p>
            Create a MySQL database (for example <code className="rounded bg-slate-900/80 px-1 py-0.5">tempest_map</code>) and
            grant credentials that the Unturned server and web client can both access. The plugin will automatically create
            the <code>tempest_map_metadata</code> and <code>tempest_player_positions</code> tables.
          </p>
        </li>
        <li className="rounded-3xl border border-white/10 bg-slate-950/60 p-6 shadow-lg shadow-black/40">
          <h2 className="text-base font-semibold text-white">2 · Configure ARC Tempest</h2>
          <p>
            Edit <code>ARC_TPA_Commands.configuration.xml</code> (generated on first launch) and set
            <code>Enable_Map_Bridge</code> to <code>true</code>, then paste your MySQL connection string into
            <code>Map_Connection_String</code>. Update <code>Map_Share_Url</code> if you deploy the site on a custom domain.
          </p>
        </li>
        <li className="rounded-3xl border border-white/10 bg-slate-950/60 p-6 shadow-lg shadow-black/40">
          <h2 className="text-base font-semibold text-white">3 · Deploy the Next.js site</h2>
          <p>
            Install dependencies with <code>pnpm install</code> (or npm/yarn) inside <code>tempest-map</code>, configure the
            environment variables documented in the project README, and run <code>pnpm run build</code> followed by
            <code>pnpm start</code>. The tactical map pulls data directly from the MySQL tables populated by the plugin.
          </p>
        </li>
        <li className="rounded-3xl border border-white/10 bg-slate-950/60 p-6 shadow-lg shadow-black/40">
          <h2 className="text-base font-semibold text-white">4 · Share the map</h2>
          <p>
            Survivors can use the new <code>/tmap</code> command in-game to receive a private link to the tactical map (default
            <Link className="ml-1 underline" href="https://tempest.arcfoundation.net/map">
              https://tempest.arcfoundation.net/map
            </Link>
            ).
          </p>
        </li>
      </ol>
    </section>
  );
}
