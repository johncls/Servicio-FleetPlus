export interface VehicleState {
  vehicleId: string;
  lastEventId: string;
  sequenceNumber: number;
  recordedAt: string;
  latitude: number;
  longitude: number;
}

export function mergeVehicle(current: VehicleState | undefined, incoming: VehicleState): VehicleState {
  if (!current)
    return incoming;

  const currentTime = Date.parse(current.recordedAt);
  const incomingTime = Date.parse(incoming.recordedAt);
  if (incomingTime > currentTime)
    return incoming;

  if (incomingTime === currentTime && incoming.sequenceNumber > current.sequenceNumber)
    return incoming;

  return current;
}
