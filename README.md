# Servicio-FleetPlus

Proyecto desde cero. El primer servicio es PostgreSQL 17 con TimescaleDB. La base se llama `fleetplus`.

La contraseña no está en el repositorio. Copia el ejemplo y cambia el valor antes de levantar el contenedor:

```bash
cp .env.example .env
docker compose up -d
docker compose ps
```

`POSTGRES_PORT` es el puerto publicado en el host. Dentro del contenedor PostgreSQL escucha en el 5432. Si ese puerto del host ya lo usa otro proceso, cámbialo en `.env` y vuelve a levantar el servicio.

Las migraciones viven en `infra/postgres/migrations` y se aplican al crear el volumen:

- `V001__timescaledb.sql` habilita TimescaleDB.
- `V002__vehicle_telemetry.sql` separa el historial (`telemetry.vehicle_positions`) del estado actual (`fleet.vehicle_state`).

`telemetry.ingest_event` acepta cada `event_id` una sola vez. Un evento repetido no duplica el historial. Un evento más antiguo no reemplaza una posición más reciente. No hay borrado automático del historial: la retención queda pendiente de una ventana operativa.
