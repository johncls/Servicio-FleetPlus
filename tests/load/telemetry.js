import http from "k6/http";
import { check, sleep } from "k6";

const baseUrl = __ENV.BASE_URL || "http://127.0.0.1:8080";
const apiKey = __ENV.TELEMETRY_API_KEY || "change-me";

http.setResponseCallback(http.expectedStatuses(200, 202, 400, 404));

export const options = {
  scenarios: {
    hundreds_of_vehicles: {
      executor: "per-vu-iterations",
      vus: 200,
      iterations: 1,
      maxDuration: "1m",
      exec: "sendVehiclePosition"
    },
    controlled_faults: {
      executor: "shared-iterations",
      vus: 1,
      iterations: 1,
      startTime: "20s",
      maxDuration: "1m",
      exec: "verifyControlledCases"
    }
  },
  thresholds: {
    checks: ["rate>0.95"],
    http_req_failed: ["rate<0.05"],
    "http_req_duration{expected_response:true}": ["p(95)<3000"]
  }
};

const headers = {
  "Content-Type": "application/json",
  Accept: "application/json",
  "X-Api-Key": apiKey
};

export function sendVehiclePosition() {
  const vehicleId = fleetVehicleId(__VU);
  if (__VU % 25 === 0) {
    const rejected = postBatch([
      event(vehicleId, eventId(__VU, 1), 1, 120, -74.05, new Date())
    ]);
    check(rejected, {
      "el lote con coordenadas inválidas responde 400": (response) => response.status === 400
    });
    return;
  }

  const recordedAt = new Date();
  const payload = event(vehicleId, eventId(__VU, 1), 1, 4.65, -74.05, recordedAt);
  const accepted = postBatch([payload]);
  check(accepted, {
    "la posición del vehículo responde 202": (response) => response.status === 202
  });

  if (__VU % 10 === 0) {
    const duplicate = postBatch([payload]);
    check(duplicate, {
      "el reintento del mismo eventId responde 202": (response) => response.status === 202
    });
  }
}

export function verifyControlledCases() {
  const now = new Date();
  const duplicateVehicle = fleetVehicleId(901);
  const duplicateEvent = eventId(901, 1);
  const duplicate = event(duplicateVehicle, duplicateEvent, 1, 4.71, -74.07, now);
  const first = postBatch([duplicate]);
  const second = postBatch([duplicate]);
  check(first, { "el primer envío del duplicado responde 202": (response) => response.status === 202 });
  check(second, { "el segundo envío del duplicado responde 202": (response) => response.status === 202 });

  const stored = waitForPositions(
    duplicateVehicle,
    (positions) => positions.filter((position) => position.eventId === duplicateEvent).length === 1
  );
  const copies = (stored || []).filter((position) => position.eventId === duplicateEvent).length;
  check(copies, { "el eventId duplicado queda una sola vez": (count) => count === 1 });

  const rejectedVehicle = fleetVehicleId(902);
  const keptEvent = eventId(902, 1);
  const rejected = postBatch([
    event(rejectedVehicle, keptEvent, 1, 4.71, -74.07, now),
    event(rejectedVehicle, eventId(902, 2), 2, 120, -74.07, now)
  ]);
  check(rejected, { "un lote con un evento inválido responde 400": (response) => response.status === 400 });
  sleep(2);
  const absent = http.get(`${baseUrl}/api/vehicles/${rejectedVehicle}/positions?limit=20`, { headers });
  check(absent, { "el lote rechazado no crea el vehículo": (response) => response.status === 404 });

  const orderedVehicle = fleetVehicleId(903);
  const newerAt = new Date();
  const olderAt = new Date(newerAt.getTime() - 60_000);
  const newer = postBatch([
    event(orderedVehicle, eventId(903, 2), 2, 4.8, -74.1, newerAt)
  ]);
  check(newer, { "el evento más reciente responde 202": (response) => response.status === 202 });
  waitForVehicle(orderedVehicle, (vehicle) => vehicle.sequenceNumber === 2 && Math.abs(vehicle.latitude - 4.8) < 0.001);

  const older = postBatch([
    event(orderedVehicle, eventId(903, 1), 1, 4.1, -74.5, olderAt)
  ]);
  check(older, { "el evento más antiguo responde 202": (response) => response.status === 202 });
  sleep(2);
  const current = findVehicle(orderedVehicle);
  check(current, {
    "un evento antiguo no reemplaza la posición vigente": (vehicle) =>
      vehicle !== null && vehicle.sequenceNumber === 2 && Math.abs(vehicle.latitude - 4.8) < 0.001
  });
}

function postBatch(events) {
  return http.post(`${baseUrl}/api/telemetry/batches`, JSON.stringify({ events }), { headers });
}

function event(vehicleId, eventIdValue, sequenceNumber, latitude, longitude, recordedAt) {
  return {
    eventId: eventIdValue,
    vehicleId,
    recordedAt: recordedAt.toISOString(),
    latitude,
    longitude,
    sequenceNumber
  };
}

function fleetVehicleId(index) {
  return `a1000000-0000-4000-8000-${index.toString(16).padStart(12, "0")}`;
}

function eventId(vehicleIndex, sequenceNumber) {
  const value = vehicleIndex * 100 + sequenceNumber;
  return `b1000000-0000-4000-8000-${value.toString(16).padStart(12, "0")}`;
}

function waitForPositions(vehicleId, ready) {
  const deadline = Date.now() + 20_000;
  let last = [];
  while (Date.now() < deadline) {
    const response = http.get(`${baseUrl}/api/vehicles/${vehicleId}/positions?limit=20`, { headers });
    if (response.status === 200) {
      last = response.json();
      if (ready(last))
        return last;
    }
    sleep(0.5);
  }
  return last;
}

function waitForVehicle(vehicleId, ready) {
  const deadline = Date.now() + 20_000;
  while (Date.now() < deadline) {
    const vehicle = findVehicle(vehicleId);
    if (vehicle && ready(vehicle))
      return vehicle;
    sleep(0.5);
  }
  return null;
}

function findVehicle(vehicleId) {
  const response = http.get(`${baseUrl}/api/vehicles?limit=100`, { headers });
  if (response.status !== 200)
    return null;
  return response.json().find((vehicle) => vehicle.vehicleId === vehicleId) || null;
}
