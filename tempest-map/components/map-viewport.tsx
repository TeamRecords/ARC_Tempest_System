"use client";

import { Fragment, useCallback, useEffect, useMemo, useState } from "react";
import clsx from "clsx";
import type { PlayerSnapshot, PlayerPosition } from "@/lib/positions";

const REFRESH_INTERVAL = Number(process.env.NEXT_PUBLIC_TEMPEST_MAP_REFRESH_MS ?? 5000);

function useLiveSnapshot(initial: PlayerSnapshot) {
  const [snapshot, setSnapshot] = useState<PlayerSnapshot>(initial);

  const refresh = useCallback(async () => {
    try {
      const response = await fetch("/api/map", { cache: "no-store" });
      if (!response.ok) {
        throw new Error(`Failed to refresh tactical map: ${response.status}`);
      }
      const payload = (await response.json()) as PlayerSnapshot;
      setSnapshot(payload);
    } catch (error) {
      console.error("[TempestMap] Live refresh failed", error);
    }
  }, []);

  useEffect(() => {
    setSnapshot(initial);
  }, [initial]);

  useEffect(() => {
    const interval = setInterval(refresh, Math.max(1500, REFRESH_INTERVAL));
    return () => clearInterval(interval);
  }, [refresh]);

  return snapshot;
}

function projectCoordinate(value: number, levelSize: number) {
  const halfSize = levelSize / 2;
  const clamped = Math.max(-halfSize, Math.min(halfSize, value));
  const normalised = (clamped + halfSize) / levelSize;
  return Math.round(normalised * 1000) / 10; // percentage with single decimal
}

function PlayerMarker({ player, levelSize }: { player: PlayerPosition; levelSize: number }) {
  const top = 100 - projectCoordinate(player.position.z, levelSize);
  const left = projectCoordinate(player.position.x, levelSize);
  const rotation = player.rotationY;

  return (
    <div
      className={clsx(
        "group absolute flex -translate-x-1/2 -translate-y-1/2 flex-col items-center gap-1 text-xs",
        player.isOnline ? "text-brand-100" : "text-slate-500"
      )}
      style={{ top: `${top}%`, left: `${left}%` }}
    >
      <span
        className={clsx(
          "relative flex h-10 w-10 items-center justify-center rounded-full border text-sm font-semibold transition",
          player.isOnline
            ? "border-brand-300/60 bg-brand-500/20 shadow-lg shadow-brand-500/20"
            : "border-white/10 bg-white/5"
        )}
        style={{ transform: `rotate(${rotation}deg)` }}
        title={`${player.characterName}\n(${player.position.x.toFixed(1)}, ${player.position.y.toFixed(1)}, ${player.position.z.toFixed(1)})`}
      >
        <span className="pointer-events-none select-none" style={{ transform: `rotate(${-rotation}deg)` }}>
          ▲
        </span>
      </span>
      <div className="rounded-full bg-slate-900/80 px-3 py-1 text-[0.65rem] font-semibold uppercase tracking-[0.35em]">
        {player.characterName}
      </div>
    </div>
  );
}

function BackgroundGrid() {
  const lines = useMemo(() => Array.from({ length: 10 }, (_, index) => index + 1), []);
  return (
    <div className="absolute inset-0 overflow-hidden rounded-2xl">
      <div className="absolute inset-0 bg-[radial-gradient(circle_at_top_left,rgba(51,210,255,0.12),transparent_55%)]" />
      <div className="absolute inset-0 bg-slate-900/50" />
      <svg className="absolute inset-0 h-full w-full" aria-hidden="true">
        {lines.map((line) => (
          <Fragment key={line}>
            <line x1="0" x2="100%" y1={`${(line / 10) * 100}%`} y2={`${(line / 10) * 100}%`} stroke="rgba(148, 163, 184, 0.15)" strokeWidth="1" />
            <line y1="0" y2="100%" x1={`${(line / 10) * 100}%`} x2={`${(line / 10) * 100}%`} stroke="rgba(148, 163, 184, 0.15)" strokeWidth="1" />
          </Fragment>
        ))}
      </svg>
    </div>
  );
}

export default function MapViewport({ snapshot: initialSnapshot }: { snapshot: PlayerSnapshot }) {
  const snapshot = useLiveSnapshot(initialSnapshot);
  const levelSize = snapshot.metadata.levelSize || 4096;

  return (
    <div className="relative flex flex-1 flex-col">
      <div className="relative flex flex-1 items-stretch overflow-hidden rounded-3xl border border-white/10 bg-slate-950/80">
        <BackgroundGrid />
        <div className="relative z-10 h-full w-full">
          {snapshot.players.map((player) => (
            <PlayerMarker key={player.steamId} player={player} levelSize={levelSize} />
          ))}
        </div>
        <div className="pointer-events-none absolute inset-x-0 bottom-0 bg-gradient-to-t from-slate-950 via-transparent to-transparent p-4 text-right text-[0.6rem] uppercase tracking-[0.35em] text-slate-400">
          Coordinates locked · Scale 1 : {levelSize}
        </div>
      </div>
    </div>
  );
}
