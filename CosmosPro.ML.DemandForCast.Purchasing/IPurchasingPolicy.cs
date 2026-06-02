namespace CosmosPro.ML.DemandForCast.Purchasing;

/// <summary>
/// Política de compra. Dado o contexto de um SKU×loja num dia D, computa o par
/// <see cref="PolicyParameters.ReorderPoint"/> (s) e <see cref="PolicyParameters.OrderUpToLevel"/> (S)
/// no estilo clássico (s, S):
/// <list type="bullet">
///   <item>se posição de estoque (físico + em trânsito) ≤ s → pede até S.</item>
///   <item>quantidade pedida = max(0, S − posição de estoque).</item>
/// </list>
///
/// <para>
/// Duas implementações comparadas no TCC:
/// <see cref="Policies.EMaxESegPolicy"/> (média histórica × LT — regra clássica do
/// varejo farma) e <see cref="Policies.ForecastRopPolicy"/> (forecast LightGBM
/// acumulado no LT + safety por desvio do erro).
/// </para>
/// </summary>
public interface IPurchasingPolicy
{
    string Name { get; }

    PolicyParameters Compute(PolicyContext context);
}

/// <summary>Par (s, S) de uma política de reabastecimento num dia D.</summary>
public sealed record PolicyParameters(decimal ReorderPoint, decimal OrderUpToLevel);
