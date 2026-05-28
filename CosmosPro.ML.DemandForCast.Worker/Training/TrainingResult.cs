using CosmosPro.ML.DemandForCast.Forecasting.Evaluation;

namespace CosmosPro.ML.DemandForCast.Worker.Training;

/// <summary>
/// Resultado serializável de um treino: comparação walk-forward dos engines.
/// Gravado como JSON em <c>TreinoJob.ResultadoJson</c> e renderizado pela UI.
/// </summary>
public sealed record TrainingResult(
    DateTimeOffset GeradoEm,
    int SkusUsados,
    long TotalObservacoes,
    long TotalFeatures,
    int Folds,
    int TestWindowDias,
    IReadOnlyList<EngineResult> Engines,
    string MelhorEngine);

public sealed record EngineResult(
    string Engine,
    MetricsDto Global,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, MetricsDto>> PorDimensao);

public sealed record MetricsDto(int N, double Mae, double Rmse, double Wape, double? Mape)
{
    public static MetricsDto From(ForecastMetrics m) => new(m.N, m.Mae, m.Rmse, m.Wape, m.Mape);
}
