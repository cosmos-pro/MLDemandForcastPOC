using CosmosPro.ML.DemandForCast.Engine;
using CosmosPro.ML.DemandForCast.Worker;
using CosmosPro.ML.DemandForCast.Worker.Training;

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

// Treino do engine de previsão: processador + loop de polling próprio (corre em
// paralelo ao ImportWorker, mesma fila-pattern sobre engine.TreinoJobs).
builder.Services.AddSingleton<TreinoProcessor>();
builder.Services.AddHostedService<TreinoWorker>();

var host = builder.Build();
await host.RunAsync();
