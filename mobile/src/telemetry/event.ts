export const MAX_BATCH_EVENTS = 100;

const EMPTY_GUID = "00000000-0000-0000-0000-000000000000";
const GUID_PATTERN =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const EARLIEST_RECORDED_AT = Date.parse("2000-01-01T00:00:00.000Z");

export interface TelemetryDraft {
  eventId: string;
  vehicleId: string;
  recordedAt: string;
  latitude: number;
  longitude: number;
  sequenceNumber: number;
}

export function validateTelemetry(event: TelemetryDraft, now = new Date()): string | null {
  if (!isGuid(event.eventId))
    return "eventId es obligatorio.";

  if (!isGuid(event.vehicleId))
    return "vehicleId es obligatorio.";

  const recordedAt = Date.parse(event.recordedAt);
  if (!Number.isFinite(recordedAt))
    return "recordedAt no es válido.";

  const latestAccepted = now.getTime() + 5 * 60 * 1000;
  if (recordedAt < EARLIEST_RECORDED_AT || recordedAt > latestAccepted)
    return "recordedAt no es válido.";

  if (!isCoordinate(event.latitude, -90, 90) || !isCoordinate(event.longitude, -180, 180))
    return "Las coordenadas no son válidas.";

  if (!Number.isInteger(event.sequenceNumber) || event.sequenceNumber < 0)
    return "sequenceNumber no es válido.";

  return null;
}

export function createTelemetry(input: {
  eventId: string;
  vehicleId: string;
  latitude: number;
  longitude: number;
  sequenceNumber: number;
  recordedAt?: Date;
  now?: Date;
}): { ok: true; event: TelemetryDraft } | { ok: false; error: string } {
  const recordedAt = input.recordedAt ?? input.now ?? new Date();
  const event: TelemetryDraft = {
    eventId: input.eventId,
    vehicleId: input.vehicleId,
    recordedAt: recordedAt.toISOString(),
    latitude: input.latitude,
    longitude: input.longitude,
    sequenceNumber: input.sequenceNumber
  };
  const error = validateTelemetry(event, input.now ?? recordedAt);
  return error ? { ok: false, error } : { ok: true, event };
}

export function normalizeBaseUrl(value: string): string | null {
  const trimmed = value.trim().replace(/\/+$/, "");
  if (!trimmed)
    return null;

  let url: URL;
  try {
    url = new URL(trimmed);
  } catch {
    return null;
  }

  if (url.protocol !== "http:" && url.protocol !== "https:")
    return null;

  if (url.username || url.password || url.search || url.hash || (url.pathname !== "/" && url.pathname !== ""))
    return null;

  return url.toString().replace(/\/$/, "");
}

function isGuid(value: string): boolean {
  return GUID_PATTERN.test(value) && value.toLowerCase() !== EMPTY_GUID;
}

function isCoordinate(value: number, minimum: number, maximum: number): boolean {
  return Number.isFinite(value) && value >= minimum && value <= maximum;
}
