import { formatDistanceToNowStrict } from "date-fns";
import clsx from "clsx";
import type { PlayerSnapshot } from "@/lib/positions";

function formatRelative(time: string) {
  try {
    return formatDistanceToNowStrict(new Date(time), { addSuffix: true });
  } catch (error) {
    console.error("[TempestMap] Failed to format time", error);
    return "recently";
  }
}

export default function MapLegend({ snapshot }: { snapshot: PlayerSnapshot }) {
  return (
    <div className="flex flex-1 flex-col gap-6">
      <div>
        <h2 className="text-lg font-semibold text-white">{snapshot.metadata.mapName}</h2>
        <p className="text-xs uppercase tracking-[0.35em] text-slate-400">
          Last sync · {formatRelative(snapshot.metadata.lastSyncedUtc)} · Level size {snapshot.metadata.levelSize}
        </p>
      </div>
      <div className="space-y-3">
        <h3 className="text-xs uppercase tracking-[0.35em] text-slate-400">Active Squads</h3>
        <ul className="space-y-2">
          {snapshot.players.length === 0 && (
            <li className="rounded-2xl border border-dashed border-white/10 bg-white/5 px-4 py-6 text-center text-xs uppercase tracking-[0.35em] text-slate-400">
              Awaiting first telemetry packet
            </li>
          )}
          {snapshot.players.map((player) => {
            const isOnline = player.isOnline;
            return (
              <li
                key={player.steamId}
                className={clsx(
                  "flex items-center justify-between rounded-2xl border px-4 py-3 text-sm transition",
                  isOnline
                    ? "border-brand-400/40 bg-brand-500/5 text-brand-50 shadow-brand-500/20"
                    : "border-white/5 bg-white/5 text-slate-400"
                )}
              >
                <div className="flex flex-col">
                  <span className="font-semibold text-white">{player.characterName}</span>
                  <span className="text-xs uppercase tracking-[0.3em] text-slate-400">
                    {player.groupName ?? "Lone Wolf"}
                  </span>
                </div>
                <div className="text-right text-xs text-slate-300">
                  <div>{isOnline ? "Online" : "Offline"}</div>
                  <div className="text-[0.7rem] uppercase tracking-[0.4em] text-slate-500">
                    {formatRelative(player.lastSeenUtc)}
                  </div>
                </div>
              </li>
            );
          })}
        </ul>
      </div>
      <div className="rounded-2xl border border-white/10 bg-white/5 p-4 text-xs text-slate-300">
        <h3 className="mb-2 text-sm font-semibold uppercase tracking-[0.3em] text-white/80">Coordinates</h3>
        <p>
          Positions are expressed using the Unturned world coordinate system. Hover or tap on a player marker to reveal
          exact values alongside facing direction. Data is refreshed on a {Number(process.env.TEMPEST_MAP_REFRESH_SECONDS ?? 5)}
          second cadence directly from the Tempest plugin bridge.
        </p>
      </div>
    </div>
  );
}
