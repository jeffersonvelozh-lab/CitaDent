# Plan de trabajo de CitaDent

Cada tarea es pequeña (idealmente una sesión de Claude Code) y termina con una **puerta de revisión**: Jefferson revisa el código, corre las pruebas y aprueba antes de pasar a la siguiente.

**Formato de cada tarea:** ID · qué se hace · criterio de aceptación (CA).
**Estado:** `[x]` hecho, `[ ]` pendiente.

## Cómo pedirle cada tarea a Claude Code
```
Lee CLAUDE.md, docs/traspaso.md y docs/plan-de-trabajo.md.
Haz solo la tarea 1b-03. Primero dime tu plan (archivos, pruebas, dudas) y espera mi aprobación.
```

---

## Fase 0: Cimientos `[x]`
Repo, Docker Compose con PostgreSQL, solución .NET, CI en GitHub Actions, `/health`.

## Fase 1a: Dominio y base de datos `[x]`
Entidades Procedure, Patient, Dentist, Chair, ScheduleBlock, Appointment; restricciones `EXCLUDE`; 131 pruebas en verde.

---

## Fase 1b: API y autenticación
Objetivo: una API que permita iniciar sesión por rol y gestionar pacientes, catálogo, odontólogos, sillones y citas, aplicando todas las reglas.

### Bloque A: Usuarios y seguridad
- `[x] 1b-01` **Entidad `User` y roles.** Dominio: `User` (email único, nombre, hash de contraseña, rol, activo) y enum `UserRole` (Admin, Reception, Dentist). CA: pruebas de dominio para creación, email inválido y activar/desactivar.
- `[x] 1b-02` **Configuración EF y migración de `users`.** Agrega las FK pendientes `dentists.user_id` y `appointments.created_by_user_id`. CA: migración aplicada, `\d users` correcto, pruebas de integración existentes siguen en verde.
- `[x] 1b-03` **Puerto y adaptador de hash de contraseñas.** Interfaz `IPasswordHasher` en Application, implementación con PBKDF2 en Infrastructure. CA: pruebas de hash correcto, hash incorrecto y salt distinto por hash.
- `[x] 1b-04a` **Caso de uso de login y emisión de JWT.** Puertos `IUserRepository` e `IAccessTokenIssuer`, `LoginHandler` en Application, emisor JWT (HS256) y repositorio de usuarios en Infrastructure. CA: pruebas del handler con dobles (credenciales válidas, inválidas, usuario inactivo), pruebas del token (claims, expiración, firma) y validación de la clave al arrancar.
- `[x] 1b-04b` **Endpoint de login.** `POST /api/auth/login` devuelve token con rol y expiración. CA: prueba de integración con `WebApplicationFactory` con credenciales válidas e inválidas; la clave de firma sale de configuración, no del código.
- `[x] 1b-05a` **Autorización por rol.** Validación del JWT en cada petición (JwtBearer), políticas `AdminOnly`, `StaffWithAgendaAccess` (Admin, Reception) y `ClinicalAccess` (Admin, Dentist), y autenticación exigida por defecto (`[AllowAnonymous]` solo en login, `/health` y OpenAPI). CA: un endpoint de prueba responde 401 sin token, 403 con rol insuficiente y 200 con el rol correcto; token con otra firma, vencido o de otro emisor da 401.
- `[x] 1b-05b` **Usuario administrador inicial.** Siembra del primer Admin desde configuración (`Seed:Admin:*`, contraseña por user-secrets o variable de entorno, mínimo 12 caracteres), idempotente. CA: pruebas del caso de uso con dobles, del repositorio y una de integración donde el Admin sembrado inicia sesión y accede a un endpoint `AdminOnly`.

### Bloque B: Base de la capa Application y la API
- `[x] 1b-06` **Manejo de errores.** Middleware que convierte `DomainException` en `ProblemDetails` con el código RN-xx (400/409/422 según el caso) y un 500 genérico para el resto. CA: pruebas que verifican el código y el estado HTTP por regla.
- `[x] 1b-07` **Convenciones de casos de uso.** Plantilla de caso de uso (comando, manejador, validación), registro por DI y `IUnitOfWork` o equivalente. CA: un caso de uso de ejemplo con prueba unitaria usando dobles.

### Bloque C: Catálogos y personas
- `[x] 1b-08a` **Procedimientos: casos de uso y repositorio.** Crear, editar, activar/desactivar, consultar y listar (RN-16) en Application; `IQueryHandler`; códigos `NOT-FOUND` y `DUPLICATE`; nombre único sin distinguir mayúsculas; `ProcedureRepository` en Infrastructure. CA: pruebas de Application con dobles y del repositorio; cambiar la duración no altera citas ya creadas.
- `[x] 1b-08b` **Procedimientos: endpoints.** `GET/POST/PUT /api/procedures`, `POST /api/procedures/{id}/activate|deactivate`; cualquier rol lee, solo Admin escribe; `NOT-FOUND` → 404 y `DUPLICATE` → 409. CA: pruebas de integración de los endpoints (201, 200, 204, 401, 403, 404, 409, 422).
- `[x] 1b-09a` **Pacientes: casos de uso y repositorio.** Crear y consultar en Application con RN-10 (documento válido y único por tipo, `DUPLICATE`) y RN-12; `ClinicClock` da la fecha de hoy en la zona de la clínica (`Clinic:TimeZone`, `America/Guayaquil`); `PatientRepository` en Infrastructure. CA: no se puede repetir documento; el menor exige representante; "hoy" se calcula en hora de Ecuador.
- `[x] 1b-09b` **Pacientes: endpoints.** `POST /api/patients` (Admin y Reception) y `GET /api/patients/{id}` (cualquier rol); enums como texto en el JSON. CA: pruebas de integración (201, 200, 400, 401, 403, 404, 409, 422).
- `[x] 1b-10a` **Pacientes: buscar y editar (casos de uso y repositorio).** `Patient.Update` en el dominio (RN-10, RN-12; si falla no cambia nada); `SearchPatientsHandler` (cada palabra debe aparecer en nombre, apellido o documento, sin distinguir mayúsculas; `%` y `_` como texto), `UpdatePatientHandler` (`DUPLICATE` si otro paciente tiene el documento) y `SetPatientActiveHandler` (RN-11: no hay borrado, solo desactivar); `PagedResult<T>` y `Paging` (página desde 1, tamaño 1..100, 20 por defecto; `VALIDATION` fuera de esos límites). CA: pruebas de búsqueda parcial, sin resultados y página fuera de rango (vacía con el total).
- `[x] 1b-10b` **Pacientes: endpoints de búsqueda y edición.** `GET /api/patients?term=&includeInactive=&page=&pageSize=` (cualquier rol), `PUT /api/patients/{id}` y `POST /api/patients/{id}/activate|deactivate` (Admin y Reception). CA: pruebas de integración (200, 204, 400, 401, 403, 404, 409, 422).
- `[x] 1b-11a` **Sillones: casos de uso y repositorio.** Crear, renombrar, activar/desactivar, consultar y listar en Application; nombre único sin distinguir mayúsculas (`DUPLICATE`), también frente a sillones inactivos; no se borran; `ChairRepository` en Infrastructure. CA: pruebas de unicidad y estado con dobles y del repositorio.
- `[x] 1b-11b` **Sillones: endpoints.** `GET /api/chairs` (`includeInactive`), `GET /api/chairs/{id}`, `POST`, `PUT /api/chairs/{id}` y `POST /api/chairs/{id}/activate|deactivate`; cualquier rol lee, solo Admin escribe. CA: pruebas de integración (200, 201, 204, 400, 401, 403, 404, 409).
- `[x] 1b-12a` **Odontólogos y horario semanal: casos de uso y repositorio.** `RegisterDentistHandler` crea el `User` (rol Dentist) y el `Dentist` a la vez (email y licencia únicos, `DUPLICATE`; contraseña de 12 caracteres o más); `SetWeeklyScheduleHandler`, consultar y listar; `DentistRepository`; migración `AddDentistLicenseUniqueIndex`. CA: bloques solapados rechazados; el horario se reemplaza completo (comprobado en `dentist_schedules`).
- `[x] 1b-12b` **Odontólogos y horario semanal: endpoints.** `GET /api/dentists` (`includeInactive`) y `GET /api/dentists/{id}` (cualquier rol), `POST /api/dentists` y `PUT /api/dentists/{id}/schedule` (solo Admin). CA: pruebas de integración (200, 201, 400, 401, 403, 404, 409).
- `[x] 1b-12c` **Odontólogos: editar datos.** `Dentist.Update` en el dominio (nombre, apellido, especialidad, licencia; si falla no cambia nada; una especialidad vacía la borra) y `User.ChangeFullName`; `IUserRepository.GetByIdAsync`; `PUT /api/dentists/{id}` (licencia única sin distinguir mayúsculas, `DUPLICATE`) que también actualiza el nombre completo de su `User`. Solo Admin. CA: pruebas de dominio, de Application con dobles y de integración (200, 400, 401, 403, 404, 409).
- `[x] 1b-12d` **Odontólogos: activar/desactivar.** `POST /api/dentists/{id}/activate|deactivate`, que activa o desactiva también su `User` (un odontólogo inactivo no inicia sesión). Solo Admin. CA: pruebas de Application con dobles y de integración (204, 401, 403, 404); un odontólogo desactivado recibe 401 al iniciar sesión y, reactivado, vuelve a entrar.
- `[x] 1b-12e` **Usuarios: casos de uso y repositorio.** En el dominio, `User.ChangeEmail`, `ChangePasswordHash` y `ChangeRole` (solo entre Admin y Recepción). En Application: crear usuarios de Recepción y Admin (los odontólogos se crean con `POST /api/dentists`), listar (`includeInactive`, rol) y consultar, cambiar el correo (único, `DUPLICATE`), restablecer la contraseña (mínimo 12 caracteres), cambiar el rol y activar/desactivar; nunca se devuelve el hash. Desactivar o degradar al último Admin activo da `LAST-ADMIN`. Los usuarios odontólogos se activan y desactivan solo con `/api/dentists` (`VALIDATION`). CA: pruebas de dominio, de Application con dobles y del repositorio.
- `[x] 1b-12f` **Usuarios: endpoints.** `/api/users` solo para Admin: `GET` (`includeInactive`, `role`), `GET /{id}`, `POST`, `PUT /{id}/email`, `PUT /{id}/password`, `PUT /{id}/role` y `POST /{id}/activate|deactivate`; `LAST-ADMIN` responde 409. CA: pruebas de integración (200, 201, 204, 400, 401, 403, 404, 409); tras cambiar el correo o la contraseña, el login funciona con los datos nuevos y falla con los viejos.
- `[ ] 1b-12g` **Usuarios: editar el nombre completo** de usuarios de Recepción y Admin (el de los odontólogos sigue a su ficha). CA: pruebas de dominio ya existentes (`ChangeFullName`), de Application con dobles y de integración.
- `[ ] 1b-13` **Bloqueos de agenda.** Crear y quitar bloqueos de un odontólogo o de toda la clínica. CA: un bloqueo impide agendar (RN-05) en ese rango.

### Bloque D: Citas
- `[ ] 1b-14` **Cargar `SchedulingContext`.** Consulta de bloqueos y citas `Scheduled` cercanas al horario pedido, por odontólogo y sillón. CA: prueba de integración que verifica que solo se cargan registros del rango relevante.
- `[ ] 1b-15` **Agendar cita.** Caso de uso con RN-06, reglas del dominio (RN-01, 02, 03, 05) y traducción del `23P01` a RN-02 o RN-03 cuando dos usuarios chocan a la vez. CA: prueba de concurrencia real contra PostgreSQL (dos solicitudes simultáneas, una gana).
- `[ ] 1b-16` **Cancelar, completar y marcar inasistencia.** RN-07, RN-08, RN-09 (quién puede completar). CA: pruebas por rol y por estado inicial inválido.
- `[ ] 1b-17` **Consultar agenda.** Citas por día y por odontólogo, y disponibilidad libre para un procedimiento. CA: pruebas con horarios, bloqueos y citas existentes.

### Bloque E: Cierre
- `[ ] 1b-18` **Datos de demostración.** Siembra de 3 odontólogos, 2 sillones, procedimientos y pacientes ficticios (solo en Development). CA: `dotnet run` en base vacía deja un entorno usable.
- `[ ] 1b-19` **OpenAPI/Swagger y pruebas de extremo a extremo de la API.** CA: Swagger documenta los endpoints con autenticación; un recorrido completo (login, crear paciente, agendar, cancelar) pasa con `WebApplicationFactory`.
- `[ ] 1b-20` **Cierre de fase.** Actualizar README, `docs/traspaso.md`, `docs/decisiones.md`; CI en verde; etiqueta `fase-1b`.

---

## Fase 1c: Frontend Angular
Objetivo: interfaz en español para Recepción, Odontólogo y Admin.

- `[ ] 1c-01` **Proyecto Angular y estructura.** Rutas, carpeta por funcionalidad, estilos base, entorno local. CA: `ng serve` muestra una pantalla vacía con layout.
- `[ ] 1c-02` **Login, almacenamiento del token e interceptor.** Guard por rol. CA: rutas protegidas redirigen al login; el token se adjunta a las llamadas.
- `[ ] 1c-03` **Traducción de errores RN-xx a español.** Diccionario único de mensajes. CA: cada código del backend tiene su mensaje.
- `[ ] 1c-04` **Pacientes:** lista con búsqueda, formulario de alta y edición, validación del representante de menores.
- `[ ] 1c-05` **Catálogos:** procedimientos, sillones y odontólogos con su horario semanal.
- `[ ] 1c-06` **Agenda:** vista diaria y semanal por odontólogo.
- `[ ] 1c-07` **Agendar cita:** formulario que muestra disponibilidad y errores de cruce de forma clara.
- `[ ] 1c-08` **Acciones sobre la cita:** cancelar con motivo, completar, marcar inasistencia.
- `[ ] 1c-09` **Pulido y accesibilidad básica:** estados vacíos, cargando, errores, teclado.
- `[ ] 1c-10` **Cierre de fase:** prueba de humo de extremo a extremo, capturas para el README, etiqueta `fase-1c`.

**Hito: MVP.** Cuando 1c esté cerrada, desplegar en hosting gratuito (tarea de despliegue: Dockerfile de API, variables de entorno, base gestionada, CI/CD).

---

## Fase 1d: Clínico y odontograma
Reglas RN-13 a RN-15 (ver `docs/reglas-de-negocio.md`).

- `[ ] 1d-01` Dominio de la historia clínica del paciente y sus notas.
- `[ ] 1d-02` Dominio del odontograma (piezas dentales con numeración FDI y estado por superficie).
- `[ ] 1d-03` Persistencia y migración.
- `[ ] 1d-04` Casos de uso y endpoints; permisos (Odontólogo escribe, Recepción solo lee lo permitido).
- `[ ] 1d-05` Pantalla del odontograma en Angular.
- `[ ] 1d-06` Notas de la consulta ligadas a la cita completada.
- `[ ] 1d-07` Cierre de fase.

---

## Fase 2: Ampliaciones
- Planes de tratamiento (RN-17): varios procedimientos con seguimiento.
- Pagos y saldos (RN-18, RN-19).
- Adjuntos del paciente (radiografías, consentimientos).
- Recordatorios de cita (correo o WhatsApp).
- Varios procedimientos por cita (reevaluar el cálculo de duración).
- Reportes básicos.

Cada punto se divide en tareas de este mismo formato cuando se llegue a él.

---

## Definición de "hecho" para cualquier tarea
- `dotnet build` y `dotnet test` en verde (y `ng test`/`ng build` cuando toque el frontend).
- Pruebas nuevas que cubren el comportamiento y los casos de error.
- Sin advertencias nuevas ni cambios fuera del alcance.
- Resumen entregado a Jefferson según `CLAUDE.md`.
- CI en verde antes de fusionar.