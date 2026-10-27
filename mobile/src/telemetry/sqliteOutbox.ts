import type { SQLiteDatabase } from "expo-sqlite";
import type { Outbox } from "./outbox";

interface PendingRow {
  eventId: string;
  vehicleId: string;
  recordedAt: string;
  latitude: number;
  longitude: number;
  sequenceNumber: number;
}

export async function migrate(db: SQLiteDatabase): Promise<void> {
  await db.execAsync(`
    PRAGMA journal_mode = WAL;
    CREATE TABLE IF NOT EXISTS outbox (
      event_id TEXT PRIMARY KEY NOT NULL,
      vehicle_id TEXT NOT NULL,
      recorded_at TEXT NOT NULL,
      latitude REAL NOT NULL,
      longitude REAL NOT NULL,
      sequence_number INTEGER NOT NULL,
      status TEXT NOT NULL
    );
    CREATE TABLE IF NOT EXISTS device_state (
      key TEXT PRIMARY KEY NOT NULL,
      value TEXT NOT NULL
    );
  `);
}

export function createSqliteOutbox(db: SQLiteDatabase): Outbox {
  return {
    async enqueue(event) {
      const result = await db.runAsync(
        `INSERT OR IGNORE INTO outbox
          (event_id, vehicle_id, recorded_at, latitude, longitude, sequence_number, status)
         VALUES (?, ?, ?, ?, ?, ?, 'pending')`,
        event.eventId,
        event.vehicleId,
        event.recordedAt,
        event.latitude,
        event.longitude,
        event.sequenceNumber
      );
      return result.changes === 0 ? "duplicate" : "inserted";
    },
    async listPending(limit) {
      const rows = await db.getAllAsync<PendingRow>(
        `SELECT event_id AS eventId, vehicle_id AS vehicleId, recorded_at AS recordedAt,
                latitude, longitude, sequence_number AS sequenceNumber
         FROM outbox
         WHERE status = 'pending'
         ORDER BY sequence_number ASC
         LIMIT ?`,
        limit
      );
      return rows;
    },
    async markAccepted(eventIds) {
      await db.withTransactionAsync(async () => {
        for (const eventId of eventIds) {
          await db.runAsync(
            `UPDATE outbox SET status = 'accepted' WHERE event_id = ? AND status = 'pending'`,
            eventId
          );
        }
      });
    },
    async countPending() {
      const row = await db.getFirstAsync<{ total: number }>(
        `SELECT COUNT(*) AS total FROM outbox WHERE status = 'pending'`
      );
      return row?.total ?? 0;
    }
  };
}

export async function readState(db: SQLiteDatabase, key: string): Promise<string | null> {
  const row = await db.getFirstAsync<{ value: string }>(
    `SELECT value FROM device_state WHERE key = ?`,
    key
  );
  return row?.value ?? null;
}

export async function writeState(db: SQLiteDatabase, key: string, value: string): Promise<void> {
  await db.runAsync(
    `INSERT INTO device_state (key, value) VALUES (?, ?)
     ON CONFLICT(key) DO UPDATE SET value = excluded.value`,
    key,
    value
  );
}
