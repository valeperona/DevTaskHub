# Decisiones técnicas

## Backend
1. **EF Core + SQLite (prod) / InMemory (tests)**  
   - `DevTaskHubContext` se mantiene como única dependencia externa real (la base de datos).  
   - Para pruebas unitarias se usa `Microsoft.EntityFrameworkCore.InMemory`, lo que permite ejecutar los controladores sin levantar SQLite ni mockear capas artificiales. Esto se documenta como nuestra estrategia de mocking: las dependencias del dominio quedan aisladas mediante un provider en memoria.  
   - En tests de integración (`HealthEndpointTests`) se agregó `CustomWebApplicationFactory` con SQLite en memoria (`Microsoft.Data.Sqlite`) para poder ejecutar `Database.Migrate()` durante la inicialización sin chocar con archivos locales.

2. **Cobertura de controladores**  
   - `ProjectsControllerTests` y `TasksControllerTests` utilizan el patrón AAA y cubren tanto paths felices como validaciones (`BadRequest`) y estados `NotFound`.  
   - Se eligió no introducir Moq porque no existe una capa de servicios o repositorios adicional; el objetivo de mocking ya se cumple aislando EF Core.

3. **Pipeline backend**  
   - `azure-pipelines.yml` ya incluía `DotNetCoreCLI@2` con `command: test`, por lo que la suite backend se ejecuta automáticamente en cada build.

## Frontend
1. **Framework de testing**  
   - Dado que el proyecto usa Vite, se adoptó Vitest con React Testing Library (`@testing-library/react` + `@testing-library/jest-dom`). Se agregaron scripts `npm test`, `npm run test:watch` y `npm run test:run`.

2. **Mocks en frontend**  
   - `apiClient.test.ts` usa `vi.spyOn(globalThis, 'fetch')` para simular respuestas HTTP (200 y 500) y validar que el wrapper propaga correctamente los resultados o errores.  
   - `ProjectsPage.test.tsx` declara `vi.mock('../api/apiClient')` para aislar el componente del backend, inyectando datos dummy o errores controlados.

3. **Cobertura de componentes**  
   - `App.test.tsx` verifica el render básico (login).  
   - `ProjectsPage.test.tsx` comprueba que el listado muestre proyectos mockeados y mensajes de error cuando la API falla.

4. **Pipeline frontend**  
   - Se añadió un paso `npm run test:run` después de `npm install` para ejecutar Vitest en CI con `CI=true`.

## TP06 – Estrategia de testing unificado
- **Frameworks elegidos:** xUnit + coverlet para backend; Vitest + React Testing Library + JSDOM para frontend. Reporter JUnit habilitado en Vitest (genera `coverage/vitest-junit.xml`).
- **Mocking/Aislamiento:** backend usa `Microsoft.EntityFrameworkCore.InMemory` para datos, helper de `ClaimsPrincipal` y pruebas AAA; se cubren controladores con reglas de negocio y roles. Frontend mockea `fetch` y el `apiClient` con `vi.mock`, y usa `localStorage` stub para sesiones.
- **Cobertura:** backend se ejecuta con `--collect:"XPlat Code Coverage"`; frontend genera `coverage/` y `coverage/vitest-junit.xml`. Se publican resultados en Azure Pipelines.
- **Casos relevantes cubiertos:** validaciones de Auth (duplicados, credenciales inválidas), Projects (owner/members, invitaciones, transferencia, leave con reasignación, permisos viewer), Tasks (transiciones, checklist pendiente, prioridad alta sin asignar, completedLate, asignación a viewer prohibida), API client (errores/headers/204), LoginPage (errores y éxito), ProjectsPage (load/create/delete), ProjectBoardPage (filtros, restricción viewer).
## Evidencias de ejecución
1. **Backend:** `dotnet test backend/DevTaskHub.sln`  
   - Salida esperada: todas las suites (`DevTaskHub.Tests`) pasando. Guardar captura o log para defensa.
2. **Frontend:** `cd frontend/devtaskhub-frontend && npm test`  
   - Ejecuta Vitest (modo watch). Para CI se usa `npm run test:run`. Capturar el reporte (3 suites, 5 tests) como evidencia.

## Próximos pasos sugeridos
- Mantener actualizadas las capturas de `dotnet test` y `npm test`.
- Revisar periódicamente `npm audit` y actualizar dependencias si se requiere, ya que la herramienta reportó vulnerabilidades moderadas.
