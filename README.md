# Servicio-FleetPlus

Servicio de ingesta de telemetría. Un solo proceso ASP.NET Core valida el lote, lo publica en RabbitMQ y lo guarda en PostgreSQL con TimescaleDB. La base se llama `fleetplus`.

La contraseña y la clave de API no están en el repositorio. Copia el ejemplo y cambia los valores antes de levantar el entorno:

```bash
cp .env.example .env
docker compose up -d --build
```

`POSTGRES_PORT` es el puerto publicado en el host. Dentro de la red de Compose, PostgreSQL escucha en el 5432. Si ese puerto del host ya está ocupado, cámbialo en `.env`.

La API y el portal quedan en `http://localhost:8080`. Swagger está en `http://localhost:8080/swagger`. En Authorize usa la cabecera `X-Api-Key` con el valor de `TELEMETRY_API_KEY`.

- `GET /` muestra la flota. La clave se escribe en la página y no viaja dentro del código del frontend.
- `GET /health` comprueba PostgreSQL y RabbitMQ.
- `GET /api/vehicles` devuelve la posición actual. `GET /api/vehicles/{id}/positions` devuelve el historial. Un vehículo desconocido responde 404; un rango sin puntos responde una lista vacía.
- `POST /api/telemetry/batches` recibe el lote. Las rutas `/api` y `/hubs` requieren `X-Api-Key`.
- SignalR publica `positionUpdated` en `/hubs/telemetry` cuando un evento nuevo queda guardado. El portal no reemplaza una posición reciente con una más antigua.
- `POST /api/agent/questions` responde con los datos guardados. El modo actual es determinista: no usa un modelo de lenguaje y no inventa vehículos, posiciones ni incidentes. No hay registro de incidentes.
- Responde `202` solo después de que RabbitMQ confirma la publicación. El consumidor confirma el mensaje solo después de que `telemetry.ingest_event` termina.
- Un `eventId` repetido no duplica el historial. Un evento más antiguo no reemplaza una posición más reciente.
- Un fallo transitorio se reintenta hasta 5 veces, con espera creciente, y después pasa a `fleetpulse.telemetry.dlq`.
- El lote admite como máximo 100 eventos. Si uno no es válido, no se publica ninguno.

Las migraciones viven en `infra/postgres/migrations` y se aplican al crear el volumen. No hay borrado automático del historial: la retención queda pendiente de una ventana operativa.

Para ejecutar las pruebas hace falta el SDK de .NET 8 y Node.js:

```bash
dotnet test FleetPulse.sln
cd web && npm test && npm run build
```
