using System.Text.Json;
using CosmosPro.ML.DemandForCast.Engine;
using CosmosPro.ML.DemandForCast.Engine.Entities;
using CosmosPro.ML.DemandForCast.Features;
using CosmosPro.ML.DemandForCast.Forecasting.Engines;
using CosmosPro.ML.DemandForCast.Purchasing;
using CosmosPro.ML.DemandForCast.Purchasing.Policies;
using CosmosPro.ML.DemandForCast.Purchasing.Simulation;
using CosmosPro.ML.DemandForCast.Worker.Training;
using Microsoft.EntityFrameworkCore;
using Minio;
using Minio.DataModel.Args;

namespace CosmosPro.ML.DemandForCast.Worker.Purchasing;

/// <summary>
/// Executa um job de <see cref="SimulacaoCompra"/>: carrega o TreinoJob origem
/// (modelo LightGBM + parâmetros), reusa o <see cref="StageObservationLoader"/>
/// para puxar as mesmas SKUs do treino, faz o replay com duas políticas
/// (eMax/eSeg clássica vs ROP+forecast) e grava o resultado JSON.
/// </summary>
internal sealed class SimulacaoProcessor(
    IMinioClient minio,
    IConfiguration config,
    IServiceProvider services,
    ILogger<SimulacaoProcessor> logger)
{
    public sealed record Outcome(string ResultadoJson, long SeriesSimuladas);

    public async Task<Outcome> ProcessAsync(SimulacaoCompra job, CancellationToken ct)
    {
        var connStr = config.GetConnectionString("Stage")
            ?? throw new InvalidOperationException("Connection string 'Stage' não encontrada.");

        // 1) Treino origem — modelo + MaxSkus.
        TreinoJob treino;
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EngineDbContext>();
            treino = await db.TreinoJobs.AsNoTracking().FirstOrDefaultAsync(t => t.Id == job.TreinoJobId, ct)
                ?? throw new InvalidOperationException($"TreinoJob {job.TreinoJobId} não encontrado.");
        }
        if (treino.Status != TreinoStatus.Concluido || string.IsNullOrEmpty(treino.ModeloBlobKey))
            throw new InvalidOperationException($"TreinoJob {job.TreinoJobId} ainda não concluiu (Status={treino.Status}).");

        // 2) Observações (mesmas regras do treino: top MaxSkus, ABC dinâmica, ruptura marcada).
        var loader = new StageObservationLoader(connStr, logger);
        var observations = await loader.LoadAsync(treino.MaxSkus, ct);
        if (observations.Count == 0)
            throw new InvalidOperationException("Sem observações no Stage para simular.");

        var maxData = observations.Max(o => o.Data);
        var fim = maxData;
        var inicio = fim.AddDays(-job.JanelaDias + 1);
        logger.LogInformation("Janela da simulação: {Inicio} → {Fim} ({Dias} dias).", inicio, fim, job.JanelaDias);

        // 3) Estoque inicial no dia anterior à janela.
        var skus = observations.Select(o => o.Sku).Distinct().ToArray();
        var estoqueLoader = new StageEstoqueInicialLoader(connStr, logger);
        var estoqueInicialRaw = await estoqueLoader.LoadAsync(skus, inicio, ct);
        var estoqueInicial = estoqueInicialRaw.ToDictionary(
            kv => new PurchasingSimulator.SerieKey(kv.Key.Sku, kv.Key.LojaId),
            kv => kv.Value);

        // 4) Features (mesmo lead time/config do treino — F5 default).
        var features = new FeatureBuilder().Build(observations).ToList();
        logger.LogInformation("{N} features para indexar o forecast.", features.Count);

        // 5) Atributos (categoria, ABC, UF) para drill-down — pega 1ª observação de cada série.
        var atributos = observations
            .GroupBy(o => new PurchasingSimulator.SerieKey(o.Sku, o.LojaId))
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var o = g.First();
                    return new PurchasingSimulator.SerieAttributes(
                        o.Sku, o.LojaId, o.Categoria, o.ClasseAbc, o.UF, o.Regiao);
                });

        // 6) Modelo LightGBM do treino, baixado do MinIO.
        using var model = await DownloadModelAsync(treino.ModeloBlobKey!, ct);
        var forecaster = new LightGbmForecaster(model, features);

        // 7) Simula as duas políticas.
        var options = new SimulationOptions
        {
            DataInicio = inicio,
            DataFim = fim,
            LeadTimeDias = job.LeadTimeDias,
            CicloDias = job.CicloDias,
            FatorServico = job.FatorServico,
        };

        var policies = new IPurchasingPolicy[]
        {
            new EMaxESegPolicy { JanelaDias = options.JanelaHistoricoDias },
            new ForecastRopPolicy(),
        };

        var simulator = new PurchasingSimulator();
        var result = simulator.Run(options, observations, estoqueInicial, atributos, policies, forecaster);

        var output = new SimulationOutput(DateTimeOffset.UtcNow, treino.Id, result);
        var json = JsonSerializer.Serialize(output);
        return new Outcome(json, result.SeriesAvaliadas);
    }

    private async Task<LightGbmForecastModel> DownloadModelAsync(string blobKey, CancellationToken ct)
    {
        var ms = new MemoryStream();
        await minio.GetObjectAsync(new GetObjectArgs()
            .WithBucket(TreinoProcessor.ModelsBucket)
            .WithObject(blobKey)
            .WithCallbackStream(s => s.CopyTo(ms)),
            ct);
        ms.Position = 0;
        logger.LogInformation("Modelo {Key} baixado ({Bytes} bytes).", blobKey, ms.Length);
        return LightGbmForecastModel.Load(ms);
    }
}
