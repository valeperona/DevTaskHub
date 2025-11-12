# DevTaskHub

Base full-stack para prácticas de CI/CD, testing y contenedores.

## Estructura
- `backend/DevTaskHub.sln`: solución .NET 8 con la API (`DevTaskHub.Api`) y los tests (`DevTaskHub.Tests`).
- `frontend/devtaskhub-frontend/`: aplicación React + TypeScript creada con Vite.
- `nuget.config`: fuerza los paquetes NuGet a descargarse dentro del repo.
- `decisiones.md`: acuerdos técnicos y supuestos.

## Requisitos
- .NET SDK 8.0
- Node.js 20+ (Vite recomienda >=18)

## Backend
```bash
cd backend
DOTNET_CLI_HOME=../.dotnet dotnet tool run dotnet-ef database update # crea devtaskhub.db (opcional)
dotnet run --project DevTaskHub.Api
```

Características destacadas:
- ASP.NET Core Web API + Entity Framework Core (SQLite `devtaskhub.db`).
- Entidades `Project` y `TaskItem` con enums `Status` y `Priority` almacenados como texto.
- Endpoints solicitados + Swagger UI y CORS habilitado para `http://localhost:5173`.
- Migraciones iniciales en `DevTaskHub.Api/Data/Migrations` ejecutadas en el arranque (`Database.Migrate`).
- Test de integración xUnit en `DevTaskHub.Tests` que verifica `/api/health` con `WebApplicationFactory`.

Comandos útiles:
```bash
# Ejecutar tests
DOTNET_CLI_HOME=../.dotnet dotnet test DevTaskHub.sln

# Crear nuevas migraciones (usa la herramienta local dotnet-ef)
DOTNET_CLI_HOME=../.dotnet dotnet tool run dotnet-ef migrations add <Nombre>
```

## Frontend
```bash
cd frontend/devtaskhub-frontend
npm install
npm run dev -- --host 0.0.0.0 --port 5173
```

- Variables: `VITE_API_BASE_URL` (default `http://localhost:5000`).
- Páginas: Login (fake), listado de proyectos y tablero kanban simple por estado.
- Cliente API centralizado en `src/api/apiClient.ts` (fetch).
- Estilos con CSS Modules.

## Flujo básico
1. `npm run dev` levanta el frontend en `http://localhost:5173`.
2. `dotnet run --project backend/DevTaskHub.Api` expone la API en `http://localhost:5000` (Swagger disponible).
3. Loguearse con un email cualquiera, crear proyectos y tareas; la UI persiste el usuario en `localStorage`.

## Próximos pasos sugeridos
- Configurar Docker Compose para API + frontend.
- Agregar pipelines (GitHub Actions/Azure DevOps) con `dotnet test` y `npm run build`.
- Integrar herramientas de cobertura y análisis estático.
