import type { VehicleState } from "./mergeVehicle";

export interface VehiclePosition {
  eventId: string;
  recordedAt: string;
  latitude: number;
  longitude: number;
  sequenceNumber: number;
}

export class FleetApiError extends Error {
  constructor(
    message: string,
    readonly status: number
  ) {
    super(message);
  }
}

export async function fetchVehicles(apiKey: string): Promise<VehicleState[]> {
  return request<VehicleState[]>("/api/vehicles?limit=100", apiKey);
}

export async function fetchPositions(vehicleId: string, apiKey: string): Promise<VehiclePosition[]> {
  return request<VehiclePosition[]>(`/api/vehicles/${vehicleId}/positions?limit=100`, apiKey);
}

async function request<T>(path: string, apiKey: string): Promise<T> {
  const response = await fetch(path, {
    headers: {
      Accept: "application/json",
      "X-Api-Key": apiKey
    }
  });

  if (response.status === 401)
    throw new FleetApiError("La clave no es válida.", 401);

  if (!response.ok) {
    const body = await response.json().catch(() => null) as { error?: string } | null;
    throw new FleetApiError(body?.error ?? "No se pudo consultar la flota.", response.status);
  }

  return response.json() as Promise<T>;
}
