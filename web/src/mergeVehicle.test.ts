import { describe, expect, it } from "vitest";
import { mergeVehicle, type VehicleState } from "./mergeVehicle";

const current: VehicleState = {
  vehicleId: "vehicle-1",
  lastEventId: "event-2",
  sequenceNumber: 2,
  recordedAt: "2026-10-23T21:00:00.000Z",
  latitude: 4.65,
  longitude: -74.05
};

describe("mergeVehicle", () => {
  it("keeps the newer position when an older update arrives", () => {
    const older: VehicleState = {
      ...current,
      lastEventId: "event-1",
      sequenceNumber: 1,
      recordedAt: "2026-10-23T20:00:00.000Z",
      latitude: 1,
      longitude: -70
    };

    expect(mergeVehicle(current, older)).toBe(current);
  });

  it("replaces the position when the update is newer", () => {
    const newer: VehicleState = {
      ...current,
      lastEventId: "event-3",
      sequenceNumber: 3,
      recordedAt: "2026-10-23T21:05:00.000Z",
      latitude: 4.7
    };

    expect(mergeVehicle(current, newer).lastEventId).toBe("event-3");
  });
});
