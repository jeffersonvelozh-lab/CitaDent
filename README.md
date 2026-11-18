# CitaDent

![CI](https://github.com/TU_USUARIO/CitaDent/actions/workflows/ci.yml/badge.svg)

Aplicación web para la gestión de un consultorio odontológico: pacientes, agenda de citas sin cruces de odontólogo ni sillón, odontograma y, en una fase posterior, planes de tratamiento y cobros.

Proyecto de portafolio. Los datos de pacientes que se usen en la demo son ficticios.

## Stack

| Capa | Tecnología |
| --- | --- |
| Backend | .NET 10, C#, arquitectura hexagonal (Domain, Application, Infrastructure, WebApiRest) |
| Base de datos | PostgreSQL 17 con EF Core (Npgsql) |
| Frontend | Angular, TypeScript |
| Pruebas | xUnit |
| Infraestructura local | Docker Compose |

## Estructura

```
CitaDent/
├─ Backend/
│  ├─ Domain/          reglas de negocio, sin dependencias
│  ├─ Application/     casos de uso y puertos
│  ├─ Infrastructure/  EF Core, PostgreSQL y otros adaptadores
│  ├─ Shared/          utilidades transversales
│  ├─ WebApiRest/      API HTTP
│  └─ Tests/           pruebas por capa
├─ Frontend/           aplicación Angular
├─ docs/               decisiones de arquitectura
└─ docker-compose.yml  PostgreSQL para desarrollo
```

## Requisitos

- SDK de .NET 10
- Docker Desktop
- Herramienta `dotnet-ef`: `dotnet tool install --global dotnet-ef --version "10.0.*"`

## Cómo correr el backend

Desde la carpeta raíz, levanta la base de datos:

```powershell
docker compose up -d
```

El contenedor expone PostgreSQL en el puerto **5433** para no chocar con una instalación local en el 5432.

Luego, desde `Backend/`, aplica las migraciones y arranca la API:

```powershell
cd Backend
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet ef database update --project Infrastructure --startup-project WebApiRest
dotnet run --project WebApiRest
```

La API queda en `http://localhost:5080` y `http://localhost:5080/health` responde `Healthy` cuando llega a la base.

La cadena de conexión de desarrollo está en `Backend/WebApiRest/appsettings.Development.json` (clave `ConnectionStrings:Default`). En otros entornos se sobreescribe con la variable `ConnectionStrings__Default`.

## Pruebas

```powershell
cd Backend
dotnet test
```

## Estado

- [x] Fase 0: estructura, PostgreSQL con Docker, base del dominio, conexión de punta a punta
- [ ] Fase 1: agenda, pacientes, acceso por roles, pantallas Angular, odontograma
- [ ] Fase 2: planes de tratamiento, abonos, adjuntos y recordatorios

Las decisiones de diseño están en [docs/decisiones.md](docs/decisiones.md).