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
| 404 | `NOT-FOUND` (agregado en 1b-08b) |
| 403 | `RN-09`, `RN-15`, `RN-19` (reglas de permiso) |
| 409 | `RN-02`, `RN-03`, `RN-05`, `RN-07`, `RN-11` (choque con lo que ya existe) y `DUPLICATE` (agregado en 1b-08b) |
| 422 | `RN-01`, `RN-06`, `RN-08`, `RN-10`, `RN-12`, `RN-13`, `RN-14`, `RN-16`, `RN-17`, `RN-18` y cualquier código no listado |

`RN-10` queda en 422 (documento inválido); el documento repetido de la tarea 1b-09 usará un código propio con 409. Los 401 y 403 de la autorización siguen sin cuerpo.
**Motivo:** un solo formato de error para el frontend y un solo lugar para decidir el estado HTTP; una regla nueva nunca termina en 500, y un error inesperado no filtra datos internos.

### Convención de casos de uso y unidad de trabajo
Cada caso de uso vive en `Application/<Funcionalidad>/`: un comando (`record` inmutable) y un manejador `sealed` que implementa `ICommandHandler<TCommand, TResult>` con un solo método `HandleAsync`. Dentro, en este orden: (1) validar la entrada, lanzando `DomainException(DomainException.ValidationCode, ...)`, que da 400; (2) reglas que necesitan la base (unicidad, RN-06...); (3) llamar al dominio; (4) guardar una sola vez con `IUnitOfWork.SaveChangesAsync`. Los repositorios agregan y consultan, nunca guardan. `AddApplication()` registra por reflexión toda clase que implemente `ICommandHandler<,>`, como sí misma y como la interfaz, con ciclo de vida *scoped*; una prueba comprueba que todos se resuelven desde la API. `CitaDentDbContext` implementa `IUnitOfWork` y se registra como la misma instancia de la petición. El ejemplo de referencia es `SeedInitialAdminHandler`. Las consultas tendrán su propia interfaz cuando llegue la primera (1b-10). La validación vive en el manejador, sin FluentValidation ni clases validadoras aparte.
**Motivo:** un caso de uso nuevo no necesita configuración extra y no puede quedar sin registrar; guardar en un solo lugar hace que cada caso de uso sea una sola transacción y deja un punto único para traducir errores de la base (el `23P01` de la tarea 1b-15). Application agrega el paquete `Microsoft.Extensions.DependencyInjection.Abstractions` (solo interfaces) para que su registro viva en su propia capa.

### Consultas, códigos genéricos y unicidad sin distinguir mayúsculas
- `IQueryHandler<TQuery, TResult>` llega en 1b-08a (no en 1b-10) porque listar procedimientos ya es una consulta. `AddApplication()` registra comandos y consultas por igual. Una consulta no cambia estado ni guarda.
- Códigos de Application que no pertenecen a una regla: `NOT-FOUND` (registro inexistente, 404) y `DUPLICATE` (valor único repetido, 409), en `ApplicationErrorCodes`. Se reutilizan en procedimientos, sillones, pacientes y odontólogos; el documento repetido de pacientes (antes pendiente en RN-10) usará `DUPLICATE`.
- La unicidad de nombres (procedimientos, y luego sillones) se valida en Application **sin distinguir mayúsculas** (`lower(name)` en SQL). El índice único de la base distingue mayúsculas y queda como respaldo; si dos solicitudes simultáneas crean el mismo nombre, la base las detiene con `23505` y por ahora sale 500. La traducción de `23505` a `DUPLICATE` se hará en 1b-15 junto con la de `23P01`.
- Los procedimientos no se borran: se desactivan, porque las citas guardan su id.
**Motivo:** un solo par de códigos para el frontend en lugar de uno por entidad; "Limpieza" y "limpieza" son el mismo procedimiento para el personal de la clínica.

### Fecha de la clínica (`ClinicClock`) y unicidad del documento del paciente
- `ClinicClock` (Application) da `Now` y `Today` en la zona de la clínica a partir de `TimeProvider`. La zona sale de `Clinic:TimeZone` (por defecto `America/Guayaquil`) y se resuelve al arrancar: una zona desconocida detiene la API. Es una clase, no un puerto, porque solo depende de tipos de .NET. Los casos de uso le piden "hoy" y lo pasan al dominio; nunca se usa la fecha UTC como "hoy" (cerca de la medianoche en Ecuador, UTC ya es el día siguiente).
- El documento del paciente es único por **tipo y número** (igual que el índice), comparado después de que el dominio lo normaliza; un duplicado da `DUPLICATE` (409). El mensaje de error no repite el número, porque es un dato personal y los mensajes llegan a los logs.
- El plan decía "unicidad del documento (RN-11)"; la unicidad es RN-10. RN-11 (no borrar, desactivar) pasa a la tarea 1b-10.

### Enums como texto en la API
La API lee y escribe los enums por su nombre (`"documentType": "Cedula"`), con `JsonStringEnumConverter(allowIntegerValues: false)` en toda la API: un número o un nombre desconocido es un 400 `VALIDATION`. Los campos obligatorios de tipo valor (por ejemplo `documentType` y `birthDate` del paciente) son anulables en el contrato y llevan `[Required]` en el parámetro del `record`, para que un campo ausente sea un 400 y no llegue como su valor por defecto (`0001-01-01`).
**Motivo:** el contrato se lee solo, el frontend no depende de los números internos de los enums y un dato faltante nunca se guarda con un valor inventado.

### Alta de odontólogos con su usuario y licencia única
- El alta de un odontólogo (`RegisterDentistHandler`) crea en la misma operación el `User` con rol Dentist (email, contraseña con hash, nombre completo = nombre + apellido) y el `Dentist` ligado a él, y guarda una sola vez: nunca queda un odontólogo sin usuario ni un usuario odontólogo sin ficha. La contraseña exige el mismo mínimo que la del Admin inicial (12 caracteres). Un email ya usado da `DUPLICATE` sin repetir el email en el mensaje.
- La licencia profesional es única **sin distinguir mayúsculas**, validada en Application (`lower()` en SQL) con `DUPLICATE`; la migración `AddDentistLicenseUniqueIndex` agrega el índice único `ix_dentists_license_number` como respaldo, con la misma limitación de `23505` que los nombres.
- El horario semanal se reemplaza completo: EF borra las filas anteriores de `dentist_schedules` e inserta las nuevas. Una lista vacía deja al odontólogo sin horario. Los cruces dan `VALIDATION` (400), porque es un dato mal formado y no una regla RN.
- Editar los datos del odontólogo y activarlo o desactivarlo (con su usuario) queda para una tarea aparte.
**Motivo:** el plan no tiene gestión de usuarios y un odontólogo sin usuario no puede iniciar sesión; crear ambos a la vez evita estados a medias. Dos odontólogos con la misma licencia son, en la práctica, la misma persona registrada dos veces.
