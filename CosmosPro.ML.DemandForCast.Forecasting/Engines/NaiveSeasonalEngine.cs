using CosmosPro.ML.DemandForCast.Features.Models;

namespace CosmosPro.ML.DemandForCast.Forecasting.Engines;

/// <summary>
/// Baseline naïve sazonal: a previsão para o dia D é a venda observada no mesmo
/// dia da semana anterior — que, com lead time de 7 dias, é exatamente
/// <see cref="FeatureVector.Lag7"/>. É o baseline mínimo honesto: qualquer modelo
/// que não supere isso não justifica a complexidade (CLAUDE.md §"Backtest").
/// </summary>
public sealed class NaiveSeasonalEngine : IForecastEngine
{
    public string Name => "naive-sazonal";

    public IForecastModel Fit(IReadOnlyList<FeatureVector> trainSet) => Model.Instance;

    private sealed class Model : IForecastModel
    {
        public static readonly Model Instance = new();
        public double Predict(FeatureVector features) => (double)features.Lag7;
    }
}
