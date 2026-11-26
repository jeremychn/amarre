using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.FileProviders;

namespace Remotely.Server.Data;

public class SqliteDbContextDesignTime : IDesignTimeDbContextFactory<SqliteDbContext>
{
    public SqliteDbContext CreateDbContext(string[] args)
    {
        var appSettings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:SQLite"] = "Data Source=remotely.db",
            ["ApplicationOptions:DbProvider"] = "sqlite"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(appSettings)
            .Build();

        return new SqliteDbContext(configuration, new DesignTimeWebHostEnvironment());
    }

}
public class PostgreSqlDbContextDesignTime : IDesignTimeDbContextFactory<PostgreSqlDbContext>
{
    public PostgreSqlDbContext CreateDbContext(string[] args)
    {
        var appSettings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:PostgreSql"] = "Host=localhost;Database=Remotely;Username=postgres;",
            ["ApplicationOptions:DbProvider"] = "PostgreSql"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(appSettings)
            .Build();

        return new PostgreSqlDbContext(configuration, new DesignTimeWebHostEnvironment());
    }
}


public class DesignTimeWebHostEnvironment : IWebHostEnvironment
{
    public string WebRootPath { get; set; } = string.Empty;
    public IFileProvider WebRootFileProvider { get; set; } = default!;
    public string ApplicationName { get; set; } = string.Empty;
    public IFileProvider ContentRootFileProvider { get; set; } = default!;
    public string ContentRootPath { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = "Development";
}