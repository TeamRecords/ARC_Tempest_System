import type { PoolConnection } from "mysql2/promise";
import { disableDatabaseAccess, getDatabase, handleDatabaseError, isDatabaseEnabled } from "@/lib/db";

let schemaInitialised = false;

export async function ensureLiveSchema(connection?: PoolConnection): Promise<boolean> {
  if (schemaInitialised) {
    return true;
  }

  if (!isDatabaseEnabled()) {
    return false;
  }

  const pool = getDatabase();
  let conn: PoolConnection | undefined;

  try {
    conn = connection ?? (await pool.getConnection());
  } catch (error) {
    const wasEnabled = isDatabaseEnabled();
    handleDatabaseError(error, "Schema initialisation");
    if (wasEnabled) {
      console.error("[TempestMap] Failed to ensure database schema.", error);
    }
    return false;
  }

  try {
    await conn.query(`
      CREATE TABLE IF NOT EXISTS map_state (
        id TINYINT UNSIGNED NOT NULL PRIMARY KEY,
        map_name VARCHAR(120) NOT NULL,
        level_size INT NOT NULL,
        last_synced_utc DATETIME NOT NULL,
        share_url VARCHAR(255) NULL
      ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
    `);

    await conn.query(`
      CREATE TABLE IF NOT EXISTS players (
        steam_id BIGINT UNSIGNED NOT NULL PRIMARY KEY,
        character_name VARCHAR(120) NOT NULL,
        group_name VARCHAR(120) NULL,
        position_x DOUBLE NOT NULL,
        position_y DOUBLE NOT NULL,
        position_z DOUBLE NOT NULL,
        rotation_y DOUBLE NOT NULL,
        health TINYINT UNSIGNED NOT NULL,
        is_online TINYINT(1) NOT NULL DEFAULT 0,
        last_seen_utc DATETIME NOT NULL,
        updated_at DATETIME NOT NULL,
        INDEX idx_players_last_seen (last_seen_utc),
        INDEX idx_players_online (is_online)
      ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
    `);

    schemaInitialised = true;
    return true;
  } catch (error) {
    const wasEnabled = isDatabaseEnabled();
    handleDatabaseError(error, "Schema initialisation");
    if (wasEnabled) {
      console.error("[TempestMap] Failed to ensure database schema.", error);
    }
    disableDatabaseAccess("Schema migration failure", error);
    return false;
  } finally {
    if (!connection && conn) {
      conn.release();
    }
  }
}
