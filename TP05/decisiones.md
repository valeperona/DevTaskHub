# DevTaskHub – TP05 CI/CD (Backend + QA/Prod en Azure)

Este repositorio forma parte del **Trabajo Práctico 05 – DevOps CI/CD Pipelines (2025)** de la materia **Calidad de Software / Ing. de Software 3**.

El objetivo de este trabajo práctico es:
- Tener una **aplicación con backend desplegado en Azure**.
- Implementar un **pipeline CI/CD en Azure DevOps** que:
  - Compile y publique el backend.
  - Use un **self-hosted agent**.
  - Despliegue automáticamente a **QA**.
  - Permita promover el mismo artefacto a **Producción**, con aprobación manual.
  - Mantenga una estrategia básica de **rollback**.


---

## 1. Arquitectura general

- **Backend**: API REST en **.NET 8**  
- **Frontend**: (no desplegado en este TP; se utiliza sólo local para pruebas)  
- **Base de datos**: Azure SQL Database (conexión a través de connection strings en Azure App Service)
- **Infraestructura en Azure**:
  - 1 Resource Group para QA (por ejemplo: `rg-devtaskhub-qa`)
  - 1 Resource Group para Prod (por ejemplo: `rg-devtaskhub-prod`)
  - 1 App Service Plan (por ambiente) y 1 Web App (por ambiente), por ejemplo:
    - QA: `app-devtaskhub-api-qa`
    - Prod: `app-devtaskhub-api-prod`

---

## 2. Requisitos para correr el proyecto localmente

### 2.1. Requisitos generales

- **SO**: macOS / Windows / Linux
- **.NET SDK**: `8.0.x`
- **Git**
- **Azure SQL / SQL Server local** (para base de datos de desarrollo)

### 2.2. Backend (.NET 8)

- `.NET SDK 8` instalado:
  ```bash
  dotnet --version
  ```
  Debe devolver una versión 8.x.

- Cadena de conexión a BD (ejemplo local):
  ```
  Server=localhost,1433;Database=DevTaskHub;User Id=sa;Password=TuPasswordSegura;TrustServerCertificate=True;
  ```
  Se configura vía `appsettings.Development.json` o variables de entorno.

---

## 3. Estructura principal del repositorio

Ejemplo (ajustar nombres a los reales):

```
DevTaskHub/
├── backend/
│   ├── DevTaskHub.Api/
│   │   ├── DevTaskHub.Api.csproj
│   │   ├── Controllers/
│   │   ├── Services/
│   │   ├── ...
│   └── DevTaskHub.Api.Tests/
│       ├── DevTaskHub.Api.Tests.csproj
│       └── ...
├── frontend/
│   └── devtaskhub-frontend/   (no forma parte del despliegue del TP05)
└── azure-pipelines.yml        (pipeline CI/CD para el backend)
```

---

## 4. Cómo correr el backend localmente

### 4.1. Clonar el repositorio

```bash
git clone <URL_DEL_REPO>
cd DevTaskHub/backend/DevTaskHub.Api
```

### 4.2. Restaurar dependencias

```bash
dotnet restore
```

### 4.3. Configurar la base de datos

Ajustar `appsettings.Development.json` con la conexión local:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=DevTaskHub;User Id=sa;Password=TuPassword;TrustServerCertificate=True;"
  }
}
```

Aplicar migraciones (si las hay):

```bash
dotnet ef database update
```

### 4.4. Ejecutar la API

```bash
dotnet run
```

La API normalmente se levanta en:
- `https://localhost:5001` o
- `https://localhost:7042` (según configuración de Kestrel / launchSettings)

---

## 5. Pipeline CI/CD en Azure DevOps

El pipeline está definido en el archivo:

```
azure-pipelines.yml
```

### 5.1. Descripción general

- **Tipo**: Pipeline YAML
- **Agente**: Self-hosted agent corriendo en mi máquina (macOS).
- **Trigger**: `main` (cada push / merge a la rama main dispara el pipeline).
- **Stages**:
  - **Build**: compila, corre tests (si están habilitados) y genera artefactos.
  - **Deploy_QA**: despliega artefactos a la Web App de QA.
  - **Deploy_Prod**: despliega el mismo artefacto a Producción, con aprobación manual.

### 5.2. Prerrequisitos del Self-Hosted Agent

En mi máquina local (Mac) configuré un agente de Azure DevOps con:

- .NET SDK 8 instalado
- Acceso a Internet (para llegar a Azure DevOps y Azure App Service)
- Agente registrado en Pool: `Default` con demands:

```yaml
pool:
  name: Default
  demands:
    - Agent.OS -equals Darwin
```

Pasos resumidos (ya realizados) para el agente:

1. Desde Azure DevOps → Organization → Agent pools → Default → New agent.
2. Descargar el agente para macOS.
3. Configurarlo con:
   ```bash
   ./config.sh
   ```
   (URL de la organización, PAT, nombre del agente, etc.)
4. Iniciar el servicio del agente:
   ```bash
   ./run.sh
   ```
   o configurarlo como servicio para que arranque automáticamente.

---

## 6. Variables y configuración del pipeline

En Azure DevOps, el pipeline usa variables para no hardcodear datos sensibles:

**En el YAML**:

```yaml
variables:
  buildConfiguration: 'Release'
  webAppNameQa: 'app-devtaskhub-api-qa'
  webAppNameProd: 'app-devtaskhub-api-prod'
```

**En Library / Variable Groups** (opcional):
- `azureSubscription`: nombre de la Service Connection a Azure
- Connection strings de QA / Prod se configuran directamente en el App Service.

---

## 7. Flujo del pipeline (cómo corre)

1. **Trigger**: commit en `main`.

2. **Stage Build**:
   - Usa el self-hosted agent.
   - Restaura paquetes:
     ```bash
     dotnet restore
     ```
   - Compila:
     ```bash
     dotnet build --configuration Release
     ```
   - (Opcional) Ejecuta tests:
     ```bash
     dotnet test --configuration Release
     ```
   - Publica artefactos:
     ```bash
     dotnet publish -c Release -o $(Build.ArtifactStagingDirectory)
     ```
     y luego:
     ```yaml
     - task: PublishBuildArtifacts@1
       inputs:
         PathtoPublish: '$(Build.ArtifactStagingDirectory)'
         ArtifactName: 'drop'
     ```

3. **Stage Deploy_QA**:
   - Toma el artefacto `drop`.
   - Usa **Azure Web App Deploy** apuntando a:
     - Web App: `$(webAppNameQa)`
     - Azure Subscription: service connection configurada.
   - Resultado: API actualizada en QA.

4. **Stage Deploy_Prod**:
   - Tiene una **aprobación manual** antes de ejecutarse.
   - Despliega el MISMO artefacto a Prod (`app-devtaskhub-api-prod`).
   - Resultado: API actualizada en Producción.

---

## 8. Cómo ver los artefactos y despliegues

En Azure DevOps:

1. Ir a **Pipelines** → **Pipelines** → seleccionar el pipeline.
2. Elegir una ejecución:
   - **Summary**: visión general.
   - **Jobs**: logs paso a paso.
   - **Artefacts**: ver el artefacto `drop` publicado.
3. Ir a **Releases** / **Environments** (según cómo esté configurado) para ver:
   - Despliegue a QA.
   - Despliegue a Prod.
   - Aprobaciones manuales.

---

## 9. Estrategia de rollback

En caso de que un despliegue a Producción salga mal, la estrategia es:

- Volver a una ejecución anterior exitosa del pipeline y:
  - Re-deployar el artefacto anterior desde Azure DevOps, o
  - Usar el historial de despliegues de Azure App Service para hacer swap a una versión anterior.

Esto permite volver a un estado estable sin necesidad de recompilar.

---

## 10. Cómo probar que todo funciona

1. Hacer un cambio pequeño en el backend (por ejemplo, un mensaje en un endpoint).
2. Commit & push a `main`.
3. Ver en Azure DevOps:
   - Stage Build → OK.
   - Stage Deploy_QA → OK.
4. Probar la API en QA:
   ```
   https://app-devtaskhub-api-qa.azurewebsites.net/<ruta>
   ```
5. Aprobar manualmente el paso a Prod.
6. Probar la API en Prod:
   ```
   https://app-devtaskhub-api-prod.azurewebsites.net/<ruta>
   ```

Si esos pasos funcionan, el TP05 está completo desde el punto de vista de CI/CD.

---

## 📄 Más información

- **decisiones.md**: detalles técnicos sobre las decisiones de diseño del pipeline.
- **Documentación de Azure DevOps**: https://docs.microsoft.com/azure/devops/
- **Documentación de .NET**: https://docs.microsoft.com/dotnet/