using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace Remotely.Server.Data;

public class TestingDbContext : AppDb
{
    public TestingDbContext(IWebHostEnvironment hostEnvironment) 
        : base(hostEnvironment)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseInMemoryDatabase("Remotely");
        base.OnConfiguring(options);
    }
}

/// <summary>Fabrique des tests : base en mémoire (le serveur n'a pas ce fournisseur).</summary>
public class TestingDbFactory(IWebHostEnvironment hostEnvironment) : IAppDbFactory
{
    public AppDb GetContext() => new TestingDbContext(hostEnvironment);
}
