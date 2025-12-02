using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;

namespace DevTaskHub.Api.Data;

public class DevTaskHubContextFactory : IDesignTimeDbContextFactory<DevTaskHubContext>
{
    public DevTaskHubContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = ResolveConnectionString(configuration, args);

        var optionsBuilder = new DbContextOptionsBuilder<DevTaskHubContext>();
        if (IsPostgresConnection(connectionString))
        {
            optionsBuilder.UseNpgsql(connectionString);
        }
        else
        {
            optionsBuilder.UseSqlite(connectionString);
        }

        return new DevTaskHubContext(optionsBuilder.Options);
    }

    private static string ResolveConnectionString(IConfiguration configuration, string[] args)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ??
                               "Host=localhost;Port=5432;Database=devtaskhub;Username=devtaskhub;Password=devtaskhub";

        if (args.Length == 0)
        {
            return connectionString;
        }

        var connectionArgIndex = Array.FindIndex(args, arg => arg.Equals("--connection", StringComparison.OrdinalIgnoreCase));
        if (connectionArgIndex >= 0 && connectionArgIndex + 1 < args.Length)
        {
            return args[connectionArgIndex + 1];
        }

        return connectionString;
    }

    private static bool IsPostgresConnection(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        return connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("Username=", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("User ID=", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("Port=", StringComparison.OrdinalIgnoreCase);
    }
}
