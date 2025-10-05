import { withConnection } from "@/lib/db";

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
  isOnline: boolean;
  lastSeenUtc: string;
};

export type MapMetadata = {
  mapName: string;
  levelSize: number;
  lastSyncedUtc: string;
};

export type PlayerSnapshot = {
  metadata: MapMetadata;
  players: PlayerPosition[];
};

const MOCK_SNAPSHOT: PlayerSnapshot = {
  metadata: {
    mapName: "Tempest Training Grounds",
    levelSize: 4096,
    lastSyncedUtc: new Date().toISOString()
  },
  players: [
    {
      steamId: "76561198000000001",
      characterName: "Sentinel",
      groupName: "Echo",
      position: { x: 512, y: 20, z: -340 },
      rotationY: 120,
      isOnline: true,
      lastSeenUtc: new Date().toISOString()
    },
    {
      steamId: "76561198000000002",
      characterName: "Ranger",
      groupName: "Echo",
      position: { x: -210, y: 18, z: 1024 },
      rotationY: 300,
      isOnline: false,
      lastSeenUtc: new Date(Date.now() - 120000).toISOString()
    }
  ]
};

export async function fetchPlayerSnapshot(): Promise<PlayerSnapshot> {
  try {
    return await withConnection(async (connection) => {
      const [metadataRows] = await connection.query<
        {
          map_name: string;
          level_size: number;
          last_synced_utc: Date;
        }[]
      >("SELECT map_name, level_size, last_synced_utc FROM tempest_map_metadata WHERE id = 1 LIMIT 1");

      const [playerRows] = await connection.query<
        {
          steam_id: string;
          character_name: string;
          group_name: string | null;
          position_x: number;
          position_y: number;
          position_z: number;
          rotation_y: number;
          is_online: number;
          last_seen_utc: Date;
        }[]
      >(
        "SELECT steam_id, character_name, group_name, position_x, position_y, position_z, rotation_y, is_online, last_seen_utc FROM tempest_player_positions ORDER BY is_online DESC, last_seen_utc DESC"
      );

      const metadata = metadataRows?.[0]
        ? {
            mapName: metadataRows[0].map_name,
            levelSize: metadataRows[0].level_size,
            lastSyncedUtc: metadataRows[0].last_synced_utc.toISOString()
          }
        : MOCK_SNAPSHOT.metadata;

      const players: PlayerPosition[] = playerRows.map((row) => ({
        steamId: row.steam_id,
        characterName: row.character_name,
        groupName: row.group_name,
        position: { x: row.position_x, y: row.position_y, z: row.position_z },
        rotationY: row.rotation_y,
        isOnline: row.is_online === 1,
        lastSeenUtc: row.last_seen_utc.toISOString()
      }));

      return {
        metadata,
        players
      } satisfies PlayerSnapshot;
    });
  } catch (error) {
    console.error("[TempestMap] Failed to query database, returning mock snapshot.", error);
    return MOCK_SNAPSHOT;
  }
}
