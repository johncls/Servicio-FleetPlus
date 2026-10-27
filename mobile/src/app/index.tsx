import * as Crypto from "expo-crypto";
import * as Location from "expo-location";
import * as SecureStore from "expo-secure-store";
import { useSQLiteContext } from "expo-sqlite";
import { useEffect, useState } from "react";
import {
  ActivityIndicator,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput
} from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";
import { createTelemetry, normalizeBaseUrl, type TelemetryDraft } from "../telemetry/event";
import { createSqliteOutbox, readState, writeState } from "../telemetry/sqliteOutbox";
import { postTelemetryBatch, syncPending } from "../telemetry/sync";

const API_KEY = "fleetpulse.apiKey";

export default function CaptureScreen() {
  const db = useSQLiteContext();
  const [ready, setReady] = useState(false);
  const [vehicleId, setVehicleId] = useState("");
  const [baseUrl, setBaseUrl] = useState("");
  const [apiKey, setApiKey] = useState("");
  const [pending, setPending] = useState<TelemetryDraft[]>([]);
  const [pendingCount, setPendingCount] = useState(0);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let active = true;
    async function load() {
      const storedVehicle = await readState(db, "vehicle_id");
      const vehicle = storedVehicle ?? Crypto.randomUUID();
      if (!storedVehicle)
        await writeState(db, "vehicle_id", vehicle);

      const storedUrl = await readState(db, "api_base_url");
      const storedKey = await readApiKey(db);
      const outbox = createSqliteOutbox(db);
      const queued = await outbox.listPending(20);
      const total = await outbox.countPending();
      if (!active)
        return;

      setVehicleId(vehicle);
      setBaseUrl(storedUrl ?? "");
      setApiKey(storedKey ?? "");
      setPending(queued);
      setPendingCount(total);
      setReady(true);
    }

    void load();
    return () => {
      active = false;
    };
  }, [db]);

  async function saveSettings() {
    const url = normalizeBaseUrl(baseUrl);
    if (!url) {
      setError("La dirección del servicio tiene que empezar por http:// o https://.");
      return;
    }

    if (apiKey.trim().length === 0) {
      setError("La clave de acceso es obligatoria.");
      return;
    }

    setError(null);
    await writeState(db, "api_base_url", url);
    await saveApiKey(db, apiKey.trim());
    setBaseUrl(url);
    setMessage("Configuración guardada en el teléfono.");
  }

  async function capture() {
    setBusy(true);
    setError(null);
    setMessage(null);
    try {
      const permission = await Location.requestForegroundPermissionsAsync();
      if (permission.status !== "granted") {
        setError("Sin permiso de ubicación no se registra una posición.");
        return;
      }

      const position = await Location.getCurrentPositionAsync({
        accuracy: Location.Accuracy.Balanced
      });
      const sequence = await nextSequence(db);
      const created = createTelemetry({
        eventId: Crypto.randomUUID(),
        vehicleId,
        latitude: position.coords.latitude,
        longitude: position.coords.longitude,
        sequenceNumber: sequence
      });
      if (!created.ok) {
        setError(created.error);
        return;
      }

      const outbox = createSqliteOutbox(db);
      const inserted = await outbox.enqueue(created.event);
      if (inserted === "duplicate") {
        setError("Ese evento ya estaba guardado.");
        return;
      }

      await writeState(db, "next_sequence", String(sequence + 1));
      setPending(await outbox.listPending(20));
      setPendingCount(await outbox.countPending());
      setMessage("Posición guardada en el teléfono. Intentando enviarla.");
      await sendPending();
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "No se pudo leer la ubicación.");
    } finally {
      setBusy(false);
    }
  }

  async function sendPending() {
    const url = normalizeBaseUrl(baseUrl);
    if (!url || apiKey.trim().length === 0) {
      setError("Guarda la dirección del servicio y la clave antes de sincronizar.");
      return;
    }

    setBusy(true);
    setError(null);
    try {
      const outbox = createSqliteOutbox(db);
      const report = await syncPending(outbox, (events) =>
        postTelemetryBatch(url, apiKey.trim(), events)
      );
      setPending(await outbox.listPending(20));
      setPendingCount(await outbox.countPending());
      if (report.stoppedBecause === "failed" || report.stoppedBecause === "rejected") {
        setError(
          report.pending === 0
            ? "No se pudo enviar."
            : `Quedaron ${report.pending} pendiente(s). Se reintentan con el mismo identificador.`
        );
        return;
      }

      setMessage(
        report.accepted === 0
          ? "No hay posiciones pendientes."
          : `El servicio aceptó ${report.accepted} posición(es).`
      );
    } finally {
      setBusy(false);
    }
  }

  if (!ready) {
    return (
      <SafeAreaView style={styles.screen}>
        <ActivityIndicator />
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={styles.screen}>
      <ScrollView contentContainerStyle={styles.content}>
        <Text style={styles.eyebrow}>FleetPulse</Text>
        <Text style={styles.title}>Posición del vehículo</Text>
        <Text style={styles.note}>
          Cada posición se guarda en el teléfono antes de enviarla. Solo se marca como aceptada
          cuando el servicio responde 202. Con la app cerrada no se registra ni se envía nada.
        </Text>

        <Text style={styles.label}>Vehículo</Text>
        <Text selectable style={styles.vehicle}>{vehicleId}</Text>

        <Text style={styles.label}>Dirección del servicio</Text>
        <TextInput
          autoCapitalize="none"
          autoCorrect={false}
          onChangeText={setBaseUrl}
          placeholder="http://192.168.1.20:8080"
          style={styles.input}
          value={baseUrl}
        />

        <Text style={styles.label}>Clave de acceso</Text>
        <TextInput
          autoCapitalize="none"
          autoCorrect={false}
          onChangeText={setApiKey}
          secureTextEntry
          style={styles.input}
          value={apiKey}
        />

        <Pressable onPress={() => void saveSettings()} style={styles.secondary}>
          <Text style={styles.secondaryText}>Guardar</Text>
        </Pressable>

        <Pressable disabled={busy} onPress={() => void capture()} style={styles.primary}>
          <Text style={styles.primaryText}>{busy ? "Trabajando…" : "Registrar posición"}</Text>
        </Pressable>
        <Pressable disabled={busy} onPress={() => void sendPending()} style={styles.secondary}>
          <Text style={styles.secondaryText}>Sincronizar pendientes</Text>
        </Pressable>

        {message && <Text style={styles.message}>{message}</Text>}
        {error && <Text style={styles.error}>{error}</Text>}

        <Text style={styles.label}>Pendientes ({pendingCount})</Text>
        {pendingCount === 0 ? (
          <Text style={styles.note}>No hay posiciones esperando envío.</Text>
        ) : (
          pending.map((event) => (
            <Text key={event.eventId} style={styles.pending}>
              {event.latitude}, {event.longitude} · secuencia {event.sequenceNumber}
            </Text>
          ))
        )}
      </ScrollView>
    </SafeAreaView>
  );
}

async function nextSequence(db: ReturnType<typeof useSQLiteContext>): Promise<number> {
  const stored = await readState(db, "next_sequence");
  const value = stored ? Number(stored) : 1;
  return Number.isInteger(value) && value >= 1 ? value : 1;
}

async function readApiKey(db: ReturnType<typeof useSQLiteContext>): Promise<string | null> {
  try {
    return await SecureStore.getItemAsync(API_KEY);
  } catch {
    return readState(db, API_KEY);
  }
}

async function saveApiKey(db: ReturnType<typeof useSQLiteContext>, value: string): Promise<void> {
  try {
    await SecureStore.setItemAsync(API_KEY, value);
    await db.runAsync(`DELETE FROM device_state WHERE key = ?`, API_KEY);
  } catch {
    await writeState(db, API_KEY, value);
  }
}

const styles = StyleSheet.create({
  screen: {
    flex: 1,
    backgroundColor: "#f4f7f8"
  },
  content: {
    padding: 20,
    gap: 10
  },
  eyebrow: {
    color: "#0f6f73",
    fontWeight: "700"
  },
  title: {
    fontSize: 28,
    fontWeight: "700",
    color: "#142226"
  },
  note: {
    color: "#44555a",
    lineHeight: 20
  },
  label: {
    marginTop: 8,
    fontWeight: "700",
    color: "#142226"
  },
  vehicle: {
    color: "#142226"
  },
  input: {
    backgroundColor: "#ffffff",
    borderColor: "#d5e0e3",
    borderRadius: 12,
    borderWidth: 1,
    paddingHorizontal: 12,
    paddingVertical: 10
  },
  primary: {
    marginTop: 8,
    backgroundColor: "#0f6f73",
    borderRadius: 12,
    paddingVertical: 14,
    alignItems: "center"
  },
  primaryText: {
    color: "#ffffff",
    fontWeight: "700"
  },
  secondary: {
    borderColor: "#0f6f73",
    borderRadius: 12,
    borderWidth: 1,
    paddingVertical: 12,
    alignItems: "center"
  },
  secondaryText: {
    color: "#0f6f73",
    fontWeight: "700"
  },
  message: {
    color: "#0f6f73"
  },
  error: {
    color: "#9d2c2c"
  },
  pending: {
    color: "#142226"
  }
});
