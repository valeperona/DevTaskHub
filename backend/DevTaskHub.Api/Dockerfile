# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY backend/DevTaskHub.sln backend/DevTaskHub.sln
COPY backend/DevTaskHub.Api/DevTaskHub.Api.csproj backend/DevTaskHub.Api/
COPY backend/DevTaskHub.Tests/DevTaskHub.Tests.csproj backend/DevTaskHub.Tests/
# Si tenés más proyectos referenciados por la API, copiá sus .csproj también

RUN dotnet restore backend/DevTaskHub.sln
COPY . .
RUN dotnet publish backend/DevTaskHub.Api/DevTaskHub.Api.csproj -c Release -o /app/publish

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DevTaskHub.Api.dll"]
