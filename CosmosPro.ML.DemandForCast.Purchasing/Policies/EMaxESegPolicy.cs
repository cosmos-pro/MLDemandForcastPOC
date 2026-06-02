namespace CosmosPro.ML.DemandForCast.Purchasing.Policies;

/// <summary>
/// Política clássica eMax/eSeg do varejo farma. Usa a <b>estatística histórica</b>
/// de vendas (média e desvio dos últimos <see cref="JanelaDias"/> dias) para
/// calcular safety stock no LT e cobertura no ciclo:
/// <code>
/// μ      = média(histórico)
/// σ      = desvio padrão amostral(histórico)
/// eSeg   = z · σ · √LT                      (safety stock)
/// s      = μ · LT + eSeg                    (ponto de reabastecimento)
/// eMax = S = μ · (LT + ciclo) + eSeg        (estoque-alvo)
/// </code>
///
/// <para>
/// Vícios conhecidos (relevantes para o TCC):
/// <list type="bullet">
///   <item>μ trata todos os dias iguais → ignora sazonalidade semanal/feriado;</item>
///   <item>σ histórico captura ruído de demanda <i>e</i> de ruptura — tende a inflar;</item>
///   <item>não reage a promoções planejadas para o futuro;</item>
///   <item>itens classe C com vendas raras geram σ grande e cobertura excessiva.</item>
/// </list>
/// </para>
/// </summary>
public sealed class EMaxESegPolicy : IPurchasingPolicy
{
    public string Name => "emax-eseg";

    /// <summary>Janela do histórico considerada para média/desvio (default 28 dias).</summary>
    public int JanelaDias { get; init; } = 28;

    public PolicyParameters Compute(PolicyContext context)
    {
        var amostra = context.HistoricoVendas.Count > JanelaDias
            ? context.HistoricoVendas.AsEnumerable().TakeLast(JanelaDias)
            : context.HistoricoVendas;
        var arr = amostra.Select(v => (double)v).ToArray();

        var mu = arr.Length == 0 ? 0.0 : arr.Average();
        var sigma = StandardDeviation(arr);

        var safety = context.FatorServico * sigma * Math.Sqrt(context.LeadTimeDias);
        var s = mu * context.LeadTimeDias + safety;
        var S = mu * (context.LeadTimeDias + context.CicloDias) + safety;

        return new PolicyParameters(
            ReorderPoint: (decimal)Math.Max(0, s),
            OrderUpToLevel: (decimal)Math.Max(0, S));
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
