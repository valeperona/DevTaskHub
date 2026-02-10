using DevTaskHub.Api.Data;
using DevTaskHub.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// --- 1. Configuración de Servicios Básicos ---
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
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

// --- Auth JWT ---
var jwtSection = configuration.GetSection("Jwt");
var secretKey = jwtSection["SecretKey"] ?? "supersecret-devtaskhub-key-change-me";
var issuer = jwtSection["Issuer"];
var audience = jwtSection["Audience"];
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = !string.IsNullOrWhiteSpace(issuer),
            ValidIssuer = issuer,
            ValidateAudience = !string.IsNullOrWhiteSpace(audience),
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<DevTaskHub.Api.Services.ITaskService, DevTaskHub.Api.Services.TaskService>();

// --- 2. Configuración de Base de Datos (FIX para EF Core Design Time) ---

var connectionString = configuration.GetConnectionString("DefaultConnection") ??
                       "Host=localhost;Port=5432;Database=devtaskhub;Username=devtaskhub;Password=devtaskhub";

// FIX: Solo se ejecuta AddDbContext si NO estamos en Design Time (cuando dotnet ef lo usa).
// Si estamos en Design Time, EF Core usará la clase DevTaskHubContextFactory que creamos.
if (Environment.GetEnvironmentVariable("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES") == null)
{
    builder.Services.AddDbContext<DevTaskHubContext>(options =>
    {
        options.UseNpgsql(connectionString);
    });
}

var app = builder.Build();

// --- 3. Migración Automática al Iniciar (Usa el contexto registrado) ---

try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<DevTaskHubContext>();
    dbContext.Database.Migrate();
}
catch (Exception ex)
{
    Console.WriteLine($"DB migrate failed: {ex.Message}");
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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://0.0.0.0:{port}");
app.Run();

public partial class Program;
