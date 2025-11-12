using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

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

        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=devtaskhub.db";

        var optionsBuilder = new DbContextOptionsBuilder<DevTaskHubContext>();
        optionsBuilder.UseSqlite(connectionString);

        return new DevTaskHubContext(optionsBuilder.Options);
    }
}
