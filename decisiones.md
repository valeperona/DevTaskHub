# Decisiones técnicas

1. **EF Core + SQLite**: se agregó un `DbContext` dedicado (`DevTaskHubContext`) con migraciones bajo `Data/Migrations`. El arranque de la API ejecuta `Database.Migrate()` para garantizar que `devtaskhub.db` exista sin pasos manuales.
2. **Factory de diseño**: `DevTaskHubContextFactory` permite `dotnet-ef` aun con la plantilla minimalista y evita depender del host en tiempo de diseño.
3. **Paquetes locales**: `nuget.config` redefine `globalPackagesFolder` dentro del repo para sortear restricciones de escritura fuera del workspace.
4. **Exposición REST**: se mantuvieron controladores separados para salud (`HealthController`), proyectos (`ProjectsController`) y actualización de tareas (`TasksController`). Las rutas cumplen exactamente con lo solicitado.
5. **Swagger + CORS**: Swagger está siempre disponible en Development y CORS se limita a `http://localhost:5173` para facilitar luego la securización.
6. **Frontend ligero**: se eligieron CSS Modules (sin Tailwind) para evitar dependencias extra; el `apiClient` centraliza llamadas y el estado de sesión vive en `localStorage` solamente para demos.
7. **Routing**: React Router controla Login → Proyectos → Tableros con un guard dinámico; es suficiente para pruebas sin necesidad de un store complejo.
