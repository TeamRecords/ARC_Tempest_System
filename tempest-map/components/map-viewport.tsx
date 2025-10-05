"use client";

import { useCallback, useEffect, useState } from "react";
import dynamic from "next/dynamic";
import type { PlayerSnapshot } from "@/lib/positions";

const REFRESH_INTERVAL = Number(process.env.NEXT_PUBLIC_TEMPEST_MAP_REFRESH_MS ?? 5000);

const LeafletCanvas = dynamic(() => import("@/components/leaflet-canvas"), { ssr: false });

type MapViewportProps = {
  snapshot: PlayerSnapshot;
};

function useLiveSnapshot(initial: PlayerSnapshot) {
  const [snapshot, setSnapshot] = useState<PlayerSnapshot>(initial);

  const refresh = useCallback(async () => {
    try {
      const response = await fetch("/api/live", { cache: "no-store" });
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
    const interval = window.setInterval(refresh, Math.max(1500, REFRESH_INTERVAL));
    return () => window.clearInterval(interval);
  }, [refresh]);

  return snapshot;
}

export default function MapViewport({ snapshot: initialSnapshot }: MapViewportProps) {
  const snapshot = useLiveSnapshot(initialSnapshot);
  return <LeafletCanvas snapshot={snapshot} />;
}
