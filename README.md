# Servicio-FleetPlus

MVP de un portal para monitorear una flota. Un solo proceso ASP.NET Core valida la telemetría, la publica en RabbitMQ y la guarda en PostgreSQL con TimescaleDB. El portal React recibe la posición vigente por SignalR. Una app móvil guarda cada punto en SQLite antes de enviarlo. La base se llama `fleetplus`.

No es un conjunto de microservicios. El circuit breaker protege la llamada a PostgreSQL dentro de ese mismo proceso. Separar la ingesta en varios servicios no aportaba un límite operativo y sí añadía fallos de red.

## Ejecución local

Hace falta Docker, el SDK de .NET 8 y Node.js. La contraseña y la clave de API no están en el repositorio.

```bash
cp .env.example .env
docker compose up -d --build
```

`POSTGRES_PORT` es el puerto publicado en el host. Dentro de la red de Compose, PostgreSQL escucha en el 5432. Si el 5432 del host ya está ocupado, cámbialo en `.env`. En esta máquina el valor usado es `5433`.

Si la API no queda publicada por Compose, se puede levantar el mismo proyecto en el host:

```bash
dotnet run --project src/FleetPulse.Api/FleetPulse.Api.csproj --launch-profile http
```

Ese perfil escucha en todas las interfaces del puerto 8080 para que el teléfono llegue por la IP de la máquina.

## Portal, API y datos

La API y el portal quedan en `http://localhost:8080`. Swagger está en `http://localhost:8080/swagger`. En Authorize usa la cabecera `X-Api-Key` con el valor de `TELEMETRY_API_KEY`. En el ejemplo es `change-me`. La clave se escribe en el portal y no viaja dentro del código del frontend.

- `GET /` muestra la flota en una tabla. No hay mapa ni un registro de alertas.
- `GET /health` comprueba PostgreSQL y RabbitMQ.
- `GET /api/vehicles` devuelve la posición actual. `GET /api/vehicles/{id}/positions` devuelve el historial. Un vehículo desconocido responde 404. Un rango sin puntos responde una lista vacía.
- `POST /api/telemetry/batches` recibe el lote, como máximo 100 eventos. Si uno no es válido, no se publica ninguno. Las rutas `/api` y `/hubs` requieren `X-Api-Key`.
- Responde `202` solo después de que RabbitMQ confirma la publicación. El consumidor confirma el mensaje solo después de que `telemetry.ingest_event` termina.
- Un `eventId` repetido no duplica el historial. Un evento más antiguo no reemplaza una posición más reciente.
- Un fallo transitorio se reintenta hasta 5 veces, con espera creciente, y después pasa a `fleetpulse.telemetry.dlq`.
- SignalR publica `positionUpdated` en `/hubs/telemetry` cuando un evento nuevo queda guardado. El portal no reemplaza una posición reciente con una más antigua.
- `POST /api/agent/questions` responde con los datos guardados. El modo es determinista: `usesLanguageModel` es `false`. No inventa vehículos, posiciones ni incidentes. No hay registro de incidentes, y una pregunta por alertas lo dice en lugar de devolver una lista vacía.

RabbitMQ Management está en `http://localhost:15672`, con el usuario y la contraseña de `RABBITMQ_USER` y `RABBITMQ_PASSWORD`. Las colas son `fleetpulse.telemetry`, `fleetpulse.telemetry.retry` y `fleetpulse.telemetry.dlq`.

La base se consulta en el puerto publicado de Postgres, base `fleetplus`. El historial vive en `telemetry.vehicle_positions`. El estado vigente vive en `fleet.vehicle_state`. No hay tabla maestra de placas: el `vehicleId` lo aporta el cliente y el primer evento crea el estado.

Las migraciones viven en `infra/postgres/migrations` y se aplican al crear el volumen. No hay borrado automático del historial. La retención queda pendiente de una ventana operativa. TimescaleDB está elegido porque el historial es una serie temporal por vehículo: la partición por `recorded_at` sostiene la consulta reciente sin mezclarla con la fila vigente, que es la única que el portal y el agente necesitan para saber dónde está cada vehículo ahora.

## Agente

El agente no llama a un modelo de lenguaje. Traduce una pregunta corta a tres herramientas de la capa de aplicación: listar la flota, leer la telemetría de las últimas 24 horas de un `vehicleId`, o informar que no existe un registro de incidentes. No ejecuta SQL ni comandos. Una consulta como «qué vehículos llevan detenidos más de 20 minutos en zonas críticas» no tiene datos de zonas ni de detención, así que no se inventa la respuesta.

## App móvil

La app está en `mobile`. Guarda cada posición en SQLite antes de enviarla y conserva la cola si no hay red. Solo la marca como aceptada cuando `POST /api/telemetry/batches` responde 202. Un reintento usa el mismo `eventId`. La clave se escribe en el teléfono y no va en el repositorio.

La captura y la sincronización ocurren con la app abierta. No hay seguimiento en segundo plano: Android no relanza el proceso al cerrarlo, e iOS detiene la ubicación si el usuario termina la app. Un cierre a medias deja los pendientes en SQLite.

```bash
cd mobile && npx expo start
```

En el teléfono, la dirección del servicio tiene que ser la IP de la máquina, no `localhost`. Desde el navegador, la app puede llamar al servicio en otro puerto. El servicio acepta ese origen solo si es esta máquina o una dirección de la red local. La clave sigue siendo obligatoria en la petición real.

## Pruebas

```bash
dotnet test tests/FleetPulse.Domain.Tests/FleetPulse.Domain.Tests.csproj
dotnet test tests/FleetPulse.Application.Tests/FleetPulse.Application.Tests.csproj
cd web && npm test && npm run build
cd mobile && npm test && npx tsc --noEmit
k6 run tests/load/telemetry.js
```

Las pruebas de `FleetPulse.Infrastructure.Tests` escriben en la base local. No forman parte del flujo de GitHub Actions, porque ese flujo no levanta TimescaleDB.

`k6 run` simula 200 vehículos contra el servicio local. Parte de ellos reintenta el mismo `eventId` y parte envía coordenadas inválidas. Después comprueba que el duplicado quedó una sola vez, que el lote inválido no creó el vehículo y que un evento antiguo no pisa la posición vigente. Esos vehículos usan identificadores `a1000000-0000-4000-8000-*`. La clave sale de `TELEMETRY_API_KEY` si está definida. Si no, usa `change-me`. `BASE_URL` cambia el destino.

## Observabilidad

Prometheus queda en `http://localhost:9090` y Grafana en `http://localhost:3000`. El usuario inicial es `admin` y la contraseña es `GRAFANA_ADMIN_PASSWORD`. En el ejemplo es `change-me`. El tablero está en `http://localhost:3000/d/fleetpulse-prometheus/prometheus`. Hoy Prometheus se recoge a sí mismo. El servicio de flota no publica métricas de negocio.

## Infraestructura como código

La definición de nube está en `infra/terraform`. `create_cloud_resources` vale `false`, así que un plan no crea el clúster, el grupo de logs ni el parámetro de la clave. La clave entra como variable sensible y no tiene valor en el repositorio. No se ha ejecutado `terraform apply`. No hay recursos en AWS ni en Azure. El entorno que corre es Docker Compose.

```bash
cd infra/terraform
terraform init -backend=false
terraform fmt -check
terraform validate
```

## Integración continua

GitHub Actions, en `.github/workflows/ci.yml`, ejecuta las pruebas de dominio, aplicación, portal y la app móvil. La app usa `bundle exec fastlane test` desde `mobile`. Los carriles `beta` de Android e iOS se detienen a propósito: no hay credenciales de tienda y no se publica nada.

## Auditoría de IA

Estas dos decisiones rechazan un atajo que habría dejado el sistema inseguro o poco defendible. El código final las deja explícitas para poder revisarlas.

### No publicar la base con PostgREST

Al inicio se planteó exponer PostgreSQL con PostgREST para tener un API rápido. Eso habría dejado las tablas abiertas al cliente, sin validar coordenadas ni fechas, sin la clave de acceso y sin la regla de que un evento antiguo no reemplace la posición vigente. Un reintento habría podido duplicar historial o pisar el estado.

Se reemplazó por un solo servicio ASP.NET. El lote se valida en el dominio antes de publicarse. RabbitMQ solo recibe eventos válidos. La escritura pasa por la función `telemetry.ingest_event`, que acepta cada `eventId` una sola vez y actualiza `fleet.vehicle_state` solo si la lectura es más reciente. El mensaje se confirma después de esa escritura, no al recibirlo.

### No presentar lógica determinista como un modelo de lenguaje

Sin una clave de proveedor, un agente montado sobre Semantic Kernel o LangChain y presentado como chat habría tenido que inventar vehículos, zonas y alertas para responder preguntas como «cuáles llevan detenidos más de 20 minutos». También habría sido fácil dejar que el modelo armara SQL.

El agente quedó detrás de `IFleetAgent` y solo llama servicios de aplicación. Las herramientas listan la flota, leen la telemetría de un vehículo o informan que el registro de incidentes no existe. Un vehículo desconocido no se confunde con un historial vacío. La respuesta lleva `usesLanguageModel: false` y el modo `deterministic`. No hay zonas críticas ni un estado de detención en el modelo de datos, así que esa pregunta no se responde con una lista fabricada.
