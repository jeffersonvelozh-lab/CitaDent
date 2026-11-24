# Decisiones de arquitectura

Registro corto de las decisiones del proyecto, con fecha y motivo. Se agrega una entrada nueva cuando se toma otra decisión importante.

## 2026-11-18

### Arquitectura hexagonal
Domain, Application, Infrastructure y WebApiRest, con dependencias en un solo sentido: Domain no referencia a nadie, Application referencia a Domain, Infrastructure a Application, y la API a Application e Infrastructure.
**Motivo:** las reglas de agenda se prueban en el dominio sin base de datos ni HTTP.

### .NET 10, Angular y PostgreSQL
**Motivo:** .NET 10 es la versión con soporte de largo plazo; Angular es el frontend ya conocido para el proyecto.

### PostgreSQL en lugar de SQL Server
**Motivo:** PostgreSQL permite restricciones de exclusión (`EXCLUDE USING gist` con `btree_gist`) que impiden en la propia base que un odontólogo o un sillón tengan dos citas solapadas, incluso con dos usuarios agendando a la vez. Además, tiene hosting gratuito más accesible para la demo.

### Código en inglés, interfaz en español
Nombres de clases, tablas y columnas en inglés. Los errores de negocio llevan un código de regla (por ejemplo `RN-02`) y la interfaz los traduce al español.

### Claves primarias GUID generadas en el dominio
**Motivo:** el dominio puede crear entidades sin consultar a la base, y los identificadores no revelan cuántos registros hay.

### Fechas en UTC con `DateTimeOffset` y `timestamptz`
`TimeRange` normaliza todo a UTC. La interfaz muestra la hora de Ecuador (UTC-5).
**Motivo:** Npgsql solo escribe valores UTC en columnas `timestamptz`; normalizar en el dominio evita errores difíciles de rastrear.

### Nombres en `snake_case` en la base
Se usa el paquete `EFCore.NamingConventions`. En el código siguen en PascalCase.

### Una cita tiene un solo procedimiento en el MVP
**Motivo:** simplifica el cálculo de duración y los cruces. Varios procedimientos por cita se evalúan en la fase 2.

### Despliegue
Local con Docker Compose hasta tener un producto mínimo viable; después, un hosting gratuito.

### Puerto 5433 para PostgreSQL en Docker
**Motivo:** evitar el choque con instalaciones locales de PostgreSQL que usan el 5432.

## 2026-11-23

### Hash de contraseñas con PBKDF2 propio, sin ASP.NET Core Identity
El puerto `IPasswordHasher` vive en Application y la implementación `Pbkdf2PasswordHasher` en Infrastructure. Usa PBKDF2 con HMAC-SHA256, 600 000 iteraciones (recomendación de OWASP), un salt aleatorio de 16 bytes por hash y una clave de 32 bytes. Se guarda en un solo texto: `PBKDF2-SHA256$<iteraciones>$<salt base64>$<hash base64>`, en la columna `users.password_hash` de tipo `text`.
**Motivo:** solo usa la biblioteca base de .NET, sin agregar `Microsoft.Extensions.Identity.Core`. Como cada hash guarda su algoritmo y sus iteraciones, se pueden subir las iteraciones o cambiar de algoritmo más adelante sin invalidar las contraseñas ya guardadas. La comparación se hace en tiempo constante.

## 2026-11-24

### Autorización: autenticación exigida por defecto y políticas por grupo de roles
La API valida el JWT con `JwtBearer` usando los mismos `JwtOptions` que el emisor (emisor, audiencia, clave, solo HS256). Una política de respaldo (*fallback*) exige usuario autenticado en todo endpoint; solo `/api/auth/login`, `/health` y el documento OpenAPI llevan `AllowAnonymous`. Políticas: `AdminOnly`, `StaffWithAgendaAccess` (Admin y Reception) y `ClinicalAccess` (Admin y Dentist). Tolerancia de reloj (`ClockSkew`) de 30 segundos en vez de los 5 minutos por defecto. Los claims se leen con sus nombres cortos (`sub`, `role`), sin el mapeo de ASP.NET.
**Motivo:** un endpoint nuevo nace protegido aunque se olvide el atributo. Las políticas nombran necesidades del negocio (agenda, trabajo clínico) en vez de repetir listas de roles en cada controlador. Con 30 segundos, un token vencido deja de servir casi de inmediato.

### Errores de la API: `ProblemDetails` con código y tabla única de estados HTTP
Toda respuesta de error es `application/problem+json` con `status`, `title`, `detail` (en inglés), `traceId` y la extensión `code`, que el frontend traduce a español. `DomainException` se convierte en `DomainExceptionHandler` (un `IExceptionHandler`); cualquier otra excepción da un 500 genérico con `code = "INTERNAL"`, sin el mensaje ni la traza (que van solo al log), igual en todos los entornos. El 400 automático de `[ApiController]` (JSON mal formado, tipos incorrectos) también lleva `code = "VALIDATION"`. La tabla código → estado vive solo en `WebApiRest/Errors/ErrorStatusCodes.cs`:

| Estado | Códigos |
|---|---|
| 400 | `VALIDATION` |
| 401 | `AUTH-INVALID-CREDENTIALS` |
| 403 | `RN-09`, `RN-15`, `RN-19` (reglas de permiso) |
| 409 | `RN-02`, `RN-03`, `RN-05`, `RN-07`, `RN-11` (choque con lo que ya existe) |
| 422 | `RN-01`, `RN-06`, `RN-08`, `RN-10`, `RN-12`, `RN-13`, `RN-14`, `RN-16`, `RN-17`, `RN-18` y cualquier código no listado |

`RN-10` queda en 422 (documento inválido); el documento repetido de la tarea 1b-09 usará un código propio con 409. Los 401 y 403 de la autorización siguen sin cuerpo.
**Motivo:** un solo formato de error para el frontend y un solo lugar para decidir el estado HTTP; una regla nueva nunca termina en 500, y un error inesperado no filtra datos internos.
