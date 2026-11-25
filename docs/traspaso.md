# CitaDent: documento de traspaso

> Pega este archivo al inicio de un chat nuevo y di: "Continuemos CitaDent desde la Fase 1b".
> Última actualización: 2026-11-20, fin de la Fase 1a.

## Qué es
Aplicación web para un consultorio odontológico. Proyecto de portafolio (reinicio de MediCita). Autor: Jefferson (Ecuador; stack .NET/C#, Angular, SQL Server, TypeScript; aprendiendo Java).

## Decisiones cerradas
- .NET 10 + Angular + PostgreSQL 17 (EF Core 10 + Npgsql + EFCore.NamingConventions snake_case).
- Arquitectura hexagonal: Domain (sin dependencias) ← Application ← Infrastructure; WebApiRest referencia Application + Infrastructure.
- Código y base de datos en inglés; interfaz en español.
- MVP: un procedimiento por cita. Despliegue local con Docker hasta tener el MVP; luego hosting gratuito.
- El modelo soporta N dentistas y N sillones; el demo siembra 3 dentistas y 2 sillones.
- Metodología: Scrum/Kanban ligero, iteraciones de 1 semana, cortes verticales, pruebas de dominio primero, rama por historia, commits convencionales, CI, bitácora en docs/decisiones.md.

## Entorno local
- `docker compose up -d`: contenedor `citadent-db` (postgres:17), puerto host **5433** (el 5432 lo ocupa un PostgreSQL 18 de Windows), usuario/base/clave `citadent` / `citadent` / `citadent_dev`.
- Cadena de conexión: clave `ConnectionStrings:Default` en `appsettings.Development.json`.
- Clave de firma JWT (obligatoria; la API no arranca sin ella, mínimo 32 bytes). En desarrollo, una sola vez: `dotnet user-secrets set "Jwt:SigningKey" "<clave-larga-aleatoria>" --project WebApiRest` desde `Backend/`. Fuera de desarrollo, variable de entorno `Jwt__SigningKey`. Emisor, audiencia y expiración están en `appsettings.json`.
- Primer administrador (opcional): la API lo crea al arrancar si no existe ningún Admin. En desarrollo, una sola vez desde `Backend/`: `dotnet user-secrets set "Seed:Admin:Email" "admin@citadent.local" --project WebApiRest`, lo mismo con `Seed:Admin:FullName` y `Seed:Admin:Password` (mínimo 12 caracteres). Fuera de desarrollo, variables `Seed__Admin__Email`, `Seed__Admin__FullName` y `Seed__Admin__Password`. Sin las tres claves se omite con un aviso en el log; con una contraseña corta la API no arranca.
- Endpoints disponibles: `POST /api/auth/login` (anónimo); `/api/procedures` (GET para cualquier rol; POST, PUT y `/{id}/activate|deactivate` solo Admin). `/api/patients` (`GET` con `term`, `includeInactive`, `page` y `pageSize`, y `GET /{id}`, para cualquier rol; POST, PUT y `/{id}/activate|deactivate` solo Admin y Reception; no hay DELETE, RN-11). `/api/chairs` (GET con `includeInactive` y `GET /{id}` para cualquier rol; POST, PUT y `/{id}/activate|deactivate` solo Admin; no hay DELETE). `/api/dentists` (GET con `includeInactive` y `GET /{id}`, con el horario, para cualquier rol; `POST` crea el odontólogo y su usuario, `PUT /{id}` edita nombre, apellido, licencia y especialidad (y el nombre completo de su usuario), `PUT /{id}/schedule` reemplaza el horario con un arreglo `[{ day, start, end }]` y `POST /{id}/activate|deactivate` activa o desactiva al odontólogo junto con su usuario; todos solo Admin; no hay DELETE). `/api/schedule-blocks` (`GET` con `from` y `to` obligatorios, días de la clínica en `yyyy-MM-dd`, ambos incluidos y máximo 366 días, más `dentistId` e `includeClinicWide` (por defecto true), y `GET /{id}`, para cualquier rol; `POST` con `{ dentistId, startsAt, endsAt, reason }`, donde `dentistId` nulo bloquea toda la clínica, y `DELETE /{id}`, para Admin y Recepción; responde los instantes en UTC). `/api/users`, todo solo Admin: `GET` (`includeInactive` y `role` por nombre), `GET /{id}`, `POST` (cuentas de Recepción y Admin), `PUT /{id}/email`, `PUT /{id}/full-name` (no para cuentas de odontólogos), `PUT /{id}/password` (204), `PUT /{id}/role` (solo Admin ↔ Recepción) y `POST /{id}/activate|deactivate` (no para cuentas de odontólogos); nunca devuelve el hash, no hay DELETE y quitar o desactivar al último Admin activo da 409 `LAST-ADMIN`. Los enums viajan como texto, y los instantes (`DateTimeOffset`) deben llevar desfase (`-05:00` o `Z`): sin él la API responde 400.
- `dotnet ef` se ejecuta desde `Backend/` con `--project Infrastructure --startup-project WebApiRest`.
- `dotnet test`: 931 pruebas (188 de dominio, 241 de Application, 122 de Infrastructure y 380 de la API en `Tests/WebApiRest.Tests`; las de Infrastructure y API necesitan la base del compose y crean una base temporal por clase de pruebas).

## Estructura (Backend)
- `Domain/`: Common (Entity, DomainException, TimeRange, Guard), Procedures, Patients (EcuadorianId), Scheduling (Dentist, WorkingHours, Chair, ScheduleBlock), Appointments (Appointment, AppointmentStatus, SchedulingContext).
- `Infrastructure/`: DependencyInjection, Persistence/CitaDentDbContext, Configurations/*, Migrations (InitialCreate, AddCoreTables, AddAppointmentOverlapConstraints, AddUsers, AddDentistLicenseUniqueIndex).
- `Tests/`: Domain.Tests, Application.Tests, Infrastructure.Tests, WebApiRest.Tests (`WebApplicationFactory`; reutiliza `PostgresFixture` enlazado).
- `Directory.Build.props`: net10.0, Nullable, TreatWarningsAsErrors.

## Convenciones del dominio
- Errores de negocio: `DomainException(code, message)` con códigos RN-xx o "VALIDATION". La API los devuelve como `ProblemDetails` con la extensión `code`; el estado HTTP de cada código está en `WebApiRest/Errors/ErrorStatusCodes.cs`.
- `TimeRange` semiabierto [inicio, fin), UTC, `DateTimeOffset`.
- IDs: `Guid.CreateVersion7()`.
- La zona horaria (America/Guayaquil) y la fecha de "hoy" se pasan como parámetro; el dominio no tiene reloj. En Application, `ClinicClock` da `Today` y `Now` en la zona de `Clinic:TimeZone` (`appsettings.json`).
- Solo las citas en estado Scheduled (1) ocupan tiempo.
- Casos de uso: comando + manejador `ICommandHandler<,>` (registro automático con `AddApplication()`); se guarda una vez al final con `IUnitOfWork`, los repositorios no guardan. Ejemplo: `SeedInitialAdminHandler`. Consultas: `IQueryHandler<,>`. Errores de Application: `NOT-FOUND`, `DUPLICATE` y `LAST-ADMIN` (`ApplicationErrorCodes`; `LAST-ADMIN` responde 409). Para agendar, `SchedulingContextLoader` arma el `SchedulingContext` con los bloqueos (del odontólogo y de la clínica) y las citas `Scheduled` (del odontólogo o del sillón) que se solapan con el horario pedido; no es un manejador, se registra a mano en `AddApplication()`. Listas paginadas: `PagedResult<T>` y `Paging` (página desde 1, tamaño 1..100); una página más allá del final devuelve ítems vacíos con el total.
- Auditoría: propiedades sombra CreatedAt/UpdatedAt rellenadas en SaveChanges con TimeProvider.

## Reglas de negocio (RN-01..RN-19)
Agenda RN-01..09, pacientes RN-10..12, clínico RN-13..15, catálogo RN-16, tratamientos RN-17, pagos RN-18..19.
Pertenecen a Application: RN-06 (no agendar en el pasado), RN-09 (quién completa la cita), RN-11. El detalle completo está en el documento "CitaDent — Plan del proyecto" (Claude Docs).

## Base de datos (estado actual)
Tablas: procedures, patients, dentists, dentist_schedules, chairs, schedule_blocks, appointments.
Restricciones EXCLUDE (btree_gist) `ex_appointments_dentist_no_overlap` y `ex_appointments_chair_no_overlap` sobre `tstzrange(starts_at, ends_at, '[)') WHERE status = 1`.

## Hoja de ruta
- Fase 0 (cimientos): hecha.
- **Fase 1a (dominio + BD): hecha.**
- Fase 1b (API + JWT con roles Reception/Dentist/Admin): siguiente.
- Fase 1c (pantallas Angular), 1d (clínico/odontograma).
- Fase 2: planes de tratamiento, pagos, adjuntos, recordatorios.

## Siguiente paso: Fase 1b
1. Entidad `User` + migración; FKs pendientes: `dentists.user_id`, `appointments.created_by_user_id`.
2. Autenticación JWT con roles.
3. Casos de uso en Application (RN-06, RN-09, RN-11, unicidad), cargando el `SchedulingContext` solo con registros cercanos.
4. Endpoints: pacientes, catálogo, dentistas, sillones, agendar/cancelar citas.
5. Pruebas de Application y de la API.

## Límites conocidos
- La regla de "al menos un Admin activo" se revisa en Application sin bloqueo: si dos Admins se desactivan o degradan uno al otro al mismo tiempo, podría no quedar ninguno. La siembra del Admin inicial no lo arregla, porque solo actúa si no existe ningún Admin (ni siquiera inactivo); habría que reactivarlo directamente en la base.
- Desactivar un usuario (por ejemplo, un odontólogo) le impide iniciar sesión, pero un token ya emitido sigue sirviendo hasta que expira (`Jwt:ExpirationMinutes`, 60 minutos). Lo mismo pasa con los datos del token (`email`, `name` y `role`): tras cambiar el correo, el nombre o el rol, un token anterior sigue mostrando los datos viejos, y como la autorización lee el rol del token, un Admin degradado conserva el acceso de Admin hasta que expira. Cortarlo antes requiere revisar el usuario en cada solicitud o revocar tokens.
- `updated_at` del dentista no cambia si solo cambia su horario semanal.
- Crear un bloqueo de agenda no revisa ni cancela las citas `Scheduled` que ya caen en ese rango: solo impide agendar citas nuevas.
- Faltan índices secundarios en las columnas de propiedades complejas: `schedule_blocks.starts_at`/`ends_at` (listado de bloqueos y carga del contexto) no tiene índice, y la consulta de citas solapadas compara `starts_at`/`ends_at` con `<`, así que probablemente no usa los índices GiST de las restricciones `EXCLUDE`. Con el volumen de un consultorio no se nota; medirlo con los datos de demostración (1b-18) antes de crear una migración.
- Testcontainers no se usa (base temporal sobre el compose).
- La búsqueda de pacientes distingue tildes ("perez" no encuentra "Pérez") y no tiene índice de texto: ignorar tildes requiere `unaccent` y acelerarla `pg_trgm`, ambas con migración.

## Cómo trabajamos
- Respuestas y explicaciones en español.
- Archivos listos para copiar, con la ubicación en el árbol de carpetas.
- Pegar cualquier error completo para diagnosticarlo.