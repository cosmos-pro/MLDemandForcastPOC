namespace CosmosPro.ML.DemandForCast.Forecasting.Evaluation;

/// <summary>
/// Métricas de erro de previsão. MAPE acompanha as demais porque quebra em
/// demanda baixa/zero (comum em farma) — WAPE e MAE são as referências
/// primárias (CLAUDE.md §6). Todas calculadas sobre pares (real, previsto).
/// </summary>
public sealed record ForecastMetrics(
    int N,
    double Mae,
    double Rmse,
    double Wape,
    double? Mape)
{
    /// <param name="pairs">(real, previsto). Reais negativos não são esperados (demanda ≥ 0).</param>
    public static ForecastMetrics Compute(IReadOnlyList<(double Actual, double Predicted)> pairs)
    {
        if (pairs.Count == 0)
            return new ForecastMetrics(0, 0, 0, 0, null);

        double absErrSum = 0;
        double sqErrSum = 0;
        double actualAbsSum = 0;

        // MAPE só sobre pontos com real != 0 (divisão por zero não definida).
        double mapeSum = 0;
        int mapeCount = 0;

        foreach (var (actual, predicted) in pairs)
        {
            var err = predicted - actual;
            var absErr = Math.Abs(err);
            absErrSum += absErr;
            sqErrSum += err * err;
            actualAbsSum += Math.Abs(actual);

            if (actual != 0)
            {
                mapeSum += absErr / Math.Abs(actual);
                mapeCount++;
            }
        }

        var n = pairs.Count;
        var mae = absErrSum / n;
        var rmse = Math.Sqrt(sqErrSum / n);
        // WAPE = soma dos erros absolutos / soma dos reais. Robusto a zeros.
        var wape = actualAbsSum > 0 ? absErrSum / actualAbsSum : 0;
        double? mape = mapeCount > 0 ? mapeSum / mapeCount : null;

        return new ForecastMetrics(n, mae, rmse, wape, mape);
    }
}
