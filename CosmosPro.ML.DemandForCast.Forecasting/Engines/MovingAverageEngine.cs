using CosmosPro.ML.DemandForCast.Features.Models;

namespace CosmosPro.ML.DemandForCast.Forecasting.Engines;

/// <summary>
/// Baseline de média móvel: previsão = média das vendas da janela recente
/// (terminando em D − lead time), já calculada em F5 como
/// <see cref="FeatureVector.RollMean28"/>. Mais estável que o naïve sazonal para
/// itens de giro irregular; serve de segundo ponto de comparação.
/// </summary>
public sealed class MovingAverageEngine(bool useShortWindow = false) : IForecastEngine
{
    private readonly bool _useShortWindow = useShortWindow;

    public string Name => _useShortWindow ? "media-movel-7" : "media-movel-28";

    public IForecastModel Fit(IReadOnlyList<FeatureVector> trainSet) =>
        new Model(_useShortWindow);

    private sealed class Model(bool useShortWindow) : IForecastModel
    {
        public double Predict(FeatureVector features) =>
            (double)(useShortWindow ? features.RollMean7 : features.RollMean28);
    }
}
