# Servicio-FleetPlus

Servicio de ingesta de telemetría. Un solo proceso ASP.NET Core valida el lote, lo publica en RabbitMQ y lo guarda en PostgreSQL con TimescaleDB. La base se llama `fleetplus`.

La contraseña y la clave de API no están en el repositorio. Copia el ejemplo y cambia los valores antes de levantar el entorno:

```bash
cp .env.example .env
docker compose up -d --build
```

`POSTGRES_PORT` es el puerto publicado en el host. Dentro de la red de Compose, PostgreSQL escucha en el 5432. Si ese puerto del host ya está ocupado, cámbialo en `.env`.

La API queda en `http://localhost:8080`.

- `GET /health` comprueba PostgreSQL y RabbitMQ.
- `POST /api/telemetry/batches` recibe el lote. Requiere la cabecera `X-Api-Key`.
- Responde `202` solo después de que RabbitMQ confirma la publicación. El consumidor confirma el mensaje solo después de que `telemetry.ingest_event` termina.
- Un `eventId` repetido no duplica el historial. Un evento más antiguo no reemplaza una posición más reciente.
- Un fallo transitorio se reintenta hasta 5 veces, con espera creciente, y después pasa a `fleetpulse.telemetry.dlq`.
- El lote admite como máximo 100 eventos. Si uno no es válido, no se publica ninguno.

Las migraciones viven en `infra/postgres/migrations` y se aplican al crear el volumen. No hay borrado automático del historial: la retención queda pendiente de una ventana operativa.

Para ejecutar las pruebas hace falta el SDK de .NET 8:

```bash
dotnet test FleetPulse.sln
```
