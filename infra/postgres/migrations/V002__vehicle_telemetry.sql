-- Historial temporal y estado actual viven en esquemas distintos.
-- No hay política de retención: todavía no hay una ventana operativa
-- aprobada. El historial se conserva hasta definirla.
-- Las escrituras de ingesta pasan por telemetry.ingest_event para que
-- un reintento sea seguro: el event_id se acepta una sola vez y un
-- evento más antiguo no reemplaza el estado más reciente.

CREATE SCHEMA IF NOT EXISTS fleet;
CREATE SCHEMA IF NOT EXISTS telemetry;

CREATE TABLE telemetry.processed_events (
    event_id uuid PRIMARY KEY,
    vehicle_id uuid NOT NULL,
    recorded_at timestamptz NOT NULL,
    processed_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

CREATE TABLE telemetry.vehicle_positions (
    event_id uuid NOT NULL,
    vehicle_id uuid NOT NULL,
    recorded_at timestamptz NOT NULL,
    latitude double precision NOT NULL,
    longitude double precision NOT NULL,
    sequence_number bigint NOT NULL,
    ingested_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    CONSTRAINT vehicle_positions_pkey PRIMARY KEY (event_id, recorded_at),
    CONSTRAINT vehicle_positions_latitude_chk CHECK (latitude >= -90 AND latitude <= 90),
    CONSTRAINT vehicle_positions_longitude_chk CHECK (longitude >= -180 AND longitude <= 180),
    CONSTRAINT vehicle_positions_sequence_chk CHECK (sequence_number >= 0),
    CONSTRAINT vehicle_positions_recorded_at_chk CHECK (
        recorded_at >= TIMESTAMPTZ '2000-01-01 UTC'
    )
);

SELECT create_hypertable(
    'telemetry.vehicle_positions',
    'recorded_at',
    if_not_exists => TRUE
);

CREATE INDEX vehicle_positions_vehicle_time_idx
    ON telemetry.vehicle_positions (vehicle_id, recorded_at DESC);

CREATE TABLE fleet.vehicle_state (
    vehicle_id uuid PRIMARY KEY,
    last_event_id uuid NOT NULL,
    last_sequence bigint NOT NULL,
    recorded_at timestamptz NOT NULL,
    latitude double precision NOT NULL,
    longitude double precision NOT NULL,
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    CONSTRAINT vehicle_state_latitude_chk CHECK (latitude >= -90 AND latitude <= 90),
    CONSTRAINT vehicle_state_longitude_chk CHECK (longitude >= -180 AND longitude <= 180),
    CONSTRAINT vehicle_state_sequence_chk CHECK (last_sequence >= 0)
);

CREATE INDEX vehicle_state_recorded_at_idx
    ON fleet.vehicle_state (recorded_at DESC);

CREATE OR REPLACE FUNCTION telemetry.ingest_event(
    p_event_id uuid,
    p_vehicle_id uuid,
    p_recorded_at timestamptz,
    p_latitude double precision,
    p_longitude double precision,
    p_sequence_number bigint
) RETURNS text
LANGUAGE plpgsql
AS $$
BEGIN
    IF p_event_id IS NULL OR p_vehicle_id IS NULL THEN
        RAISE EXCEPTION 'event_id and vehicle_id are required'
            USING ERRCODE = '22023';
    END IF;

    IF p_recorded_at IS NULL
        OR p_recorded_at < TIMESTAMPTZ '2000-01-01 UTC'
        OR p_recorded_at > clock_timestamp() + INTERVAL '5 minutes'
    THEN
        RAISE EXCEPTION 'invalid recorded_at'
            USING ERRCODE = '22007';
    END IF;

    IF p_latitude IS NULL OR p_latitude < -90 OR p_latitude > 90
        OR p_longitude IS NULL OR p_longitude < -180 OR p_longitude > 180
    THEN
        RAISE EXCEPTION 'invalid coordinates'
            USING ERRCODE = '22023';
    END IF;

    IF p_sequence_number IS NULL OR p_sequence_number < 0 THEN
        RAISE EXCEPTION 'invalid sequence_number'
            USING ERRCODE = '22023';
    END IF;

    INSERT INTO telemetry.processed_events (event_id, vehicle_id, recorded_at)
    VALUES (p_event_id, p_vehicle_id, p_recorded_at)
    ON CONFLICT (event_id) DO NOTHING;

    IF NOT FOUND THEN
        RETURN 'duplicate';
    END IF;

    INSERT INTO telemetry.vehicle_positions (
        event_id,
        vehicle_id,
        recorded_at,
        latitude,
        longitude,
        sequence_number
    ) VALUES (
        p_event_id,
        p_vehicle_id,
        p_recorded_at,
        p_latitude,
        p_longitude,
        p_sequence_number
    );

    INSERT INTO fleet.vehicle_state (
        vehicle_id,
        last_event_id,
        last_sequence,
        recorded_at,
        latitude,
        longitude
    ) VALUES (
        p_vehicle_id,
        p_event_id,
        p_sequence_number,
        p_recorded_at,
        p_latitude,
        p_longitude
    )
    ON CONFLICT (vehicle_id) DO UPDATE
    SET last_event_id = EXCLUDED.last_event_id,
        last_sequence = EXCLUDED.last_sequence,
        recorded_at = EXCLUDED.recorded_at,
        latitude = EXCLUDED.latitude,
        longitude = EXCLUDED.longitude,
        updated_at = clock_timestamp()
    WHERE fleet.vehicle_state.recorded_at < EXCLUDED.recorded_at
        OR (
            fleet.vehicle_state.recorded_at = EXCLUDED.recorded_at
            AND fleet.vehicle_state.last_sequence < EXCLUDED.last_sequence
        );

    RETURN 'inserted';
END;
$$;
