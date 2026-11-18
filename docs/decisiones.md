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