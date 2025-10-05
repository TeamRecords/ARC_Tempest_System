import mysql from "mysql2/promise";

function parseBoolean(value) {
  return value ? value.toLowerCase() === "true" : false;
}

function parseNumber(value, fallback) {
  if (typeof value === "string") {
    const parsed = Number.parseInt(value, 10);
    return Number.isFinite(parsed) ? parsed : fallback;
  }

  if (typeof value === "number" && Number.isFinite(value)) {
    return value;
  }

  return fallback;
}

async function ensureSchema() {
  if (parseBoolean(process.env.TEMPEST_USE_MOCK_DB)) {
    console.log("[TempestMap] Database schema skipped (mock DB enabled).");
    return;
  }

  const host = process.env.MYSQL_HOST ?? "127.0.0.1";
  const port = parseNumber(process.env.MYSQL_PORT, 3306);
  const user = process.env.MYSQL_USER ?? "tempest";
  const password = process.env.MYSQL_PASSWORD ?? "";
  const database = process.env.MYSQL_DATABASE ?? "tempest_map";

  const pool = mysql.createPool({
    host,
    port,
    user,
    password,
    database,
    waitForConnections: true,
    connectionLimit: 2,
    queueLimit: 0,
    dateStrings: true
  });

  let connection;

  try {
    connection = await pool.getConnection();

    await connection.query(`
      CREATE TABLE IF NOT EXISTS map_state (
        id TINYINT UNSIGNED NOT NULL PRIMARY KEY,
        map_name VARCHAR(120) NOT NULL,
        level_size INT NOT NULL,
        last_synced_utc DATETIME NOT NULL,
        share_url VARCHAR(255) NULL
      ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
    `);

    await connection.query(`
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

    console.log("[TempestMap] Database schema ensured.");
  } finally {
    if (connection) {
      connection.release();
    }

    await pool.end();
  }
}

ensureSchema().catch((error) => {
  console.error("[TempestMap] Failed to ensure database schema.", error);
  process.exitCode = 1;
});
