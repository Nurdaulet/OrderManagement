using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrderManagement.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by the EF Core tools (<c>dotnet ef</c>) to create an
/// <see cref="AppDbContext"/> without booting the full application host.
/// </summary>
/// <remarks>
/// Migrations are generated into this (Infrastructure) assembly. At runtime the
/// connection string is resolved from configuration; here a local default is used
/// purely so the tooling can build the model.
/// </remarks>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=ordermanagement.db");

        return new AppDbContext(optionsBuilder.Options);
    }
}
