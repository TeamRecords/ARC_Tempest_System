import mysql, { Pool, PoolOptions } from "mysql2/promise";

type DatabasePool = Pool;

let pool: DatabasePool | undefined;

function createPool(): DatabasePool {
  const config: PoolOptions = {
    host: process.env.TEMPEST_MAP_DB_HOST ?? "127.0.0.1",
    port: Number(process.env.TEMPEST_MAP_DB_PORT ?? 3306),
    user: process.env.TEMPEST_MAP_DB_USER ?? "tempest",
    password: process.env.TEMPEST_MAP_DB_PASSWORD ?? "ChangeMe!",
    database: process.env.TEMPEST_MAP_DB_NAME ?? "tempest_map",
    connectionLimit: Number(process.env.TEMPEST_MAP_DB_POOL_SIZE ?? 8),
    waitForConnections: true,
    timezone: "Z"
  };

  return mysql.createPool(config);
}

export function getPool(): DatabasePool {
  if (!pool) {
    pool = createPool();
  }

  return pool;
}

export async function withConnection<T>(callback: (connection: mysql.PoolConnection) => Promise<T>): Promise<T> {
  const dbPool = getPool();
  const connection = await dbPool.getConnection();
  try {
    return await callback(connection);
  } finally {
    connection.release();
  }
}
