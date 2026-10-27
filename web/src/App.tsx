import { FormEvent, useEffect, useState } from "react";
import { HubConnectionBuilder, HubConnectionState } from "@microsoft/signalr";
import { askAgent, fetchPositions, fetchVehicles, FleetApiError, type VehiclePosition } from "./fleetApi";
import { mergeVehicle, type VehicleState } from "./mergeVehicle";

type ConnectionStatus = "idle" | "connecting" | "connected" | "disconnected";

export function App() {
  const [apiKey, setApiKey] = useState("");
  const [activeKey, setActiveKey] = useState("");
  const [vehicles, setVehicles] = useState<VehicleState[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [positions, setPositions] = useState<VehiclePosition[]>([]);
  const [loading, setLoading] = useState(false);
  const [positionsLoading, setPositionsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [positionsError, setPositionsError] = useState<string | null>(null);
  const [connectionStatus, setConnectionStatus] = useState<ConnectionStatus>("idle");
  const [question, setQuestion] = useState("");
  const [agentAnswer, setAgentAnswer] = useState<string | null>(null);
  const [agentMode, setAgentMode] = useState<string | null>(null);
  const [agentLoading, setAgentLoading] = useState(false);
  const [agentError, setAgentError] = useState<string | null>(null);

  useEffect(() => {
    if (!activeKey)
      return;

    const connection = new HubConnectionBuilder()
      .withUrl("/hubs/telemetry", { accessTokenFactory: () => activeKey })
      .withAutomaticReconnect()
      .build();

    connection.on("positionUpdated", (update: {
      vehicleId: string;
      eventId: string;
      recordedAt: string;
      latitude: number;
      longitude: number;
      sequenceNumber: number;
    }) => {
      setVehicles((current) => {
        const existing = current.find((vehicle) => vehicle.vehicleId === update.vehicleId);
        const merged = mergeVehicle(existing, {
          vehicleId: update.vehicleId,
          lastEventId: update.eventId,
          sequenceNumber: update.sequenceNumber,
          recordedAt: update.recordedAt,
          latitude: update.latitude,
          longitude: update.longitude
        });
        if (existing && merged === existing)
          return current;

        const next = current.filter((vehicle) => vehicle.vehicleId !== update.vehicleId);
        next.push(merged);
        next.sort((left, right) => Date.parse(right.recordedAt) - Date.parse(left.recordedAt));
        return next;
      });
    });

    connection.onreconnecting(() => setConnectionStatus("connecting"));
    connection.onreconnected(() => setConnectionStatus("connected"));
    connection.onclose(() => setConnectionStatus("disconnected"));

    let stopped = false;
    setConnectionStatus("connecting");
    connection.start()
      .then(() => {
        if (!stopped)
          setConnectionStatus("connected");
      })
      .catch(() => {
        if (!stopped)
          setConnectionStatus("disconnected");
      });

    return () => {
      stopped = true;
      if (connection.state !== HubConnectionState.Disconnected)
        void connection.stop();
    };
  }, [activeKey]);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const key = apiKey.trim();
    if (!key)
      return;

    setLoading(true);
    setError(null);
    setSelectedId(null);
    setPositions([]);
    try {
      const fleet = await fetchVehicles(key);
      setVehicles(fleet);
      setActiveKey(key);
    } catch (caught) {
      setActiveKey("");
      setVehicles([]);
      setError(caught instanceof FleetApiError ? caught.message : "No se pudo consultar la flota.");
    } finally {
      setLoading(false);
    }
  }

  async function selectVehicle(vehicleId: string) {
    setSelectedId(vehicleId);
    setPositionsLoading(true);
    setPositionsError(null);
    try {
      setPositions(await fetchPositions(vehicleId, activeKey));
    } catch (caught) {
      setPositions([]);
      setPositionsError(caught instanceof FleetApiError ? caught.message : "No se pudo consultar el historial.");
    } finally {
      setPositionsLoading(false);
    }
  }

  async function onAsk(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const text = question.trim();
    if (!text || !activeKey)
      return;

    setAgentLoading(true);
    setAgentError(null);
    try {
      const reply = await askAgent(text, activeKey);
      setAgentAnswer(reply.answer);
      setAgentMode(reply.usesLanguageModel ? reply.mode : "Sin modelo de lenguaje");
    } catch (caught) {
      setAgentAnswer(null);
      setAgentMode(null);
      setAgentError(caught instanceof FleetApiError ? caught.message : "No se pudo consultar el agente.");
    } finally {
      setAgentLoading(false);
    }
  }

  return (
    <main className="page">
      <header className="header">
        <div>
          <p className="eyebrow">FleetPulse</p>
          <h1>Flota en vivo</h1>
        </div>
        <ConnectionBadge status={connectionStatus} />
      </header>

      <form className="key-form" onSubmit={onSubmit}>
        <label htmlFor="api-key">Clave de acceso</label>
        <div className="key-row">
          <input
            id="api-key"
            type="password"
            autoComplete="off"
            value={apiKey}
            onChange={(event) => setApiKey(event.target.value)}
          />
          <button type="submit" disabled={loading || apiKey.trim().length === 0}>
            {loading ? "Consultando…" : "Ver flota"}
          </button>
        </div>
      </form>

      {error && <p className="banner error" role="alert">{error}</p>}
      {loading && <p className="banner">Cargando vehículos…</p>}
      {!loading && activeKey && vehicles.length === 0 && (
        <p className="banner">No hay vehículos con posición registrada.</p>
      )}

      {activeKey && (
        <form className="key-form" onSubmit={onAsk}>
          <label htmlFor="agent-question">Pregunta sobre la flota</label>
          <div className="key-row">
            <input
              id="agent-question"
              value={question}
              onChange={(event) => setQuestion(event.target.value)}
            />
            <button type="submit" disabled={agentLoading || question.trim().length === 0}>
              {agentLoading ? "Consultando…" : "Preguntar"}
            </button>
          </div>
          {agentMode && <p className="banner">{agentMode}</p>}
          {agentAnswer && <p className="banner">{agentAnswer}</p>}
          {agentError && <p className="banner error" role="alert">{agentError}</p>}
        </form>
      )}

      {vehicles.length > 0 && (
        <div className="layout">
          <VehicleTable vehicles={vehicles} selectedId={selectedId} onSelect={selectVehicle} />
          <TrackPanel
            loading={positionsLoading}
            error={positionsError}
            positions={selectedId ? positions : []}
            selected={selectedId !== null}
          />
        </div>
      )}
    </main>
  );
}

function ConnectionBadge({ status }: { status: ConnectionStatus }) {
  const label = {
    idle: "Sin conexión en vivo",
    connecting: "Conectando…",
    connected: "En vivo",
    disconnected: "Desconectado"
  }[status];

  return <p className={`badge ${status}`}>{label}</p>;
}

function VehicleTable({
  vehicles,
  selectedId,
  onSelect
}: {
  vehicles: VehicleState[];
  selectedId: string | null;
  onSelect: (vehicleId: string) => void;
}) {
  return (
    <table className="fleet">
      <caption>Posición actual</caption>
      <thead>
        <tr>
          <th>Vehículo</th>
          <th>Hora UTC</th>
          <th>Latitud</th>
          <th>Longitud</th>
          <th>Secuencia</th>
        </tr>
      </thead>
      <tbody>
        {vehicles.map((vehicle) => (
          <tr
            key={vehicle.vehicleId}
            className={vehicle.vehicleId === selectedId ? "selected" : undefined}
            onClick={() => onSelect(vehicle.vehicleId)}
          >
            <td>{vehicle.vehicleId}</td>
            <td>{formatTime(vehicle.recordedAt)}</td>
            <td>{vehicle.latitude.toFixed(5)}</td>
            <td>{vehicle.longitude.toFixed(5)}</td>
            <td>{vehicle.sequenceNumber}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function TrackPanel({
  loading,
  error,
  positions,
  selected
}: {
  loading: boolean;
  error: string | null;
  positions: VehiclePosition[];
  selected: boolean;
}) {
  return (
    <section className="track">
      <h2>Historial reciente</h2>
      {!selected && <p>Selecciona un vehículo para ver su recorrido.</p>}
      {selected && loading && <p>Cargando historial…</p>}
      {selected && error && <p className="banner error" role="alert">{error}</p>}
      {selected && !loading && !error && positions.length === 0 && (
        <p>No hay posiciones en las últimas 24 horas.</p>
      )}
      {selected && positions.length > 0 && (
        <ol>
          {positions.map((position) => (
            <li key={position.eventId}>
              <span>{formatTime(position.recordedAt)}</span>
              <span>{position.latitude.toFixed(5)}, {position.longitude.toFixed(5)}</span>
              <span>Secuencia {position.sequenceNumber}</span>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}

function formatTime(value: string): string {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime()))
    return value;

  return parsed.toISOString().replace(".000Z", "Z");
}
