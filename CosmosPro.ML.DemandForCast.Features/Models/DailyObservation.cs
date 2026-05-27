namespace CosmosPro.ML.DemandForCast.Features.Models;

/// <summary>
/// Observação diária de um SKU em uma loja. É o INPUT do feature engineering —
/// a série deve ser ordenável por (Sku, LojaId, Data). Dias sem venda devem
/// existir com <see cref="Quantidade"/> = 0 (o <see cref="FeatureBuilder"/>
/// densifica gaps automaticamente, mas observações já densas evitam reprocessar).
///
/// <para>
/// <see cref="EmRuptura"/> marca dias com estoque zero. Esses dias NÃO viram
/// target de treino (a venda observada subestima a demanda real) — ver
/// CLAUDE.md §6. Servem apenas como contexto histórico para lags.
/// </para>
/// </summary>
public sealed record DailyObservation
{
    public required DateOnly Data { get; init; }
    public required int LojaId { get; init; }
    public required string Sku { get; init; }

    /// <summary>Unidades vendidas no dia. 0 em dia sem venda.</summary>
    public required decimal Quantidade { get; init; }

    /// <summary>Estoque zerado no dia — venda observada não reflete demanda real.</summary>
    public bool EmRuptura { get; init; }

    /// <summary>Havia promoção ativa para o SKU neste dia.</summary>
    public bool EmPromocao { get; init; }

    /// <summary>Preço unitário praticado (ou planejado) no dia.</summary>
    public decimal PrecoUnitario { get; init; }

    // --- Atributos estáticos (mesma para toda a série do SKU/loja) ------------
    // Categóricos, viram features de hierarquia para o modelo global.

    public string Categoria { get; init; } = "";
    public string PrincipioAtivo { get; init; } = "";
    public string ClasseAbc { get; init; } = "";
    public string UF { get; init; } = "";
    public string Regiao { get; init; } = "";
    public string PerfilLoja { get; init; } = "";
}
