import { MAX_BATCH_EVENTS, type TelemetryDraft } from "./event";
import type { Outbox } from "./outbox";

export interface SyncReport {
  accepted: number;
  pending: number;
  stoppedBecause: "empty" | "accepted" | "rejected" | "failed";
  status?: number;
  error?: string;
}

export type BatchPoster = (events: TelemetryDraft[]) => Promise<number>;

export async function syncPending(
  outbox: Outbox,
  postBatch: BatchPoster,
  maxBatches = 20
): Promise<SyncReport> {
  let accepted = 0;

  for (let batch = 0; batch < maxBatches; batch += 1) {
    const pending = await outbox.listPending(MAX_BATCH_EVENTS);
    if (pending.length === 0) {
      return { accepted, pending: 0, stoppedBecause: accepted === 0 ? "empty" : "accepted" };
    }

    let status: number;
    try {
      status = await postBatch(pending);
    } catch (error) {
      return {
        accepted,
        pending: await outbox.countPending(),
        stoppedBecause: "failed",
        error: error instanceof Error ? error.message : "No se pudo enviar el lote."
      };
    }

    if (status !== 202) {
      return {
        accepted,
        pending: await outbox.countPending(),
        stoppedBecause: "rejected",
        status
      };
    }

    await outbox.markAccepted(pending.map((event) => event.eventId));
    accepted += pending.length;
  }

  return {
    accepted,
    pending: await outbox.countPending(),
    stoppedBecause: "accepted"
  };
}

export async function postTelemetryBatch(
  baseUrl: string,
  apiKey: string,
  events: TelemetryDraft[],
  fetchImpl: typeof fetch = fetch
): Promise<number> {
  const response = await fetchImpl(`${baseUrl}/api/telemetry/batches`, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
      "X-Api-Key": apiKey
    },
    body: JSON.stringify({
      events: events.map((event) => ({
        eventId: event.eventId,
        vehicleId: event.vehicleId,
        recordedAt: event.recordedAt,
        latitude: event.latitude,
        longitude: event.longitude,
        sequenceNumber: event.sequenceNumber
      }))
    }),
    signal: AbortSignal.timeout(15_000)
  });

  return response.status;
}
