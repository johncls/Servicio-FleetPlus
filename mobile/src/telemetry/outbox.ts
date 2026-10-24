import type { TelemetryDraft } from "./event";

export interface Outbox {
  enqueue(event: TelemetryDraft): Promise<"inserted" | "duplicate">;
  listPending(limit: number): Promise<TelemetryDraft[]>;
  markAccepted(eventIds: string[]): Promise<void>;
  countPending(): Promise<number>;
}

export class MemoryOutbox implements Outbox {
  private readonly events: TelemetryDraft[] = [];
  private readonly accepted = new Set<string>();

  constructor(existing: TelemetryDraft[] = []) {
    this.events.push(...existing);
  }

  async enqueue(event: TelemetryDraft): Promise<"inserted" | "duplicate"> {
    if (this.events.some((item) => item.eventId === event.eventId) || this.accepted.has(event.eventId))
      return "duplicate";

    this.events.push(event);
    return "inserted";
  }

  async listPending(limit: number): Promise<TelemetryDraft[]> {
    return this.events
      .filter((event) => !this.accepted.has(event.eventId))
      .sort((left, right) => left.sequenceNumber - right.sequenceNumber)
      .slice(0, limit);
  }

  async markAccepted(eventIds: string[]): Promise<void> {
    for (const eventId of eventIds)
      this.accepted.add(eventId);
  }

  async countPending(): Promise<number> {
    return (await this.listPending(Number.MAX_SAFE_INTEGER)).length;
  }
}
