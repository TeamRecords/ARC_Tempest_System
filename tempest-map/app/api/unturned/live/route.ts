import { NextRequest, NextResponse } from "next/server";
import { getDatabase, handleDatabaseError, isDatabaseEnabled } from "@/lib/db";
import { ensureLiveSchema } from "@/lib/schema";
import type { PoolConnection } from "mysql2/promise";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

type IncomingPlayer = {
  steamId: string;
  characterName: string;
  groupName?: string | null;
  position: { x: number; y: number; z: number };
  rotationY: number;
  health: number;
  isOnline: boolean;
  lastSeenUtc?: string;
};

type IncomingPayload = {
  capturedAt?: string;
  map?: { name?: string; levelSize?: number; shareUrl?: string | null };
  players?: IncomingPlayer[];
};

function parseDate(value: string | undefined, fallback: Date): Date {
  if (!value) {
    return fallback;
  }

  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? fallback : parsed;
}

function toNumber(value: unknown, fallback: number): number {
  if (typeof value === "number" && Number.isFinite(value)) {
    return value;
  }

  if (typeof value === "string" && value.trim().length > 0) {
    const parsed = Number.parseFloat(value);
    if (Number.isFinite(parsed)) {
      return parsed;
    }
  }

  return fallback;
}

function sanitisePlayer(player: IncomingPlayer, fallbackDate: Date) {
  const lastSeen = parseDate(player.lastSeenUtc, fallbackDate);
  return {
    steamId: typeof player.steamId === "string" ? player.steamId : String(player.steamId ?? ""),
    characterName: player.characterName?.trim().length ? player.characterName.trim() : "Unknown Survivor",
    groupName: player.groupName && player.groupName.trim().length > 0 ? player.groupName.trim() : null,
    position: {
      x: toNumber(player.position?.x, 0),
      y: toNumber(player.position?.y, 0),
      z: toNumber(player.position?.z, 0)
    },
    rotationY: toNumber(player.rotationY, 0),
    health: Math.max(0, Math.min(100, Math.round(toNumber(player.health, 0)))),
    isOnline: Boolean(player.isOnline),
    lastSeenUtc: lastSeen.toISOString()
  };
}

async function persistSnapshot(connection: PoolConnection, payload: IncomingPayload) {
  await ensureLiveSchema(connection);

  const capturedAt = parseDate(payload.capturedAt, new Date());
  const mapName = payload.map?.name?.trim().length ? payload.map.name.trim() : "Unknown";
  const levelSize = Math.max(1, Math.round(toNumber(payload.map?.levelSize, 4096)));
  const shareUrl = payload.map?.shareUrl?.trim().length ? payload.map.shareUrl?.trim() ?? null : null;

  await connection.execute(
    `INSERT INTO map_state (id, map_name, level_size, last_synced_utc, share_url)
     VALUES (1, ?, ?, ?, ?)
     ON DUPLICATE KEY UPDATE
       map_name = VALUES(map_name),
       level_size = VALUES(level_size),
       last_synced_utc = VALUES(last_synced_utc),
       share_url = VALUES(share_url)`,
    [mapName, levelSize, capturedAt.toISOString().slice(0, 19).replace("T", " "), shareUrl]
  );

  const players = Array.isArray(payload.players) ? payload.players : [];
  const playerIds: string[] = [];

  for (const player of players) {
    const entry = sanitisePlayer(player, capturedAt);
    if (!entry.steamId || entry.steamId.trim().length === 0) {
      continue;
    }

    playerIds.push(entry.steamId);

    await connection.execute(
      `INSERT INTO players (steam_id, character_name, group_name, position_x, position_y, position_z, rotation_y, health, is_online, last_seen_utc, updated_at)
       VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
       ON DUPLICATE KEY UPDATE
         character_name = VALUES(character_name),
         group_name = VALUES(group_name),
         position_x = VALUES(position_x),
         position_y = VALUES(position_y),
         position_z = VALUES(position_z),
         rotation_y = VALUES(rotation_y),
         health = VALUES(health),
         is_online = VALUES(is_online),
         last_seen_utc = VALUES(last_seen_utc),
         updated_at = VALUES(updated_at)`,
      [
        entry.steamId,
        entry.characterName,
        entry.groupName,
        entry.position.x,
        entry.position.y,
        entry.position.z,
        entry.rotationY,
        entry.health,
        entry.isOnline ? 1 : 0,
        entry.lastSeenUtc.slice(0, 19).replace("T", " "),
        capturedAt.toISOString().slice(0, 19).replace("T", " ")
      ]
    );
  }

  if (playerIds.length > 0) {
    const placeholders = playerIds.map(() => "?").join(", ");
    await connection.execute(
      `UPDATE players SET is_online = 0 WHERE steam_id NOT IN (${placeholders})`,
      playerIds
    );
  } else {
    await connection.execute("UPDATE players SET is_online = 0");
  }
}

export async function POST(request: NextRequest) {
  const configuredKey = process.env.LIVE_SYNC_SERVER_KEY;
  const presentedKey = request.headers.get("x-server-key");

  if (!configuredKey || presentedKey !== configuredKey) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }

  let payload: IncomingPayload;
  try {
    payload = (await request.json()) as IncomingPayload;
  } catch (error) {
    console.error("[TempestMap] Failed to parse live payload", error);
    return NextResponse.json({ error: "Invalid payload" }, { status: 400 });
  }

  if (!payload || typeof payload !== "object") {
    return NextResponse.json({ error: "Invalid payload" }, { status: 400 });
  }

  if (!isDatabaseEnabled()) {
    console.error("[TempestMap] Live sync skipped: database disabled via TEMPEST_USE_MOCK_DB.");
    return NextResponse.json({ error: "Database disabled" }, { status: 503 });
  }

  let connection: PoolConnection;

  try {
    const pool = getDatabase();
    connection = await pool.getConnection();
  } catch (error) {
    handleDatabaseError(error, "Connection acquisition");
    console.error("[TempestMap] Failed to obtain database connection", error);
    return NextResponse.json({ error: "Database unavailable" }, { status: 503 });
  }

  try {
    await connection.beginTransaction();
    await persistSnapshot(connection, payload);
    await connection.commit();
    return NextResponse.json({ status: "ok" });
  } catch (error) {
    await connection.rollback();
    console.error("[TempestMap] Failed to persist live snapshot", error);
    return NextResponse.json({ error: "Failed to persist live snapshot" }, { status: 500 });
  } finally {
    connection.release();
  }
}
