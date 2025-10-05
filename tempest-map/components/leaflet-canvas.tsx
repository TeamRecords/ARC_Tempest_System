"use client";

import { useEffect, useMemo } from "react";
import { MapContainer, CircleMarker, Tooltip, SVGOverlay, useMap } from "react-leaflet";
import { CRS, LatLngBounds } from "leaflet";
import type { PlayerSnapshot, PlayerPosition } from "@/lib/positions";

type LatLng = [number, number];

type LeafletCanvasProps = {
  snapshot: PlayerSnapshot;
};

function projectPosition(position: PlayerPosition["position"], levelSize: number): LatLng {
  const half = levelSize / 2;
  const latitude = half - position.z;
  const longitude = position.x + half;
  return [latitude, longitude];
}

function FitBounds({ levelSize }: { levelSize: number }) {
  const map = useMap();

  useEffect(() => {
    const bounds = new LatLngBounds([0, 0], [levelSize, levelSize]);
    map.fitBounds(bounds, { padding: [40, 40] });
    map.setMaxBounds(bounds.pad(0.1));
  }, [map, levelSize]);

  return null;
}

function GridOverlay({ levelSize }: { levelSize: number }) {
  const bounds: [[number, number], [number, number]] = useMemo(() => [[0, 0], [levelSize, levelSize]], [levelSize]);
  const lines = useMemo(() => Array.from({ length: 10 }, (_, index) => index + 1), []);

  return (
    <SVGOverlay bounds={bounds}>
      <svg viewBox={`0 0 ${levelSize} ${levelSize}`} xmlns="http://www.w3.org/2000/svg">
        <rect width="100%" height="100%" fill="url(#gridGradient)" />
        <defs>
          <radialGradient id="gridGradient" cx="0%" cy="0%" r="120%">
            <stop offset="0%" stopColor="rgba(56,189,248,0.18)" />
            <stop offset="60%" stopColor="rgba(15,23,42,0.85)" />
          </radialGradient>
        </defs>
        {lines.map((line) => {
          const offset = (line / 10) * levelSize;
          return (
            <g key={line}>
              <line x1="0" y1={offset} x2={levelSize} y2={offset} stroke="rgba(148,163,184,0.2)" strokeWidth={1} />
              <line x1={offset} y1="0" x2={offset} y2={levelSize} stroke="rgba(148,163,184,0.2)" strokeWidth={1} />
            </g>
          );
        })}
      </svg>
    </SVGOverlay>
  );
}

function PlayerMarker({ player, levelSize }: { player: PlayerPosition; levelSize: number }) {
  const center = projectPosition(player.position, levelSize);
  const color = player.isOnline ? "#38bdf8" : "#475569";
  const fillOpacity = player.isOnline ? 0.65 : 0.35;

  return (
    <CircleMarker center={center} pathOptions={{ color, weight: 2, fillColor: color, fillOpacity }} radius={8}>
      <Tooltip direction="top" offset={[0, -12]} opacity={1} permanent={false}>
        <div className="min-w-[180px] space-y-1 text-xs">
          <div className="font-semibold text-white">{player.characterName}</div>
          <div className="text-[0.65rem] uppercase tracking-[0.35em] text-slate-400">
            {player.groupName ?? "Lone Wolf"}
          </div>
          <div className="text-slate-200">Health · {player.health}%</div>
          <div className="text-slate-300">
            Pos · {player.position.x.toFixed(1)}, {player.position.y.toFixed(1)}, {player.position.z.toFixed(1)}
          </div>
          <div className="text-slate-400">Facing · {player.rotationY.toFixed(0)}°</div>
        </div>
      </Tooltip>
    </CircleMarker>
  );
}

export default function LeafletCanvas({ snapshot }: LeafletCanvasProps) {
  const levelSize = snapshot.metadata.levelSize > 0 ? snapshot.metadata.levelSize : 4096;
  const bounds: [[number, number], [number, number]] = useMemo(
    () => [[0, 0], [levelSize, levelSize]],
    [levelSize]
  );

  return (
    <div className="relative flex flex-1 flex-col">
      <div className="relative flex flex-1 overflow-hidden rounded-3xl border border-white/10 bg-slate-950/80">
        <MapContainer
          key={levelSize}
          className="tempest-map"
          crs={CRS.Simple}
          bounds={bounds}
          zoomControl={false}
          scrollWheelZoom
          style={{ height: "100%", width: "100%" }}
        >
          <FitBounds levelSize={levelSize} />
          <GridOverlay levelSize={levelSize} />
          {snapshot.players.map((player) => (
            <PlayerMarker key={player.steamId} player={player} levelSize={levelSize} />
          ))}
        </MapContainer>
        {snapshot.players.length === 0 && (
          <div className="pointer-events-none absolute inset-0 flex items-center justify-center text-xs uppercase tracking-[0.4em] text-slate-400">
            Awaiting live telemetry…
          </div>
        )}
        <div className="pointer-events-none absolute inset-x-0 bottom-0 bg-gradient-to-t from-slate-950 via-transparent to-transparent p-4 text-right text-[0.6rem] uppercase tracking-[0.35em] text-slate-400">
          Coordinates locked · Scale 1 : {levelSize}
        </div>
      </div>
    </div>
  );
}
