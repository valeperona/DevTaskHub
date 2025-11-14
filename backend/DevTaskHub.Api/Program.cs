using DevTaskHub.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// --- 1. Configuración de Servicios Básicos ---
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "https://devtaskhub-frontend-qa-e6gsa4f8eqcwerdw.centralus-01.azurewebsites.net",
                "https://devtaskhub-frontend-prod-fuh5ahbeh5gud0d6.centralus-01.azurewebsites.net"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// --- 2. Configuración de Base de Datos (FIX para EF Core Design Time) ---

var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=devtaskhub.db";

// FIX: Solo se ejecuta AddDbContext si NO estamos en Design Time (cuando dotnet ef lo usa).
// Si estamos en Design Time, EF Core usará la clase DevTaskHubContextFactory que creamos.
if (Environment.GetEnvironmentVariable("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES") == null)
{
    builder.Services.AddDbContext<DevTaskHubContext>(options =>
    {
        if (builder.Environment.IsDevelopment())
        {
            options.UseSqlite(connectionString);
        }
        else
        {
            // Usará SQL Server en QA y PROD (ya que ASPNETCORE_ENVIRONMENT != Development)
            options.UseSqlServer(connectionString);
        }
    });
}

var app = builder.Build();

// --- 3. Migración Automática al Iniciar (Usa el contexto registrado) ---

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DevTaskHubContext>();
    dbContext.Database.Migrate();
}

// --- 4. Configuración del Pipeline HTTP ---

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("QA"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
