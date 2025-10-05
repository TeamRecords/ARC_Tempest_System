import mysql, { type Pool } from "mysql2/promise";

export type TempestDatabase = Pool;

let database: TempestDatabase | undefined;

function parseBoolean(value: string | undefined): boolean {
  return value ? value.toLowerCase() === "true" : false;
}

const databaseDisabled = parseBoolean(process.env.TEMPEST_USE_MOCK_DB);

export function isDatabaseEnabled(): boolean {
  return !databaseDisabled;
}

function parseNumber(value: string | number | undefined, fallback: number): number {
  const parsed = typeof value === "string" ? Number.parseInt(value, 10) : value;
  return Number.isFinite(parsed as number) ? (parsed as number) : fallback;
}

function createDatabase(): TempestDatabase {
  if (!isDatabaseEnabled()) {
    throw new Error("Tempest map database access has been disabled via TEMPEST_USE_MOCK_DB");
  }

  const host = process.env.MYSQL_HOST ?? "127.0.0.1";
  const port = parseNumber(process.env.MYSQL_PORT, 3306);
  const user = process.env.MYSQL_USER ?? "tempest";
  const password = process.env.MYSQL_PASSWORD ?? "";
  const databaseName = process.env.MYSQL_DATABASE ?? "tempest_map";
  const connectionLimit = parseNumber(process.env.MYSQL_POOL_LIMIT, 10);

  return mysql.createPool({
    host,
    port,
    user,
    password,
    database: databaseName,
    waitForConnections: true,
    connectionLimit,
    queueLimit: 0,
    dateStrings: true
  });
}

export function getDatabase(): TempestDatabase {
  if (!database) {
    database = createDatabase();
  }

  return database;
}

export async function closeDatabase(): Promise<void> {
  if (database) {
    await database.end();
    database = undefined;
  }
}
