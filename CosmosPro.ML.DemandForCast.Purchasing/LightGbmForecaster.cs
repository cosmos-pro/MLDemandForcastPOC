using CosmosPro.ML.DemandForCast.Features.Models;
using CosmosPro.ML.DemandForCast.Forecasting.Engines;

namespace CosmosPro.ML.DemandForCast.Purchasing;

/// <summary>
/// Adapter de <see cref="LightGbmForecastModel"/> para <see cref="IForecaster"/>.
/// Indexa as features pré-construídas por (Sku, Loja, Data) e devolve a previsão
/// pontual do modelo. Mantém o simulador agnóstico em relação ao ML.NET.
///
/// <para>
/// As features fornecidas devem ter sido geradas com o mesmo lead time usado no
/// treino — caso contrário, a previsão olha para uma janela de histórico
/// incompatível com a aprendida.
/// </para>
/// </summary>
public sealed class LightGbmForecaster : IForecaster
{
    private readonly LightGbmForecastModel _model;
    private readonly Dictionary<(string Sku, int LojaId, DateOnly Data), FeatureVector> _byKey;

    public LightGbmForecaster(LightGbmForecastModel model, IEnumerable<FeatureVector> features)
    {
        _model = model;
        _byKey = features.ToDictionary(f => (f.Sku, f.LojaId, f.Data));
    }

    public decimal Predict(string sku, int lojaId, DateOnly data)
    {
        if (!_byKey.TryGetValue((sku, lojaId, data), out var f)) return 0m;
        var v = _model.Predict(f);
        return v <= 0 ? 0m : (decimal)v;
    }
}
