# Forest.Test

Suite de tests del proyecto Forest (API minimal `Forest.Minimal` + AppHost Aspire `Forest.Host`).

## Estructura

| Ruta | Qué cubre | Necesita Docker |
| --- | --- | --- |
| `Unit/Account` | `SignUpHandler`, `SignInHandler`, `UpdateUserHandler`, `BearerGenerator` y los tres validators | No |
| `Unit/Storage` | `ImageHandler`, `ImageCleanerService` (barrido de huérfanas) y `StorageHelper` | No |
| `Unit/Common` | `Problem` (record de respuesta de error), `ProblemErrors` (traducción de excepciones) y `ErrorMiddleware` | No |
| `Aspire/AppHostResourceTests` | Modelo de recursos de `Forest.Host` (puertos, volumenes, parametros, `WaitFor`) | No |
| `Aspire/ForestServerTests` | End-to-end contra el servidor real: sign-up, sign-in, `PUT /users`, `GET /img/{id}` | **Si** |
| `Support` | Dobles de test: `FakeRustfsObjectStorageClient`, `ForestContextScope` (SQLite en memoria), `StaticOptionsMonitor`, `TestData` | No |

## Ejecutar

Todo lo que no necesita Docker (rápido, ~1 s):

```bash
dotnet test Test/Forest.Test.csproj --filter "Category!=Docker"
```

Suite completa, requiere Docker Desktop arrancado. Levanta Postgres, Redis y RustFS reales
y aplica las migraciones, por lo que la primera ejecución tarda de 1 a 3 minutos:

```bash
dotnet test Test/Forest.Test.csproj
```

## Notas

- Los tests unitarios usan **SQLite en memoria** en lugar del proveedor `InMemory` porque
  `UpdateUserHandler` usa `ExecuteUpdateAsync` y entidades `Owned` mapeadas a columna JSON,
  que `InMemory` no soporta.
- Los tests de integración fijan los parámetros secretos (`BearerKey`, `AccessKey`, `SecretKey`)
  desde `ForestAppFixture` para que Aspire no los pida por consola.
- `ForestAppFixture` comprueba al arrancar que los puertos 5610-5621 están libres. El `AppHost`
  los fija con `WithEndpoint` y no los reasigna si están ocupados, así que sin esa comprobación
  el fallo aparecería más tarde como un error de DCP sin contexto.

## Requisitos para los tests de integración

`Aspire/ForestServerTests` levanta la aplicación completa, así que necesita en local:

| Requisito | Por qué |
| --- | --- |
| Docker arrancado | Postgres, Redis y RustFS se levantan como contenedores |
| CLI de Aspire en el PATH | `Forest.Host.csproj` usa `AspireUseCliBundle`, así que DCP se busca en `~/.aspire/bundle/dcp`, que descarga el CLI |
| Puertos 5610-5621 libres | Los fija `AppHost.cs`. Cierra el dashboard de Aspire antes de testear |
| Volumen `forest` sin esquema obsoleto | Postgres lo reutiliza entre ejecuciones; si urge, `docker volume rm forest` |