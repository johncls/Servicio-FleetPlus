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

La app móvil está en `mobile`. Guarda cada posición en SQLite antes de enviarla y conserva la cola si no hay red. Solo marca el evento como aceptado cuando `POST /api/telemetry/batches` responde 202. Un reintento usa el mismo `eventId`. La clave se escribe en el teléfono y no va en el repositorio.

Desde el navegador, la app puede llamar al servicio en otro puerto. El servicio acepta ese origen solo si es esta máquina o una dirección de la red local. La clave sigue siendo obligatoria en la petición real.

La captura y la sincronización ocurren con la app abierta. No hay seguimiento en segundo plano: Android no relanza el proceso al cerrarlo, e iOS detiene la ubicación si el usuario termina la app. Un cierre a medias deja los pendientes en SQLite. En el teléfono, la dirección del servicio tiene que ser la IP de la máquina, no `localhost`. El perfil local escucha en todas las interfaces del puerto 8080 para que el teléfono pueda llegar.

Para ejecutar las pruebas hace falta el SDK de .NET 8 y Node.js:

```bash
dotnet test FleetPulse.sln
cd web && npm test && npm run build
cd mobile && npm test && npx tsc --noEmit
k6 run tests/load/telemetry.js
```

`k6 run` simula 200 vehículos contra el servicio local. También reintenta un `eventId`, rechaza un lote inválido y comprueba que un evento antiguo no pisa la posición vigente. Los vehículos de esa prueba usan identificadores `a1000000-0000-4000-8000-*`. La clave sale de `TELEMETRY_API_KEY` si está definida; si no, usa `change-me`. `BASE_URL` cambia el destino.

Para abrir la app: `cd mobile && npx expo start`.

Prometheus queda en `http://localhost:9090` y Grafana en `http://localhost:3000`. El usuario inicial es `admin` y la contraseña es `GRAFANA_ADMIN_PASSWORD` (en el ejemplo, `change-me`). Grafana ya tiene a Prometheus como origen de datos.

La definición de nube está en `infra/terraform`. `create_cloud_resources` vale `false`, así que un plan no crea el clúster, el grupo de logs ni el parámetro de la clave. La clave entra como variable sensible y no tiene valor en el repositorio. No se ha ejecutado `terraform apply`.

```bash
cd infra/terraform
terraform init -backend=false
terraform fmt -check
terraform validate
```

GitHub Actions, en `.github/workflows/ci.yml`, ejecuta las pruebas de dominio, aplicación, portal y la app móvil. La app usa `bundle exec fastlane test` desde `mobile`. Los carriles `beta` de Android e iOS se detienen a propósito: no hay credenciales de tienda y no se publica nada. Las pruebas de infraestructura contra Timescale siguen siendo locales, porque el flujo no levanta la base.
