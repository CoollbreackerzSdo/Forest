# Forest

Proyecto personal de práctica para **medir y validar conocimientos propios** en desarrollo
backend con .NET. No es un producto de producción ni una librería reutilizable: sirve como
laboratorio para probar patrones, librerías y arquitectura en un escenario realista.

## Stack

- **.NET 11** / ASP.NET Core Minimal APIs
- **.NET Aspire** (AppHost + ServiceDefaults) para orquestación local
- **PostgreSQL** (EF Core + migraciones)
- **Redis** (caché distribuida)
- **RustFS** (almacenamiento de objetos compatible con S3)
- **JWT Bearer** (autenticación)
- **FluentValidation**, **Riok.Mapperly**, **Ardalis.Result**
- **Serilog**, **Scalar** (documentación OpenAPI)

## Estructura

```
Forest/
├── Server/
│   ├── Forest.Host/        # AppHost de Aspire (orquesta Postgres, Redis, RustFS y la API)
│   └── Forest.Minimal/     # API minimal (endpoints, handlers, validadores, EF Core)
├── Shared/
│   ├── Forest.Communication/
│   ├── Forest.Host.Default/ # Service defaults (OpenTelemetry, resiliencia, service discovery)
│   └── RustfsClient/       # Cliente S3 para RustFS
├── Test/                   # Tests unitarios e integración (ver Test/README.md)
└── Forest.slnx
```

## Requisitos

- SDK de .NET 11 (preview)
- Docker Desktop (Postgres, Redis y RustFS se levantan como contenedores)
- CLI de Aspire en el `PATH` (`Forest.Host` usa `AspireUseCliBundle`)

## Ejecutar en local

```bash
dotnet run --project Server/Forest.Host/Forest.Host.csproj
```

El dashboard de Aspire queda disponible en la URL que imprime la consola; la API expone
Scalar en la raíz (`/`) en desarrollo y `health/check`.

## Tests

Ver `Test/README.md`. Resumen:

```bash
# Sin Docker (~1 s)
dotnet test Test/Forest.Test.csproj --filter "Category!=Docker"

# Suite completa (requiere Docker)
dotnet test Test/Forest.Test.csproj
```

## Endpoints principales

- `POST /auth/signup`, `POST /auth/signin` — registro e inicio de sesión
- `GET/PUT /users` — perfil del usuario autenticado
- `POST/GET /img/{id}` — subida y obtención de imágenes
- `GET /health/check` — health check

## Notas

- Las credenciales y parámetros de `.env` son de ejemplo para desarrollo local.
- Este repositorio existe con fines de aprendizaje y autoevaluación; no está pensado
  para despliegue en producción.

## Licencia

MIT. Ver [LICENSE](LICENSE).
