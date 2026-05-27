using CosmosPro.ML.DemandForCast.Engine;
using CosmosPro.ML.DemandForCast.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Engine DB (EF Core) — para ler/atualizar CargasStage.
builder.AddSqlServerDbContext<EngineDbContext>("engine");

// Stage DB (SqlConnection puro) — para DELETE + SqlBulkCopy.
// `Microsoft.Data.SqlClient` direto, sem EF, porque BULK INSERT é off-EF.
builder.AddSqlServerClient(connectionName: "Stage");

// MinIO client para baixar o ZIP da carga.
builder.AddMinioClient("minio");

builder.Services.AddSingleton<CargaProcessor>();
builder.Services.AddHostedService<ImportWorker>();

var host = builder.Build();
await host.RunAsync();
