import DatabaseConstructor, { Database as DatabaseInstance } from "better-sqlite3";
import { existsSync, mkdirSync } from "node:fs";
import path from "node:path";

export type TempestDatabase = DatabaseInstance;

let database: TempestDatabase | undefined;

function getDatabasePath(): string {
  const configuredPath = process.env.TEMPEST_MAP_DB_PATH;
  if (configuredPath && configuredPath.trim().length > 0) {
    return configuredPath;
  }

  return path.join(process.cwd(), "data", "tempest-map.db");
}

function ensureDirectoryExists(directory: string) {
  if (!existsSync(directory)) {
    mkdirSync(directory, { recursive: true });
  }
}

function seedMetadata(db: TempestDatabase) {
  const { count } = db.prepare("SELECT COUNT(*) AS count FROM tempest_map_metadata").get() as { count: number };
  if (count > 0) {
    return;
  }

  const now = new Date().toISOString();
  db.prepare(
    `INSERT INTO tempest_map_metadata (id, map_name, level_size, last_synced_utc)
     VALUES (1, @mapName, @levelSize, @lastSyncedUtc)`
  ).run({
    mapName: "Tempest Training Grounds",
    levelSize: 4096,
    lastSyncedUtc: now
  });
}

function seedPlayers(db: TempestDatabase) {
  const { count } = db.prepare("SELECT COUNT(*) AS count FROM tempest_player_positions").get() as { count: number };
  if (count > 0) {
    return;
  }

  const now = new Date();
  const players = [
    {
      steamId: "76561198000000001",
      characterName: "Sentinel",
      groupName: "Echo",
      positionX: 512,
      positionY: 20,
      positionZ: -340,
      rotationY: 120,
      isOnline: 1,
      lastSeenUtc: now.toISOString()
    },
    {
      steamId: "76561198000000002",
      characterName: "Ranger",
      groupName: "Echo",
      positionX: -210,
      positionY: 18,
      positionZ: 1024,
      rotationY: 300,
      isOnline: 0,
      lastSeenUtc: new Date(now.getTime() - 120_000).toISOString()
    }
  ];

  const statement = db.prepare(
    `INSERT INTO tempest_player_positions
      (steam_id, character_name, group_name, position_x, position_y, position_z, rotation_y, is_online, last_seen_utc)
     VALUES
      (@steamId, @characterName, @groupName, @positionX, @positionY, @positionZ, @rotationY, @isOnline, @lastSeenUtc)`
  );

  const insert = db.transaction((records: typeof players) => {
    for (const record of records) {
      statement.run(record);
    }
  });

  insert(players);
}

function initialiseSchema(db: TempestDatabase) {
  db.pragma("journal_mode = WAL");
  db.exec(`
    CREATE TABLE IF NOT EXISTS tempest_map_metadata (
      id INTEGER PRIMARY KEY CHECK (id = 1),
      map_name TEXT NOT NULL,
      level_size INTEGER NOT NULL,
      last_synced_utc TEXT NOT NULL
    );

    CREATE TABLE IF NOT EXISTS tempest_player_positions (
      steam_id TEXT PRIMARY KEY,
      character_name TEXT NOT NULL,
      group_name TEXT,
      position_x REAL NOT NULL,
      position_y REAL NOT NULL,
      position_z REAL NOT NULL,
      rotation_y REAL NOT NULL,
      is_online INTEGER NOT NULL DEFAULT 0,
      last_seen_utc TEXT NOT NULL
    );
  `);

  seedMetadata(db);
  seedPlayers(db);
}

function createDatabase(): TempestDatabase {
  const databasePath = getDatabasePath();
  ensureDirectoryExists(path.dirname(databasePath));

  const db = new DatabaseConstructor(databasePath);
  initialiseSchema(db);
  return db;
}

export function getDatabase(): TempestDatabase {
  if (!database) {
    database = createDatabase();
  }

  return database;
}

export function closeDatabase(): void {
  if (database) {
    database.close();
    database = undefined;
  }
}
