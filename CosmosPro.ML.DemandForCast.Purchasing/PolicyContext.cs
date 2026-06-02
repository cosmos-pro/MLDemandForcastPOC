namespace CosmosPro.ML.DemandForCast.Purchasing;

/// <summary>
/// Tudo que uma política de compra precisa decidir num dia D para um SKU×loja.
/// Construído pelo <see cref="Simulation.PurchasingSimulator"/> a cada iteração;
/// é determinístico (somente leitura).
/// </summary>
public sealed record PolicyContext
{
    public required string Sku { get; init; }
    public required int LojaId { get; init; }
    public required DateOnly Hoje { get; init; }

    /// <summary>Dias entre o pedido e o recebimento (parametrizado na simulação).</summary>
    public required int LeadTimeDias { get; init; }

    /// <summary>
    /// Dias do ciclo de revisão de compra — quanto cada pedido deve "cobrir" além
    /// do lead time. Em farma normalmente semanal (7).
    /// </summary>
    public required int CicloDias { get; init; }

    /// <summary>
    /// Fator de serviço z (quantil da Normal). 1.65 ≈ 95%, 2.05 ≈ 98%, 1.28 ≈ 90%.
    /// Reflete quanto "remédio extra" mantemos para absorver variabilidade.
    /// </summary>
    public required double FatorServico { get; init; }

    public required decimal EstoqueAtual { get; init; }
    public required decimal EmTransito { get; init; }

    /// <summary>
    /// Histórico recente de vendas (excluindo dias de ruptura). Ordenado do mais
    /// antigo ao mais recente, terminando em D − 1. A política clássica usa para
    /// derivar média/desvio; a forecast-based usa para calcular o resíduo do
    /// modelo (sigma do erro).
    /// </summary>
    public required IReadOnlyList<decimal> HistoricoVendas { get; init; }

    /// <summary>
    /// Forecast pontual para cada dia em [D+1, D+LeadTimeDias+CicloDias]. Mesma
    /// ordem que o intervalo. <c>null</c> quando a política não usa forecast
    /// (e.g., eMax/eSeg clássico).
    /// </summary>
    public IReadOnlyList<decimal>? ForecastFuturo { get; init; }

    /// <summary>
    /// Erros pontuais (real − previsto) do forecast em uma janela histórica
    /// recente do SKU×loja. Permite à política forecast-based dimensionar o
    /// safety stock a partir do desvio do erro de previsão (e não do desvio da
    /// demanda bruta). <c>null</c> quando indisponível.
    /// </summary>
    public IReadOnlyList<decimal>? ResiduosForecast { get; init; }
}
