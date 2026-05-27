using ClickHouse.Driver.ADO;
using CosmosPro.ML.DemandForCast.OlapSchema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddClickHouseDataSource("vendas-olap");

using var host = builder.Build();
await host.StartAsync();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
int exitCode = 0;
try
{
    var dataSource = host.Services.GetRequiredService<ClickHouseDataSource>();
    var migrator = new Migrator(dataSource, host.Services.GetRequiredService<ILogger<Migrator>>());
    await migrator.RunAsync();
}
catch (Exception ex)
{
    logger.LogError(ex, "OLAP schema migration failed.");
    exitCode = 1;
}
finally
{
    await host.StopAsync();
}

return exitCode;
