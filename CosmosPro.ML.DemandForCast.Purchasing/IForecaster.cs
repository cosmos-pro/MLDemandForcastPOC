namespace CosmosPro.ML.DemandForCast.Purchasing;

/// <summary>
/// Abstrai o acesso ao forecast pontual de demanda de um SKU×loja num dia futuro.
/// Mantém o <see cref="Simulation.PurchasingSimulator"/> agnóstico em relação ao
/// engine (LightGBM, naïve, etc.) e facilita teste sem ML.NET.
/// </summary>
public interface IForecaster
{
    /// <summary>
    /// Demanda prevista (unidades, ≥ 0) para (sku, loja) no dia <paramref name="data"/>.
    /// Implementações devem retornar 0 quando não há features disponíveis para
    /// aquela chave (e.g., início do histórico, série não treinada).
    /// </summary>
    decimal Predict(string sku, int lojaId, DateOnly data);
}
