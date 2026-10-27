import { describe, expect, it } from "vitest";
import { createTelemetry, normalizeBaseUrl, validateTelemetry } from "./event";
import { MemoryOutbox } from "./outbox";
import { syncPending } from "./sync";

const vehicleId = "22222222-2222-2222-2222-222222222222";
const now = new Date("2026-10-24T18:00:00.000Z");

function draft(sequenceNumber: number, eventId = `11111111-1111-1111-1111-${sequenceNumber.toString(16).padStart(12, "0")}`) {
  const created = createTelemetry({
    eventId,
    vehicleId,
    latitude: 4.65,
    longitude: -74.05,
    sequenceNumber,
    recordedAt: now,
    now
  });
  if (!created.ok)
    throw new Error(created.error);
  return created.event;
}

describe("telemetry queue", () => {
  it("rejects an invalid position before it can be stored", () => {
    const created = createTelemetry({
      eventId: "11111111-1111-1111-1111-111111111111",
      vehicleId,
      latitude: 120,
      longitude: -74.05,
      sequenceNumber: 1,
      now
    });

    expect(created.ok).toBe(false);
  });

  it("keeps the event pending when the send fails", async () => {
    const outbox = new MemoryOutbox();
    const event = draft(1);
    await outbox.enqueue(event);

    const report = await syncPending(outbox, async () => {
      throw new Error("sin red");
    });

    expect(report.stoppedBecause).toBe("failed");
    expect(report.accepted).toBe(0);
    expect(await outbox.listPending(10)).toEqual([event]);
  });

  it("marks events accepted only after the server returns 202", async () => {
    const outbox = new MemoryOutbox();
    const event = draft(1);
    await outbox.enqueue(event);

    const rejected = await syncPending(outbox, async () => 503);
    expect(rejected.stoppedBecause).toBe("rejected");
    expect(await outbox.countPending()).toBe(1);

    const accepted = await syncPending(outbox, async () => 202);
    expect(accepted.accepted).toBe(1);
    expect(await outbox.countPending()).toBe(0);
  });

  it("does not enqueue the same event twice", async () => {
    const outbox = new MemoryOutbox();
    const event = draft(1);

    expect(await outbox.enqueue(event)).toBe("inserted");
    expect(await outbox.enqueue(event)).toBe("duplicate");
    expect(await outbox.countPending()).toBe(1);
  });

  it("sends at most 100 events per request and continues with the rest", async () => {
    const outbox = new MemoryOutbox();
    for (let sequence = 1; sequence <= 150; sequence += 1)
      await outbox.enqueue(draft(sequence));

    const sizes: number[] = [];
    const report = await syncPending(outbox, async (events) => {
      sizes.push(events.length);
      return 202;
    });

    expect(sizes).toEqual([100, 50]);
    expect(report.accepted).toBe(150);
    expect(await outbox.countPending()).toBe(0);
  });

  it("still has the pending event after a restart", async () => {
    const event = draft(4);
    const first = new MemoryOutbox();
    await first.enqueue(event);
    const restarted = new MemoryOutbox(await first.listPending(10));

    expect(await restarted.countPending()).toBe(1);
    expect((await restarted.listPending(1))[0]?.eventId).toBe(event.eventId);
  });

  it("rejects an empty vehicle id and a base url that is not http", () => {
    expect(validateTelemetry({ ...draft(1), vehicleId: "00000000-0000-0000-0000-000000000000" }, now)).toBe(
      "vehicleId es obligatorio."
    );
    expect(normalizeBaseUrl("ftp://localhost:8080")).toBeNull();
    expect(normalizeBaseUrl("http://127.0.0.1:8080/")).toBe("http://127.0.0.1:8080");
  });
});
