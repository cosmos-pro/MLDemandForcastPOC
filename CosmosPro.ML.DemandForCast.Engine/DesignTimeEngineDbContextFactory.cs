using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CosmosPro.ML.DemandForCast.Engine;

/// <summary>
/// Usado APENAS pelo <c>dotnet ef migrations</c> em design time. Em runtime, o
/// <see cref="EngineDbContext"/> é registrado pelo host (ApiService/Worker) com a
/// connection string injetada pelo Aspire — esta factory não roda.
/// </summary>
internal sealed class DesignTimeEngineDbContextFactory : IDesignTimeDbContextFactory<EngineDbContext>
{
    public EngineDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<EngineDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=engine-designtime;Trusted_Connection=True;")
            .Options;
        return new EngineDbContext(options);
    }
}
