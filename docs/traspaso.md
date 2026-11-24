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
- Endpoints disponibles: `POST /api/auth/login` (anónimo); `/api/procedures` (GET para cualquier rol; POST, PUT y `/{id}/activate|deactivate` solo Admin). `/api/patients` (POST para Admin y Reception; `GET /{id}` para cualquier rol). Los enums viajan como texto.
- `dotnet ef` se ejecuta desde `Backend/` con `--project Infrastructure --startup-project WebApiRest`.
- `dotnet test`: 437 pruebas (152 de dominio, 89 de Application, 74 de Infrastructure y 122 de la API en `Tests/WebApiRest.Tests`; las de Infrastructure y API necesitan la base del compose y crean una base temporal por clase de pruebas).

## Estructura (Backend)
- `Domain/`: Common (Entity, DomainException, TimeRange, Guard), Procedures, Patients (EcuadorianId), Scheduling (Dentist, WorkingHours, Chair, ScheduleBlock), Appointments (Appointment, AppointmentStatus, SchedulingContext).
- `Infrastructure/`: DependencyInjection, Persistence/CitaDentDbContext, Configurations/*, Migrations (InitialCreate, AddCoreTables, AddAppointmentOverlapConstraints).
- `Tests/`: Domain.Tests, Application.Tests, Infrastructure.Tests, WebApiRest.Tests (`WebApplicationFactory`; reutiliza `PostgresFixture` enlazado).
- `Directory.Build.props`: net10.0, Nullable, TreatWarningsAsErrors.

## Convenciones del dominio
- Errores de negocio: `DomainException(code, message)` con códigos RN-xx o "VALIDATION". La API los devuelve como `ProblemDetails` con la extensión `code`; el estado HTTP de cada código está en `WebApiRest/Errors/ErrorStatusCodes.cs`.
- `TimeRange` semiabierto [inicio, fin), UTC, `DateTimeOffset`.
- IDs: `Guid.CreateVersion7()`.
- La zona horaria (America/Guayaquil) y la fecha de "hoy" se pasan como parámetro; el dominio no tiene reloj. En Application, `ClinicClock` da `Today` y `Now` en la zona de `Clinic:TimeZone` (`appsettings.json`).
- Solo las citas en estado Scheduled (1) ocupan tiempo.
- Casos de uso: comando + manejador `ICommandHandler<,>` (registro automático con `AddApplication()`); se guarda una vez al final con `IUnitOfWork`, los repositorios no guardan. Ejemplo: `SeedInitialAdminHandler`. Consultas: `IQueryHandler<,>`. Errores de Application: `NOT-FOUND` y `DUPLICATE` (`ApplicationErrorCodes`). Listas paginadas: `PagedResult<T>` y `Paging` (página desde 1, tamaño 1..100); una página más allá del final devuelve ítems vacíos con el total.
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
- `updated_at` del dentista no cambia si solo cambia su horario semanal.
- Faltan índices secundarios en las columnas de propiedades complejas.
- Testcontainers no se usa (base temporal sobre el compose).
- La búsqueda de pacientes distingue tildes ("perez" no encuentra "Pérez") y no tiene índice de texto: ignorar tildes requiere `unaccent` y acelerarla `pg_trgm`, ambas con migración.

## Cómo trabajamos
- Respuestas y explicaciones en español.
- Archivos listos para copiar, con la ubicación en el árbol de carpetas.
- Pegar cualquier error completo para diagnosticarlo.