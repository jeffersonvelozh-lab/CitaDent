# CitaDent

API REST de AgendaDental, desarrollada con ASP.NET Core y .NET 10.

## Requisitos

- .NET 10 SDK
- Docker (opcional, para ejecutar la API en un contenedor)

## Ejecutar localmente

Desde la raíz del repositorio:

```bash
dotnet run --project WebApiRest/WebApiRest.csproj
```

La API ofrece el endpoint de ejemplo `GET /WeatherForecast`. En el entorno de
desarrollo, el documento OpenAPI está disponible en `/openapi/v1.json`.

## Ejecutar con Docker

Construye la imagen desde la raíz del repositorio:

```bash
docker build -f WebApiRest/Dockerfile -t agendadental-api .
```

Inicia el contenedor y publica el puerto HTTP:

```bash
docker run --rm -p 8080:8080 agendadental-api
```
