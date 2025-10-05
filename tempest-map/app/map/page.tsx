import { Suspense } from "react";
import MapLegend from "@/components/map-legend";
import MapViewport from "@/components/map-viewport";
import { fetchPlayerSnapshot } from "@/lib/positions";

export const revalidate = 5;

async function MapContent() {
  const snapshot = await fetchPlayerSnapshot();
  return (
    <div className="relative mx-auto flex w-full max-w-6xl flex-1 flex-col gap-8 px-6 py-10 lg:flex-row">
      <section className="flex flex-1 flex-col rounded-3xl border border-white/10 bg-slate-950/60 p-6 shadow-2xl shadow-black/40 backdrop-blur">
        <MapViewport snapshot={snapshot} />
      </section>
      <aside className="flex w-full max-w-md flex-col gap-6 rounded-3xl border border-white/10 bg-slate-950/40 p-6 shadow-xl shadow-black/40 backdrop-blur">
        <MapLegend snapshot={snapshot} />
      </aside>
    </div>
  );
}

export default function MapPage() {
  return (
    <section className="flex flex-1 flex-col">
      <div className="border-b border-white/5 bg-slate-950/60 px-6 py-8 shadow-lg shadow-black/50 backdrop-blur">
        <div className="mx-auto flex w-full max-w-6xl flex-col gap-3">
          <h1 className="text-3xl font-semibold tracking-tight text-white">Tempest Tactical Map</h1>
          <p className="text-sm text-slate-300">
            Live player positions refreshed every {Math.max(5, Number(process.env.TEMPEST_MAP_REFRESH_SECONDS ?? 5))} seconds.
          </p>
        </div>
      </div>
      <Suspense
        fallback={
          <div className="flex flex-1 items-center justify-center">
            <div className="flex flex-col items-center gap-4 text-slate-400">
              <span className="h-16 w-16 animate-spin rounded-full border-4 border-brand-400/40 border-t-brand-200" />
              Fetching the latest telemetry from Tempest HQ…
            </div>
          </div>
        }
      >
        <MapContent />
      </Suspense>
    </section>
  );
}
