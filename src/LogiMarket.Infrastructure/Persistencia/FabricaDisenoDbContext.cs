using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LogiMarket.Infrastructure.Persistencia;

/// <summary>Usada solo por 'dotnet ef migrations' en tiempo de diseño.</summary>
public sealed class FabricaDisenoDbContext : IDesignTimeDbContextFactory<LogiMarketDbContext>
{
    public LogiMarketDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<LogiMarketDbContext>().UseSqlite("Data Source=diseno.db").Options);
}
