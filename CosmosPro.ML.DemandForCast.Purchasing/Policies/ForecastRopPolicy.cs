namespace CosmosPro.ML.DemandForCast.Purchasing.Policies;

/// <summary>
/// Política de reabastecimento baseada em forecast (ROP dinâmico). Em vez de usar
/// média histórica, soma a previsão pontual do engine de forecast (LightGBM) no
/// lead time e no ciclo subsequente; o safety stock vem do desvio dos
/// <i>resíduos</i> do forecast (não do desvio da demanda bruta).
/// <code>
/// dLT       = Σ ŷ_d, d ∈ [D+1, D+LT]
/// dLT+ciclo = Σ ŷ_d, d ∈ [D+1, D+LT+ciclo]
/// σ_err     = desvio padrão amostral dos resíduos recentes (real − previsto)
/// safety    = z · σ_err · √LT
/// s         = dLT + safety
/// S         = dLT+ciclo + safety
/// </code>
///
/// <para>
/// Hipótese central do TCC: usar previsão dinâmica (sensível a dia-da-semana,
/// promoção, feriado) reduz σ_err em relação a σ da demanda bruta, encolhendo o
/// safety stock <i>sem</i> piorar nível de serviço — mais giro, menos capital.
/// </para>
///
/// <para>
/// Se <see cref="PolicyContext.ForecastFuturo"/> ou <see cref="PolicyContext.ResiduosForecast"/>
/// estiverem ausentes, a política cai para um comportamento conservador (média do
/// histórico no lugar do forecast; desvio do histórico no lugar do resíduo).
/// </para>
/// </summary>
public sealed class ForecastRopPolicy : IPurchasingPolicy
{
    public string Name => "forecast-rop";

    public PolicyParameters Compute(PolicyContext context)
    {
        var lt = context.LeadTimeDias;
        var ciclo = context.CicloDias;

        decimal forecastLt;
        decimal forecastFull;
        var fc = context.ForecastFuturo;
        if (fc is { Count: > 0 })
        {
            forecastLt = SumClamp(fc, 0, Math.Min(lt, fc.Count));
            forecastFull = SumClamp(fc, 0, Math.Min(lt + ciclo, fc.Count));
        }
        else
        {
            var mu = context.HistoricoVendas.Count == 0
                ? 0m
                : context.HistoricoVendas.Average();
            forecastLt = mu * lt;
            forecastFull = mu * (lt + ciclo);
        }

        var residuos = context.ResiduosForecast;
        double sigma;
        if (residuos is { Count: >= 2 })
        {
            sigma = StandardDeviation(residuos.Select(r => (double)r).ToArray());
        }
        else
        {
            sigma = StandardDeviation(context.HistoricoVendas.Select(v => (double)v).ToArray());
        }

        var safety = (decimal)(context.FatorServico * sigma * Math.Sqrt(lt));

        return new PolicyParameters(
            ReorderPoint: Math.Max(0, forecastLt + safety),
            OrderUpToLevel: Math.Max(0, forecastFull + safety));
    }

    private static decimal SumClamp(IReadOnlyList<decimal> seq, int start, int count)
    {
        decimal s = 0;
        for (int i = start; i < start + count; i++) s += Math.Max(0, seq[i]);
        return s;
    }

    private static double StandardDeviation(double[] arr)
    {
        if (arr.Length < 2) return 0;
        var mean = arr.Average();
        var sumSq = 0.0;
        for (int i = 0; i < arr.Length; i++)
        {
            var d = arr[i] - mean;
            sumSq += d * d;
        }
        return Math.Sqrt(sumSq / (arr.Length - 1));
    }
}
