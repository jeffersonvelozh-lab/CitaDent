# CitaDent: instrucciones para Claude Code

Aplicación web para un consultorio odontológico. Proyecto de portafolio de Jefferson (estudiante y practicante en Ecuador). Estas instrucciones se leen al inicio de cada sesión.

## Idioma
- Habla con Jefferson **siempre en español**.
- Código, nombres de clases, tablas, columnas, commits y ramas: **inglés**.
- Textos de la interfaz (Angular): **español**.

## Cómo trabajamos (lo más importante)
Jefferson supervisa cada paso. Por eso:

1. **Una tarea por sesión.** Las tareas están numeradas en `docs/plan-de-trabajo.md` (por ejemplo `1b-03`). Haz solo la que se te pida. No adelantes tareas siguientes ni "aproveches" para refactorizar.
2. **Primero el plan, después el código.** Antes de escribir, responde con: qué archivos vas a crear o cambiar, qué pruebas agregarás y qué dudas tienes. Espera la aprobación de Jefferson.
3. **Pruebas primero en el dominio.** Para reglas de negocio, escribe las pruebas antes o junto con el código. Cada regla RN-xx tiene al menos una prueba que falla sin ella.
4. **Cambios pequeños.** Una tarea debería tocar pocos archivos. Si crece, detente y propón dividirla.
5. **Rama por tarea:** `feat/1b-03-password-hasher`. Nunca trabajes en `main`.
6. **Commits convencionales** (`feat:`, `fix:`, `test:`, `docs:`, `chore:`), en inglés, uno por unidad lógica. Haz commit solo en la rama local cuando Jefferson lo apruebe. **No hagas push, no abras PR ni fusiones** salvo que lo pida.
7. **Al terminar la tarea, detente y entrega este resumen:**
   - Qué hiciste (2 a 4 líneas).
   - Archivos creados y modificados.
   - Cómo verificar: comandos exactos (`dotnet test`, `dotnet run`, etc.) y resultado esperado.
   - Decisiones que tomaste y límites o deudas que dejaste.
   - Si cambió una decisión de arquitectura, agrégala a `docs/decisiones.md`.
8. Antes de dar una tarea por terminada, ejecuta `dotnet build` y `dotnet test` desde `Backend/` y confirma que todo está en verde.
9. Si algo es ambiguo o contradice una regla, **pregunta**; no adivines.
10. No agregues paquetes NuGet ni npm sin explicar para qué y esperar aprobación.

## Arquitectura (hexagonal)
```
Domain  ←  Application  ←  Infrastructure
                 ↑               ↑
                 └── WebApiRest ─┘
```
- **Domain**: reglas de negocio, sin dependencias de nada (ni EF, ni ASP.NET).
- **Application**: casos de uso y puertos (interfaces). Referencia solo a Domain.
- **Infrastructure**: EF Core, PostgreSQL, hashing, JWT y demás adaptadores.
- **WebApiRest**: controladores/endpoints delgados; solo traducen HTTP a casos de uso.
- Las dependencias van en un solo sentido. Si una capa necesita algo de otra, define un puerto.

## Stack y entorno
- .NET 10, C#, xUnit. Angular + TypeScript en `Frontend/`. PostgreSQL 17 con EF Core 10 + Npgsql + EFCore.NamingConventions (snake_case).
- `Directory.Build.props`: net10.0, Nullable, `TreatWarningsAsErrors` (excepto NU1901 a NU1904). No lo debilites.
- Base de datos local: `docker compose up -d`, contenedor `citadent-db`, **puerto host 5433** (el 5432 lo ocupa otro PostgreSQL). Usuario, base y clave de desarrollo: `citadent` / `citadent` / `citadent_dev`.
- Cadena de conexión: `ConnectionStrings:Default` en `appsettings.Development.json`.
- Migraciones: ejecutar desde `Backend/` con `ASPNETCORE_ENVIRONMENT=Development`:
  `dotnet ef migrations add <Name> --project Infrastructure --startup-project WebApiRest --output-dir Persistence/Migrations`
  `dotnet ef database update --project Infrastructure --startup-project WebApiRest`
- Las pruebas de integración (`Tests/Infrastructure.Tests`) necesitan la base del compose y crean una base temporal por clase.

## Convenciones del dominio (no las rompas)
- Errores de negocio: `DomainException(code, message)` con código `RN-xx` o `"VALIDATION"`. La API los traduce a respuestas HTTP con el código; el frontend los traduce a mensajes en español.
- IDs: `Guid.CreateVersion7()`, generados en el dominio.
- Fechas: `DateTimeOffset` en **UTC** (`TimeRange` lo normaliza, rango semiabierto `[inicio, fin)`). La zona horaria de Ecuador (`America/Guayaquil`) y la fecha de "hoy" se **pasan como parámetro**; el dominio no usa reloj. Application usa `TimeProvider`.
- Solo las citas en estado `Scheduled` (1) ocupan tiempo. Estados: Scheduled=1, Completed=2, Cancelled=3, NoShow=4.
- Los cruces de odontólogo y sillón (RN-02, RN-03) los valida el dominio con el `SchedulingContext` **y** los garantizan las restricciones `EXCLUDE` de PostgreSQL. Si la base rechaza (SqlState `23P01`), Application lo traduce al RN-02 o RN-03 según el nombre de la restricción.
- RN-06 (no agendar en el pasado), RN-09 (quién completa la cita) y RN-11 pertenecen a **Application**, no al dominio.
- Para EF: los tipos con propiedades privadas necesitan constructor sin parámetros privado (ver `TimeRange` y `WorkingHours`).
- Auditoría: `CreatedAt`/`UpdatedAt` son propiedades sombra que rellena `SaveChanges`.

## Reglas de negocio
El texto completo de RN-01 a RN-19 está en `docs/reglas-de-negocio.md`. Léelo antes de implementar cualquier regla. Resumen por grupos: agenda RN-01..09, pacientes RN-10..12, clínico RN-13..15, catálogo RN-16, tratamientos RN-17, pagos RN-18..19.

## Estado y hoja de ruta
Mira `docs/traspaso.md` (estado actual) y `docs/plan-de-trabajo.md` (fases y tareas con criterios de aceptación).

## Cosas que no debes hacer
- No modificar migraciones ya aplicadas; crea una nueva.
- No guardar secretos en el repo (usa variables de entorno o `appsettings.Development.json` ignorado cuando aplique).
- No usar datos reales de pacientes: todo dato de demo es ficticio.
- No saltar las pruebas ni silenciar advertencias para que compile.