# aqsplugin-extdb-dotnet

Port funcional a **.NET 10 / ASP.NET Core Minimal API** del plugin puente
[`aqsplugin-extdb`](https://github.com/danielramirezasyn/aqsplugin-extdb) (Python/FastAPI).
Expone un bridge HTTP stateless entre Oracle ApiQuickServe/ORDS/APEX y bases de datos externas
(SQL Server, MySQL, PostgreSQL): registra conexiones por alias (con password encriptado) y
permite ejecutar queries/stored procedures contra esas bases sin exponer credenciales en cada
request PL/SQL.

## Estructura

```
src/AqsPluginExtDb/
├── Program.cs              # bootstrap, DI, middleware, mapeo de endpoints
├── Models/                 # DTOs de request/response
├── Drivers/                # IDbDriver + SqlServerDriver/MySqlDriver/PostgresDriver
└── Core/
    ├── Security/            # ApiKeyAuthMiddleware, IpAllowlistMiddleware
    ├── Crypto/              # CryptoService (AES-256-GCM + PBKDF2)
    ├── Storage/             # ConnectionStore (persistencia en /data/connections.json)
    ├── Options/             # PluginOptions, PoolOptions (bind de env vars)
    └── Validation/          # IdentifierValidator (nombres de SP seguros)
tests/AqsPluginExtDb.Tests/  # xUnit v3
```

## Variables de entorno

| Variable | Requerida | Default | Descripción |
|---|---|---|---|
| `PLUGIN_API_KEY` | Sí | — | Valor esperado en el header `X-API-Key`. La app falla al arrancar si falta. |
| `ENCRYPTION_KEY` | Sí | — | Passphrase para derivar la clave AES-256-GCM (PBKDF2). La app falla al arrancar si falta. |
| `POOL_ENABLED` | No | `true` | Habilita/deshabilita el pooling nativo de ADO.NET. |
| `POOL_MIN_SIZE` | No | `2` | Min Pool Size por connection string. |
| `POOL_MAX_SIZE` | No | `10` | Max Pool Size por connection string. |
| `POOL_TIMEOUT` | No | `30` | Timeout de conexión (segundos). |
| `ALLOWED_IPS` | No | (vacío = permite todo) | CSV de IPs/CIDRs permitidos, ej. `10.0.0.0/24,192.168.1.10`. |
| `PORT` | No | `8000` | Puerto en el que escucha Kestrel dentro del contenedor. |

## Build & run

### Local (requiere .NET 10 SDK)

```bash
export PLUGIN_API_KEY="dev-api-key"
export ENCRYPTION_KEY="dev-encryption-key"
dotnet run --project src/AqsPluginExtDb
```

### Docker

```bash
docker network inspect oracle-ha-net >/dev/null 2>&1 || docker network create oracle-ha-net
docker compose up --build -d
```

El servicio queda expuesto en `http://localhost:9001` (mapeado a `8000` dentro del contenedor).

### Tests

```bash
dotnet test tests/AqsPluginExtDb.Tests
```

## Endpoints

### `GET /health` — sin autenticación

```bash
curl -s http://localhost:9001/health
# {"status":"ok"}
```

### `POST /setup` — registra/actualiza un alias

```bash
curl -s -X POST http://localhost:9001/setup \
  -H "X-API-Key: dev-api-key" \
  -H "Content-Type: application/json" \
  -d '{
    "alias": "core_sqlserver",
    "db_type": "sqlserver",
    "host": "10.0.1.45",
    "port": 1433,
    "database": "CoreBancario",
    "user": "apireader",
    "password": "s3cret",
    "driver_options": { "Encrypt": "false" }
  }'
```

`db_type` acepta `sqlserver`, `mysql` o `postgresql`. La password se encripta con AES-256-GCM
antes de persistirse en `/data/connections.json`.

### `GET /setup` — lista aliases (sin exponer passwords)

```bash
curl -s http://localhost:9001/setup -H "X-API-Key: dev-api-key"
```

### `DELETE /setup/{alias}` — elimina un alias

```bash
curl -s -X DELETE http://localhost:9001/setup/core_sqlserver -H "X-API-Key: dev-api-key"
```

### `POST /execute` — ejecuta contra un alias registrado

Modo **`query`** (SELECT / UPDATE / DELETE parametrizado — no DDL). Los parámetros se
bindean posicionalmente: el statement debe referenciarlos como `@p0`, `@p1`, ... (funciona
igual en los tres motores).

```bash
curl -s -X POST http://localhost:9001/execute \
  -H "X-API-Key: dev-api-key" \
  -H "Content-Type: application/json" \
  -d '{
    "alias": "core_sqlserver",
    "mode": "query",
    "statement": "SELECT id, nombre FROM pacientes WHERE id = @p0",
    "params": [42]
  }'
```

```bash
curl -s -X POST http://localhost:9001/execute \
  -H "X-API-Key: dev-api-key" \
  -H "Content-Type: application/json" \
  -d '{
    "alias": "core_sqlserver",
    "mode": "query",
    "statement": "UPDATE pacientes SET estado = @p0 WHERE id = @p1",
    "params": ["activo", 42]
  }'
```

Modo **`callable`** (stored procedures). `statement` es únicamente el nombre del
procedimiento (validado contra un identificador seguro); `params` se bindean en orden:

```bash
curl -s -X POST http://localhost:9001/execute \
  -H "X-API-Key: dev-api-key" \
  -H "Content-Type: application/json" \
  -d '{
    "alias": "core_sqlserver",
    "mode": "callable",
    "statement": "SP_PROCESAR_RECETAS",
    "params": [42, "2026-07-07"]
  }'
```

> Nota PostgreSQL: `callable` genera un `CALL proc(@p0, ...)`, válido para PROCEDURES
> (PostgreSQL 11+, `CREATE PROCEDURE`). Para invocar una FUNCTION, usá el modo `query` con
> `"statement": "SELECT * FROM mi_funcion(@p0, @p1)"`.

### Respuesta normalizada de `/execute`

```json
{
  "status": "ok",
  "rows_affected": 1,
  "columns": ["id", "nombre"],
  "data": [{ "id": 42, "nombre": "Juan Pérez" }],
  "execution_ms": 8,
  "error_code": null,
  "error_message": null
}
```

En error (`status: "error"`), `error_code` es uno de: `VALIDATION_ERROR`, `ALIAS_NOT_FOUND`,
`UNSUPPORTED_DRIVER`, `DECRYPTION_ERROR`, `CONNECTION_FAILED`, `QUERY_FAILED`, `TIMEOUT`,
`INTERNAL_ERROR`. El proceso nunca crashea: cualquier excepción no controlada es capturada por
el exception handler global y devuelve este mismo formato JSON.

## Ejemplo end-to-end verificado (SQL Server en la misma red Docker)

Si tu SQL Server (o Azure SQL Edge) corre como otro contenedor en `oracle-ha-net`, usá su
**nombre de contenedor** como `host` — Docker lo resuelve por DNS interno, no hace falta IP ni
`host.docker.internal`. Ejemplo real probado contra un contenedor `sqledge` con una base
`demo` y una tabla:

```sql
CREATE TABLE clientes (
    id INT IDENTITY(1,1) PRIMARY KEY,
    nombre VARCHAR(100) NOT NULL,
    email VARCHAR(100),
    ciudad VARCHAR(50),
    fecha_registro DATE DEFAULT GETDATE()
);
```

**1. Registrar el alias:**

```bash
curl -s -X POST http://localhost:9001/setup \
  -H "X-API-Key: change-me-to-a-long-random-value" \
  -H "Content-Type: application/json" \
  -d '{
    "alias": "demo_sqlserver",
    "db_type": "sqlserver",
    "host": "sqledge",
    "port": 1433,
    "database": "demo",
    "user": "sa",
    "password": "IngresandoPanama01."
  }'
```

**2. SELECT sobre la tabla completa:**

```bash
curl -s -X POST http://localhost:9001/execute \
  -H "X-API-Key: change-me-to-a-long-random-value" \
  -H "Content-Type: application/json" \
  -d '{
    "alias": "demo_sqlserver",
    "mode": "query",
    "statement": "SELECT id, nombre, email, ciudad, fecha_registro FROM clientes",
    "params": []
  }'
```

**3. SELECT filtrado con bind param:**

```bash
curl -s -X POST http://localhost:9001/execute \
  -H "X-API-Key: change-me-to-a-long-random-value" \
  -H "Content-Type: application/json" \
  -d '{
    "alias": "demo_sqlserver",
    "mode": "query",
    "statement": "SELECT id, nombre, email, ciudad FROM clientes WHERE id = @p0",
    "params": [1]
  }'
```

**4. UPDATE con bind params:**

```bash
curl -s -X POST http://localhost:9001/execute \
  -H "X-API-Key: change-me-to-a-long-random-value" \
  -H "Content-Type: application/json" \
  -d '{
    "alias": "demo_sqlserver",
    "mode": "query",
    "statement": "UPDATE clientes SET ciudad = @p0 WHERE id = @p1",
    "params": ["Panama City", 1]
  }'
```

> Reemplazá `change-me-to-a-long-random-value` por el `PLUGIN_API_KEY` real que hayas
> configurado en tu `docker-compose.yml` antes de usar esto fuera de desarrollo local.

## Uso desde PL/SQL (`apex_web_service.make_rest_request`)

```sql
DECLARE
    l_response   CLOB;
    l_body       CLOB;
    l_status     VARCHAR2(20);
    l_error_code VARCHAR2(50);
BEGIN
    apex_web_service.g_request_headers(1).name  := 'X-API-Key';
    apex_web_service.g_request_headers(1).value := 'dev-api-key';
    apex_web_service.g_request_headers(2).name  := 'Content-Type';
    apex_web_service.g_request_headers(2).value := 'application/json';

    l_body := '{
        "alias": "core_sqlserver",
        "mode": "callable",
        "statement": "SP_PROCESAR_RECETAS",
        "params": [:p_id_receta, :p_fecha]
    }';

    l_response := apex_web_service.make_rest_request(
        p_url         => 'http://aqsplugin-extdb-dotnet:8000/execute',
        p_http_method => 'POST',
        p_body        => l_body
    );

    apex_json.parse(l_response);

    l_status     := apex_json.get_varchar2(p_path => 'status');
    l_error_code := apex_json.get_varchar2(p_path => 'error_code');

    IF l_status = 'ok' THEN
        FOR i IN 1 .. apex_json.get_count(p_path => 'data') LOOP
            -- ej.: apex_json.get_varchar2(p_path => 'data[%d].nombre', p0 => i)
            NULL;
        END LOOP;
    ELSE
        raise_application_error(-20001, 'SP_PROCESAR_RECETAS falló: ' || l_error_code);
    END IF;
END;
/
```

También es posible parsear la respuesta con `JSON_TABLE` sobre `l_response`, proyectando
`data` como una tabla anidada de columnas dinámicas.

## Seguridad

- Autenticación por header `X-API-Key`, comparada en tiempo constante
  (`CryptographicOperations.FixedTimeEquals` sobre el hash SHA-256 de ambos valores).
- Passwords encriptados en reposo con AES-256-GCM; clave derivada de `ENCRYPTION_KEY` vía
  PBKDF2-HMAC-SHA256 (100k iteraciones) con salt aleatorio por valor.
- Allowlist de IPs opcional (`ALLOWED_IPS`, soporta CIDR) vía `System.Net.IPNetwork`.
- Los nombres de procedimiento en modo `callable` se validan contra un identificador seguro
  antes de interpolarse en el `EXEC`/`CALL`; los parámetros siempre viajan como bind
  parameters, nunca concatenados en el SQL.
- No se loguean passwords ni valores de parámetros.
