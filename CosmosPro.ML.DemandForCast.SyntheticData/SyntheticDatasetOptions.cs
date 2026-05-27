namespace CosmosPro.ML.DemandForCast.SyntheticData;

/// <summary>
/// Parâmetros para o gerador de dataset sintético farma. Defaults representam
/// o cenário "médio" do roadmap (F4): 20 lojas, 500 SKUs, 12 meses.
/// </summary>
public sealed record SyntheticDatasetOptions
{
    public int NumLojas { get; init; } = 20;
    public int NumSkus { get; init; } = 500;
    public DateOnly DataInicio { get; init; } = new DateOnly(2025, 1, 1);
    public DateOnly DataFim { get; init; } = new DateOnly(2025, 12, 31);

    /// <summary>Seed mestre. Sub-geradores derivam seeds a partir deste para reprodutibilidade.</summary>
    public int Seed { get; init; } = 42;

    /// <summary>
    /// Probabilidade base diária de um SKU estar em ruptura numa loja (estoque 0,
    /// venda = 0 forçada). Curva ABC modifica para cima/baixo por classe.
    /// </summary>
    public double RupturaBaseDiaria { get; init; } = 0.03;

    /// <summary>Fração de SKUs com pelo menos uma promoção no horizonte.</summary>
    public double FracaoSkusEmPromocao { get; init; } = 0.05;

    /// <summary>
    /// Multiplier mínimo/máximo de venda em promoção (uniforme entre os dois). 2-3x
    /// é típico em farma.
    /// </summary>
    public (double Min, double Max) MultiplicadorPromocao { get; init; } = (2.0, 3.0);
}
