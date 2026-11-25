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
- Editar los datos del odontólogo y activarlo o desactivarlo (con su usuario) queda para la tarea 1b-12c; la gestión de cuentas de usuario (correo, contraseña, activar/desactivar, usuarios de Recepción y Admin) para la 1b-12d. Ninguna regla RN las cubre.
**Motivo:** el plan no tiene gestión de usuarios y un odontólogo sin usuario no puede iniciar sesión; crear ambos a la vez evita estados a medias. Dos odontólogos con la misma licencia son, en la práctica, la misma persona registrada dos veces.

### Edición de odontólogos (1b-12c) y nueva división de tareas
- La tarea 1b-12c se dividió en dos: **1b-12c** (editar datos) y **1b-12d** (activar/desactivar con su usuario). La gestión de cuentas de usuario pasa a **1b-12e**. Esto reemplaza la numeración de la entrada anterior.
- `PUT /api/dentists/{id}` reemplaza los datos completos: nombre, apellido, licencia y especialidad. Si la especialidad falta o viene vacía, se borra. El correo, la contraseña, el estado y el horario no se editan aquí.
- Al editar, el nombre completo del `User` se recalcula como "nombre apellido" (recortado a 160 caracteres, igual que en el alta) y se guarda en la misma operación. Para eso se agregaron `User.ChangeFullName` en el dominio y `IUserRepository.GetByIdAsync` (con seguimiento) en Application.
- `IDentistRepository.LicenseNumberExistsAsync` recibe `excludingId`, como `IChairRepository.NameExistsAsync`: un odontólogo puede conservar su propia licencia escrita con otras mayúsculas.
- Si el odontólogo existe pero su `User` no, se lanza `InvalidOperationException` (500) y no `NOT-FOUND`: la llave foránea lo impide, así que sería un dato roto y no un error de quien llama.
**Motivo:** el nombre del usuario debe coincidir con la ficha del odontólogo, y separar editar de activar mantiene las tareas pequeñas.

### Activar y desactivar odontólogos (1b-12d)
- `POST /api/dentists/{id}/activate|deactivate` cambia en la misma operación el `Dentist` y su `User`. Un odontólogo inactivo no inicia sesión (el login ya rechazaba usuarios inactivos con el mismo 401 de credenciales inválidas) y sale de `GET /api/dentists` salvo con `includeInactive=true`. Pedir el estado que ya tiene responde 204 y no es error. Los odontólogos no se borran, porque las citas guardan su id.
- Desactivar no cancela ni revisa sus citas `Scheduled`. Impedir agendar con un odontólogo inactivo le toca a la tarea 1b-15 (agendar cita).
- Un token ya emitido sigue sirviendo hasta que expira (60 minutos); queda como límite conocido en `docs/traspaso.md`.
**Motivo:** el usuario y la ficha del odontólogo tienen que estar en el mismo estado. Cancelar citas automáticamente es una decisión de negocio que el plan no pide, y revocar tokens no compensa todavía con expiraciones cortas.

### Gestión de cuentas de usuario (1b-12e)
- La tarea de usuarios se dividió en **1b-12e** (dominio, casos de uso y repositorio) y **1b-12f** (endpoints `/api/users`). Editar el nombre de usuarios de Recepción y Admin queda en **1b-12g**.
- Un Admin crea cuentas de Recepción y Admin (los odontólogos se crean con su ficha), cambia el correo (único, `DUPLICATE`), restablece la contraseña sin pedir la anterior (mínimo 12 caracteres, igual que el Admin inicial), cambia el rol y activa o desactiva. `UserDto` nunca incluye el hash.
- El rol solo cambia entre Admin y Recepción: el dominio (`User.ChangeRole`) rechaza dar o quitar el rol Dentist con `VALIDATION`, porque ese rol va ligado a una ficha de odontólogo.
- Las cuentas de odontólogos aparecen al listar y consultar, y se les puede cambiar el correo o la contraseña; activarlas o desactivarlas por esta vía da `VALIDATION`, porque se hace con `/api/dentists` junto con la ficha.
- Desactivar al último Admin activo, o quitarle el rol, da el código nuevo `LAST-ADMIN` (en `ApplicationErrorCodes`; ninguna regla RN lo cubre). Un Admin inactivo no cuenta; desactivar o degradar a un Admin que ya está inactivo se permite, porque no reduce los Admins activos. Un Admin puede desactivarse a sí mismo si queda otro activo.
- La unicidad del correo usa `IUserRepository.EmailExistsAsync(email, excludingId)`, igual que la licencia y los nombres de sillón; el correo ya se guarda en minúsculas, así que la comparación es exacta.
**Motivo:** la clínica nunca debe quedar sin quien administre las cuentas, y el rol Dentist sin ficha (o una ficha sin ese rol) dejaría datos inconsistentes. La revisión del último Admin no usa bloqueo; el límite está en `docs/traspaso.md`.

### Endpoints de usuarios (1b-12f)
- Todo `/api/users` exige el rol Admin (política a nivel de controlador). `LAST-ADMIN` responde **409**, como los demás choques con el estado actual.
- El filtro `role` de la URL se lee como texto y solo acepta un nombre definido (`Admin`, `Reception`, `Dentist`, sin distinguir mayúsculas); un número (`?role=1`) o un nombre desconocido da 400 `VALIDATION`. El enlace de ASP.NET aceptaría números, incluso no definidos, y eso rompería la regla de enums como texto.
- `PUT /{id}/password` responde 204 sin cuerpo; correo y rol responden 200 con el usuario. Un Admin puede restablecer también su propia contraseña por esta vía sin dar la actual; un cambio de contraseña de autoservicio (con la actual, para cualquier rol) queda como idea para más adelante, fuera del plan.
**Motivo:** el contrato sigue las convenciones ya usadas en odontólogos y sillones, y la contraseña no tiene nada que devolver.

### Editar el nombre de usuarios (1b-12g)
- `PUT /api/users/{id}/full-name` cambia el nombre de cuentas de Recepción y Admin, en la línea de `/email`, `/password` y `/role` (un sub-recurso por dato). Las cuentas de odontólogos dan `VALIDATION`: su nombre se recalcula desde la ficha con `PUT /api/dentists/{id}`.
- El token lleva `email`, `name` y `role`; tras un cambio, un token anterior conserva los datos viejos hasta que expira. Se amplió la nota de tokens en `docs/traspaso.md`, que ahora advierte que un Admin degradado conserva el acceso de Admin hasta que su token expira.
**Motivo:** las cuentas de odontólogos se mantienen en un solo lugar, y el límite de los tokens es el mismo ya aceptado al desactivar usuarios.

### Bloqueos de agenda (1b-13a)
- La tarea se dividió en **1b-13a** (crear, consultar y quitar) y **1b-13b** (listar por odontólogo y rango), porque el listado comparte consultas con la carga del `SchedulingContext` (1b-14).
- `POST /api/schedule-blocks` y `DELETE /api/schedule-blocks/{id}` son para Admin y Recepción (`StaffWithAgendaAccess`), porque recepción maneja la agenda; `GET /{id}` es para cualquier rol. El bloqueo se borra de verdad, porque no es historial de ningún paciente. No se edita: se quita y se crea otro.
- Un bloqueo que ya terminó (su fin es igual o anterior a `ClinicClock.Now`) da `VALIDATION`; uno en curso se permite. El rango invertido o vacío lo revisa el caso de uso como `VALIDATION`, y no como el RN-01 que daría `TimeRange`, porque aquí es un dato mal formado y no una regla de citas. Un odontólogo inexistente en el cuerpo es `VALIDATION`, no `NOT-FOUND`, porque el 404 queda para el recurso de la URL.
- Se permiten bloqueos solapados entre sí y para odontólogos inactivos. Las citas `Scheduled` que ya caen en el rango se conservan (límite anotado en `docs/traspaso.md`).
- Nuevo convertidor JSON global `OffsetRequiredDateTimeOffsetConverter`: un `DateTimeOffset` sin desfase (`"2026-12-24T08:00:00"`) da 400 `VALIDATION`. Sin él, .NET usaría la zona horaria del servidor, que cambia entre la máquina de desarrollo y el hosting. Las respuestas siguen en ISO 8601 con desfase.
**Motivo:** que un bloqueo caiga en la hora correcta no puede depender de dónde corre la API, y la regla RN-05 ya vive en el dominio, así que aquí solo hace falta guardarlos y quitarlos.

### Listar bloqueos de agenda (1b-13b)
- `GET /api/schedule-blocks?from=&to=` recibe **días de la clínica** (`yyyy-MM-dd`, ambos incluidos), no instantes: el caso de uso los convierte a `[from 00:00, to + 1 día 00:00)` con la zona de `ClinicClock`. La agenda se consulta por días, y en la URL no actúa el convertidor JSON que exige desfase.
- El día se lee como texto y solo se acepta `yyyy-MM-dd`: el enlace de ASP.NET leería `01/03/2031` como 3 de enero (orden de EE. UU.), cuando en Ecuador significa 1 de marzo. Igual que `role` en `/api/users`.
- `from` y `to` son obligatorios y el rango tiene como máximo 366 días, así que la lista no se pagina. Entran los bloqueos que se solapan con el rango (uno que termina justo al empezar no entra).
- Con `dentistId` vienen sus bloqueos y, salvo `includeClinicWide=false`, los de toda la clínica; sin `dentistId`, los de todos los odontólogos. Un odontólogo inexistente da una lista sin bloqueos propios (200), porque es un filtro. Orden: inicio y luego id.
**Motivo:** la agenda de un odontólogo también debe mostrar los feriados, y un rango acotado mantiene la consulta barata sin índice nuevo; el índice se evalúa en 1b-14.

### Cargar el contexto de agenda (1b-14)
- `SchedulingContextLoader.LoadAsync(dentistId, chairId, slot)` carga **exactamente lo que se solapa con el horario pedido**: los bloqueos del odontólogo y de toda la clínica (con `IScheduleBlockRepository.ListAsync`), y las citas `Scheduled` del odontólogo o del sillón (con el nuevo `IAppointmentRepository.ListScheduledOverlappingAsync`), sin seguimiento. El dominio solo revisa solapamientos y las citas seguidas no chocan (RN-04), así que un margen alrededor no cambiaría ninguna decisión.
- Es una clase de Application, no un manejador, y se registra a mano en `AddApplication()`. Hace las dos consultas una tras otra porque comparten el `DbContext`. No revisa que el odontólogo o el sillón existan: lo hará el caso de uso de agendar (1b-15), que los carga.
- No se agregan índices todavía (límite anotado en `docs/traspaso.md`).
**Motivo:** cargar solo lo necesario mantiene la consulta pequeña aunque crezcan las tablas, y separar la carga deja el caso de uso de agendar corto y probado por partes. Si dos solicitudes chocan a la vez, la última palabra la tienen las restricciones `EXCLUDE` (1b-15).

### Agendar cita: caso de uso (1b-15a)
- La tarea se dividió en **1b-15a** (caso de uso, traducción del `23P01` y prueba de concurrencia) y **1b-15b** (endpoints).
- `ScheduleAppointmentHandler` valida en este orden: referencias que existan (`VALIDATION`), RN-06 (un inicio anterior a `ClinicClock.Now`; empezar justo ahora se permite y no hay anticipación mínima) y luego el dominio con el contexto de 1b-14. RN-06 se revisa antes de cargar el contexto.
- **El `23P01` se traduce en dos pasos, para que Application no conozca EF ni Npgsql:** `CitaDentDbContext.SaveChangesAsync` lo convierte en `ScheduleConflictException` (definida en Application) según el nombre de la restricción `EXCLUDE`, y el manejador la convierte en `DomainException` RN-02 o RN-03. Otros errores de base pasan sin cambios.
- **Deadlock entre inserciones simultáneas:** la prueba de concurrencia mostró que dos inserciones que chocan pueden recibir `40P01` (cada revisión de exclusión espera a la otra) en vez de `23P01`. Se activó `EnableRetryOnFailure` de Npgsql (3 reintentos, hasta 5 s; sin paquetes nuevos) en `CitaDentDatabaseOptions`, compartida por la API y las pruebas. Al reintentar, la otra cita ya está confirmada y la base responde `23P01`. Es seguro porque no hay transacciones explícitas.
- La prueba de concurrencia usa dos `DbContext` y una barrera que obliga a ambos a pasar la revisión del dominio antes de guardar; así solo la base puede detener a uno.
**Motivo:** el dominio revisa con lo que ve, pero solo la base garantiza la regla cuando dos personas agendan a la vez, y el usuario debe recibir el mismo RN-02 o RN-03 en los dos casos, nunca un 500.
