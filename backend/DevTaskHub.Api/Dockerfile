# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiamos el solution y los csproj primero para cachear restore
COPY backend/DevTaskHub.sln backend/DevTaskHub.sln
COPY backend/DevTaskHub.Api/DevTaskHub.Api.csproj backend/DevTaskHub.Api/DevTaskHub.Api.csproj
COPY backend/DevTaskHub.Tests/DevTaskHub.Tests.csproj backend/DevTaskHub.Tests/DevTaskHub.Tests.csproj

RUN dotnet restore backend/DevTaskHub.sln

# Copiamos el resto
COPY backend/ backend/

# Publicamos
RUN dotnet publish backend/DevTaskHub.Api/DevTaskHub.Api.csproj -c Release -o /app/publish

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Cloud Run usa PORT (por default 8080). ASP.NET tiene que escuchar en ese puerto.
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "DevTaskHub.Api.dll"]
