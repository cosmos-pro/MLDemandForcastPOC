namespace CosmosPro.ML.DemandForCast.Purchasing.Simulation;

/// <summary>
/// Parâmetros do replay de compras. Imutável — uma instância define o protocolo
/// aplicado igualmente a todas as políticas comparadas.
/// </summary>
public sealed record SimulationOptions
{
    /// <summary>Primeiro dia da janela de simulação (inclusivo).</summary>
    public required DateOnly DataInicio { get; init; }

    /// <summary>Último dia da janela (inclusivo).</summary>
    public required DateOnly DataFim { get; init; }

    /// <summary>Dias entre o pedido e a chegada. Default 7 (mesmo lead time do F5).</summary>
    public int LeadTimeDias { get; init; } = 7;

    /// <summary>Ciclo de revisão de compra — em quantos dias o pedido deve cobrir além do LT.</summary>
    public int CicloDias { get; init; } = 7;

    /// <summary>Fator de serviço z (1.65 ≈ 95%).</summary>
    public double FatorServico { get; init; } = 1.65;

    /// <summary>Janela do histórico de vendas exposta no <see cref="PolicyContext"/> (default 28 dias).</summary>
    public int JanelaHistoricoDias { get; init; } = 28;

    /// <summary>
    /// Janela usada para calcular resíduos do forecast (real − previsto), exposta
    /// na <see cref="PolicyContext.ResiduosForecast"/>. Termina em D − LT (evita
    /// usar resíduo cujo "real" ainda não seria conhecido no momento da decisão).
    /// </summary>
    public int JanelaResiduosDias { get; init; } = 28;

    /// <summary>Custo unitário diário de manter estoque (parametriza KPI de custo total).</summary>
    public decimal CustoCarregamentoDia { get; init; } = 0.01m;

    /// <summary>Custo unitário de venda perdida (parametriza KPI de custo total).</summary>
    public decimal CustoRupturaUnidade { get; init; } = 1.0m;
}
