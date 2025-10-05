import { getDatabase, handleDatabaseError, isDatabaseEnabled } from "@/lib/db";
import { ensureLiveSchema } from "@/lib/schema";
import type { RowDataPacket } from "mysql2";

export type PlayerPosition = {
  steamId: string;
  characterName: string;
  groupName?: string | null;
  position: {
    x: number;
    y: number;
    z: number;
  };
  rotationY: number;
  health: number;
  isOnline: boolean;
  lastSeenUtc: string;
};

export type MapMetadata = {
  mapName: string;
  levelSize: number;
  lastSyncedUtc: string;
  shareUrl?: string | null;
};

export type PlayerSnapshot = {
  metadata: MapMetadata;
  players: PlayerPosition[];
};

function createMockSnapshot(): PlayerSnapshot {
  const now = new Date();
  return {
    metadata: {
      mapName: "Tempest Training Grounds",
      levelSize: 4096,
      lastSyncedUtc: now.toISOString(),
      shareUrl: "https://tempest.arcfoundation.net/map"
    },
    players: [
      {
        steamId: "76561198000000001",
        characterName: "Sentinel",
        groupName: "Echo",
        position: { x: 512, y: 20, z: -340 },
        rotationY: 120,
        health: 96,
        isOnline: true,
        lastSeenUtc: now.toISOString()
      },
      {
        steamId: "76561198000000002",
        characterName: "Ranger",
        groupName: "Echo",
        position: { x: -210, y: 18, z: 1024 },
        rotationY: 300,
        health: 72,
        isOnline: false,
        lastSeenUtc: new Date(now.getTime() - 120_000).toISOString()
      }
    ]
  } satisfies PlayerSnapshot;
}

type MapMetadataRow = RowDataPacket & {
  map_name: string;
  level_size: number;
  last_synced_utc: string | null;
  share_url: string | null;
};

type PlayerRow = RowDataPacket & {
  steam_id: string;
  character_name: string;
  group_name: string | null;
  position_x: number;
  position_y: number;
  position_z: number;
  rotation_y: number;
  health: number;
  is_online: number;
  last_seen_utc: string;
};

function toIsoString(value: string | null | undefined): string {
  if (!value) {
    return new Date().toISOString();
  }

  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? new Date().toISOString() : parsed.toISOString();
}

export async function fetchPlayerSnapshot(): Promise<PlayerSnapshot> {
  if (!isDatabaseEnabled()) {
    return createMockSnapshot();
  }

  try {
    const pool = getDatabase();
    const schemaReady = await ensureLiveSchema();
    if (!schemaReady) {
      return createMockSnapshot();
    }

    const [metadataRows] = await pool.query<MapMetadataRow[]>(
      "SELECT map_name, level_size, last_synced_utc, share_url FROM map_state WHERE id = 1 LIMIT 1"
    );

    const [playerRows] = await pool.query<PlayerRow[]>(
      "SELECT steam_id, character_name, group_name, position_x, position_y, position_z, rotation_y, health, is_online, last_seen_utc FROM players ORDER BY is_online DESC, last_seen_utc DESC"
    );

    const fallbackSnapshot = createMockSnapshot();

    const metadata = metadataRows.length
      ? {
          mapName: metadataRows[0].map_name,
          levelSize: metadataRows[0].level_size,
          lastSyncedUtc: toIsoString(metadataRows[0].last_synced_utc),
          shareUrl: metadataRows[0].share_url
        }
      : fallbackSnapshot.metadata;

    const players: PlayerPosition[] = playerRows.length
      ? playerRows.map((row) => ({
          steamId: row.steam_id,
          characterName: row.character_name,
          groupName: row.group_name,
          position: { x: row.position_x, y: row.position_y, z: row.position_z },
          rotationY: row.rotation_y,
          health: row.health,
          isOnline: row.is_online === 1,
          lastSeenUtc: toIsoString(row.last_seen_utc)
        }))
      : fallbackSnapshot.players;

    return {
      metadata,
      players
    } satisfies PlayerSnapshot;
  } catch (error) {
    handleDatabaseError(error, "Live snapshot query");
    console.error("[TempestMap] Failed to query database, returning mock snapshot.", error);
    return createMockSnapshot();
  }
}
