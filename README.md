# CitaDent

CitaDent es una aplicación web para la gestión de citas odontológicas. El
repositorio contiene la base inicial de un frontend desarrollado con Angular y
una API REST desarrollada con ASP.NET Core.

Actualmente el proyecto es principalmente una estructura inicial: el backend
incluye un controlador de ejemplo y las capas previstas para la aplicación, y
el frontend contiene la aplicación generada con Angular. Aún no hay módulos de
citas, pacientes, odontólogos ni integración completa entre frontend y backend.

## Tecnologías

- .NET 10 / ASP.NET Core
- Angular 21 con Server-Side Rendering
- TypeScript
- OpenAPI
- Docker
- NPM

## Estructura del proyecto

```text
CitaDent/
├── Backend/
│   ├── Application/       Casos de uso y lógica de aplicación
│   ├── Domain/            Entidades y reglas del dominio
│   ├── Infrastructure/    Acceso a datos y servicios externos
│   ├── Shared/            Elementos compartidos
│   ├── WebApiRest/        API REST ASP.NET Core
│   ├── Tests/             Proyectos y pruebas (pendiente de implementar)
│   └── Backend.slnx       Solución del backend
└── Frontend/
    ├── src/app/           Componentes y configuración Angular
    ├── src/main.ts        Arranque de la aplicación
    └── package.json       Dependencias y scripts
```

## Requisitos

- .NET 10 SDK
- Node.js compatible con Angular 21
- npm
- Docker (opcional)

## Ejecutar el backend

Desde la raíz del repositorio:

```bash
cd Backend
dotnet restore Backend.slnx
dotnet run --project WebApiRest/WebApiRest.csproj
```

Con el perfil HTTP, la API queda disponible en `http://localhost:5080`.

Endpoints disponibles actualmente:

- `GET /WeatherForecast`: devuelve datos de ejemplo.
- `GET /openapi/v1.json`: documento OpenAPI disponible en modo Development.

También puede abrirse la solución con Visual Studio o ejecutarse los proyectos
desde su carpeta.

## Ejecutar el frontend

Desde la raíz del repositorio:

```bash
cd Frontend
npm install
npm start
```

La aplicación queda disponible en `http://localhost:4200`.

Scripts disponibles:

```bash
npm start          # Servidor de desarrollo
npm run build      # Compila la aplicación
npm test           # Ejecuta las pruebas unitarias
npm run watch      # Compilación continua
```

## Ejecutar el backend con Docker

Desde la carpeta `Backend`:

```bash
docker build -f WebApiRest/Dockerfile -t citadent-api .
docker run --rm -p 8080:8080 citadent-api
```

La API se expone en `http://localhost:8080`.

## Compilar y probar

Backend:

```bash
cd Backend
dotnet build Backend.slnx
dotnet test Backend.slnx
```

Frontend:

```bash
cd Frontend
npm run build
npm test
```

## Estado actual

El backend y el frontend compilan como proyectos base, pero todavía quedan por
definir e implementar:

- Modelos del dominio odontológico
- Operaciones CRUD y API de citas
- Gestión de pacientes y profesionales
- Base de datos y repositorios
- Autenticación y autorización
- Validaciones y manejo centralizado de errores
- Comunicación real entre Angular y ASP.NET Core
- Diseño de la interfaz principal
